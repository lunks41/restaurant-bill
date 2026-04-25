using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Data.Persistence;

namespace RestaurantBilling.Controllers;

[ApiController]
[Route("payments")]
public class PaymentsController(IConfiguration configuration, AppDbContext db, ILogger<PaymentsController> logger) : ControllerBase
{
    [HttpPost("callback/razorpay")]
    public async Task<IActionResult> RazorpayCallback(
        [FromBody] object payload,
        [FromHeader(Name = "X-Razorpay-Signature")] string signature,
        [FromHeader(Name = "X-Event-Id")] string eventId,
        [FromHeader(Name = "X-Event-Timestamp")] long eventTimestampUnix,
        [FromHeader(Name = "X-Callback-Amount")] string? callbackAmountHeader,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(signature))
        {
            return BadRequest("Missing callback headers");
        }

        if (IsTimestampStale(eventTimestampUnix))
        {
            return Unauthorized("Stale event timestamp");
        }
        if (await IsDuplicate("Razorpay", eventId, cancellationToken))
        {
            return Ok(new { status = "duplicate_ignored" });
        }

        var secret = configuration["PaymentCallbacks:RazorpayWebhookSecret"] ?? string.Empty;
        var json = payload.ToString() ?? string.Empty;
        var computed = ComputeHmacSha256Hex(secret, json);
        if (!FixedEquals(signature, computed))
        {
            await RecordEvent("Razorpay", eventId, false, "Rejected", "Invalid Razorpay signature", cancellationToken);
            return Unauthorized("Invalid Razorpay signature");
        }

