using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Services;

public class PhonePeQrProvider(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<PhonePeQrProvider> logger) : IPaymentProvider
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<PaymentInitResult> InitiateAsync(PaymentRequest request, CancellationToken cancellationToken)
    {
        var merchantId = configuration["PhonePe:MerchantId"] ?? string.Empty;
        var saltKey = configuration["PhonePe:SaltKey"] ?? string.Empty;
        var saltIndex = configuration["PhonePe:SaltIndex"] ?? "1";
        var callbackUrl = configuration["PhonePe:CallbackUrl"] ?? string.Empty;

        if (string.IsNullOrWhiteSpace(merchantId) || string.IsNullOrWhiteSpace(saltKey))
        {
            return new PaymentInitResult(false, string.Empty, null, "PhonePe credentials not configured.");
        }

        var merchantTxnId = $"MT{request.BillId}{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var amountPaise = (long)(request.Amount * 100);

        var payloadObj = new
        {
            merchantId,
            merchantTransactionId = merchantTxnId,
            merchantUserId = $"MU{request.BillId}",
            amount = amountPaise,
            redirectUrl = callbackUrl,
            redirectMode = "POST",
            callbackUrl,
            paymentInstrument = new { type = "PAY_PAGE" }
        };

        var payloadJson = JsonSerializer.Serialize(payloadObj);
        var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson));

        const string endpoint = "/pg/v1/pay";
        var checksum = ComputeChecksum(payloadBase64, endpoint, saltKey, saltIndex);

        var requestBody = JsonSerializer.Serialize(new { request = payloadBase64 });
        using var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        content.Headers.Add("X-VERIFY", checksum);
        content.Headers.Add("X-MERCHANT-ID", merchantId);

        try
        {
            var response = await httpClient.PostAsync(endpoint, content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("PhonePe pay initiation failed: {Status} {Body}", response.StatusCode, body);
                return new PaymentInitResult(false, string.Empty, null, $"PhonePe error: {response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var success = root.TryGetProperty("success", out var sp) && sp.GetBoolean();
            if (!success)
            {
                var msg = root.TryGetProperty("message", out var mp) ? mp.GetString() : "PhonePe payment failed";
                return new PaymentInitResult(false, string.Empty, null, msg);
            }

            var data = root.GetProperty("data");
            var redirectUrl = data.TryGetProperty("instrumentResponse", out var ir)
                && ir.TryGetProperty("redirectInfo", out var ri)
                && ri.TryGetProperty("url", out var urlProp)
                ? urlProp.GetString()
                : null;

            return new PaymentInitResult(true, merchantTxnId, redirectUrl, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PhonePe InitiateAsync error");
            return new PaymentInitResult(false, string.Empty, null, ex.Message);
        }
    }

    public async Task<PaymentStatusResult> GetStatusAsync(string providerReference, CancellationToken cancellationToken)
    {
        var merchantId = configuration["PhonePe:MerchantId"] ?? string.Empty;
        var saltKey = configuration["PhonePe:SaltKey"] ?? string.Empty;
        var saltIndex = configuration["PhonePe:SaltIndex"] ?? "1";

        var endpoint = $"/pg/v1/status/{merchantId}/{providerReference}";
        var checksum = ComputeStatusChecksum(merchantId, providerReference, saltKey, saltIndex);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, endpoint);
            req.Headers.Add("X-VERIFY", checksum);
            req.Headers.Add("X-MERCHANT-ID", merchantId);

            var response = await httpClient.SendAsync(req, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new PaymentStatusResult(providerReference, "Error", 0);
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var success = root.TryGetProperty("success", out var sp) && sp.GetBoolean();
            if (!success)
            {
                return new PaymentStatusResult(providerReference, "Pending", 0);
            }

            var data = root.GetProperty("data");
            var state = data.TryGetProperty("state", out var stProp) ? stProp.GetString() ?? "PENDING" : "PENDING";
            var amountPaise = data.TryGetProperty("amount", out var aProp) ? aProp.GetInt64() : 0L;
            var paidAmount = amountPaise / 100m;

            return new PaymentStatusResult(providerReference, state == "COMPLETED" ? "Captured" : state, paidAmount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PhonePe GetStatusAsync error");
            return new PaymentStatusResult(providerReference, "Error", 0);
        }
    }

    public async Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken cancellationToken)
    {
        var merchantId = configuration["PhonePe:MerchantId"] ?? string.Empty;
        var saltKey = configuration["PhonePe:SaltKey"] ?? string.Empty;
        var saltIndex = configuration["PhonePe:SaltIndex"] ?? "1";

        var refundTxnId = $"RF{request.BillId}{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var amountPaise = (long)(request.Amount * 100);

        var payloadObj = new
        {
            merchantId,
            merchantTransactionId = refundTxnId,
            originalTransactionId = $"MT{request.BillId}",
            amount = amountPaise,
            callbackUrl = configuration["PhonePe:CallbackUrl"] ?? string.Empty
        };

        var payloadJson = JsonSerializer.Serialize(payloadObj);
        var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson));

        const string endpoint = "/pg/v1/refund";
        var checksum = ComputeChecksum(payloadBase64, endpoint, saltKey, saltIndex);

        var requestBody = JsonSerializer.Serialize(new { request = payloadBase64 });
        using var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        content.Headers.Add("X-VERIFY", checksum);

        try
        {
            var response = await httpClient.PostAsync(endpoint, content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("PhonePe refund failed: {Status} {Body}", response.StatusCode, body);
                return new RefundResult(false, string.Empty, $"PhonePe refund error: {response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(body);
            var success = doc.RootElement.TryGetProperty("success", out var sp) && sp.GetBoolean();
            return success
                ? new RefundResult(true, refundTxnId, null)
                : new RefundResult(false, string.Empty, doc.RootElement.TryGetProperty("message", out var mp) ? mp.GetString() : "Refund failed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PhonePe RefundAsync error");
            return new RefundResult(false, string.Empty, ex.Message);
        }
    }

    private static string ComputeChecksum(string base64Payload, string endpoint, string saltKey, string saltIndex)
    {
        var raw = base64Payload + endpoint + saltKey;
        var hash = ComputeSha256Hex(raw);
        return $"{hash}###{saltIndex}";
    }

    private static string ComputeStatusChecksum(string merchantId, string txnId, string saltKey, string saltIndex)
    {
        var raw = $"/pg/v1/status/{merchantId}/{txnId}{saltKey}";
        var hash = ComputeSha256Hex(raw);
        return $"{hash}###{saltIndex}";
    }

    private static string ComputeSha256Hex(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