        var callbackAmount = TryParseAmount(callbackAmountHeader);
        await RecordEvent("Razorpay", eventId, true, "Verified", null, cancellationToken);
        await TryApplySettlement("Razorpay", eventId, callbackAmount, cancellationToken);
        return Ok(new { status = "verified" });
    }

    [HttpPost("callback/phonepe")]
    public async Task<IActionResult> PhonePeCallback(
        [FromBody] string base64Payload,
        [FromHeader(Name = "X-VERIFY")] string verifyHeader,
        [FromHeader(Name = "X-Event-Id")] string eventId,
        [FromHeader(Name = "X-Event-Timestamp")] long eventTimestampUnix,
        CancellationToken cancellationToken,
        [FromHeader(Name = "X-Callback-Amount")] string? callbackAmountHeader = null)
    {
        if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(verifyHeader) || string.IsNullOrWhiteSpace(base64Payload))
        {
            return BadRequest("Missing callback headers or payload");
        }

        if (IsTimestampStale(eventTimestampUnix))
        {
            return Unauthorized("Stale event timestamp");
        }
        if (await IsDuplicate("PhonePe", eventId, cancellationToken))
        {
            return Ok(new { status = "duplicate_ignored" });
        }

        var saltKey = configuration["PaymentCallbacks:PhonePeSaltKey"] ?? string.Empty;
        var saltIndex = configuration["PaymentCallbacks:PhonePeSaltIndex"] ?? "1";
        var raw = base64Payload + "/callback" + saltKey;
        var hash = ComputeSha256Hex(raw);
        var expected = $"{hash}###{saltIndex}";
        if (!FixedEquals(verifyHeader, expected))
        {
            await RecordEvent("PhonePe", eventId, false, "Rejected", "Invalid PhonePe signature", cancellationToken);
            return Unauthorized("Invalid PhonePe signature");
        }

        var callbackAmount = TryParseAmount(callbackAmountHeader);
        await RecordEvent("PhonePe", eventId, true, "Verified", null, cancellationToken);
        callbackAmount ??= TryExtractAmountFromPayload(base64Payload);
        await TryApplySettlement("PhonePe", eventId, callbackAmount, cancellationToken);
        return Ok(new { status = "verified" });
    }

    private bool IsTimestampStale(long eventTimestampUnix)
    {
        var toleranceMinutes = int.TryParse(configuration["PaymentCallbacks:TimestampToleranceMinutes"], out var value) ? value : 5;
        var eventTime = DateTimeOffset.FromUnixTimeSeconds(eventTimestampUnix);
        var now = DateTimeOffset.UtcNow;
        return Math.Abs((now - eventTime).TotalMinutes) > toleranceMinutes;
    }

    private async Task<bool> IsDuplicate(string provider, string eventId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return false;
        }

        return await db.PaymentCallbackEvents
            .AnyAsync(x => x.Provider == provider && x.EventId == eventId, cancellationToken);
    }

    private async Task RecordEvent(string provider, string eventId, bool valid, string processingStatus, string? errorMessage, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return;
        }
        db.PaymentCallbackEvents.Add(new Entities.Integration.PaymentCallbackEvent
        {
            Provider = provider,
            EventId = eventId,
            IsValid = valid,
            ProcessingStatus = processingStatus,
            ProcessedAtUtc = DateTime.UtcNow,
            ErrorMessage = errorMessage
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is not null)
        {
            // Concurrent callback retries may insert same (Provider, EventId); treat as idempotent success.
            logger.LogWarning("Duplicate callback event ignored for {Provider}:{EventId}", provider, eventId);
            db.ChangeTracker.Clear();
        }
    }

    private async Task TryApplySettlement(string provider, string eventId, decimal? callbackAmount, CancellationToken cancellationToken)
    {
        var callbackEvent = await db.PaymentCallbackEvents
            .OrderByDescending(x => x.PaymentCallbackEventId)
            .FirstOrDefaultAsync(x => x.Provider == provider && x.EventId == eventId, cancellationToken);
        if (callbackEvent is null || !callbackEvent.IsValid || callbackEvent.ProcessingStatus == "Applied")
        {
            return;
        }

        var payment = await db.Payments
            .AsNoTracking()
            .OrderByDescending(x => x.PaymentId)
            .FirstOrDefaultAsync(x => x.ReferenceNo == eventId || x.UpiTxnId == eventId, cancellationToken);

        if (payment is null)
        {
            callbackEvent.ProcessingStatus = "Verified";
            callbackEvent.ErrorMessage ??= "No matching payment found for callback event.";
            callbackEvent.ProcessedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        callbackEvent.ProcessingStatus = "Applied";
        callbackEvent.MatchedPaymentId = payment.PaymentId;
        callbackEvent.MatchedBillId = payment.BillId;
        callbackEvent.ProcessedAtUtc = DateTime.UtcNow;
        callbackEvent.ErrorMessage = null;

        if (callbackAmount.HasValue)
        {
            if (Math.Abs(payment.Amount - callbackAmount.Value) <= 0.01m)
            {
                callbackEvent.ProcessingStatus = "Settled";
            }
            else
            {
                callbackEvent.ProcessingStatus = "AmountMismatch";
                callbackEvent.ErrorMessage = $"Payment amount mismatch. Expected {payment.Amount:0.00}, got {callbackAmount.Value:0.00}.";
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static decimal? TryParseAmount(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static decimal? TryExtractAmountFromPayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        var direct = TryExtractAmountFromJson(payload);
        if (direct.HasValue)
        {
            return direct.Value;
        }

        if (TryDecodeBase64(payload, out var decoded))
        {
            return TryExtractAmountFromJson(decoded);
        }

        return null;
    }

    private static decimal? TryExtractAmountFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (TryReadAmount(doc.RootElement, out var amount))
            {
                return amount;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static bool TryReadAmount(JsonElement element, out decimal amount)
    {
        amount = 0m;

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, "amount", StringComparison.OrdinalIgnoreCase) &&
                    TryConvertJsonDecimal(prop.Value, out amount))
                {
                    return true;
                }

                if (TryReadAmount(prop.Value, out amount))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryReadAmount(item, out amount))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryConvertJsonDecimal(JsonElement element, out decimal value)
    {
        value = 0m;
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetDecimal(out value),
            JsonValueKind.String => decimal.TryParse(element.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out value),
            _ => false
        };
    }

    private static bool TryDecodeBase64(string value, out string decoded)
    {
        decoded = string.Empty;
        try
        {
            var bytes = Convert.FromBase64String(value);
            decoded = Encoding.UTF8.GetString(bytes);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string ComputeHmacSha256Hex(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputeSha256Hex(string input)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool FixedEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a ?? string.Empty);
        var bBytes = Encoding.UTF8.GetBytes(b ?? string.Empty);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}

