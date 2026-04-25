using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using IServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Services;

public class RazorpayUpiProvider(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<RazorpayUpiProvider> logger) : IPaymentProvider
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<PaymentInitResult> InitiateAsync(PaymentRequest request, CancellationToken cancellationToken)
    {
        var keyId = configuration["Razorpay:KeyId"] ?? string.Empty;
        var keySecret = configuration["Razorpay:KeySecret"] ?? string.Empty;

        if (string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(keySecret))
        {
            return new PaymentInitResult(false, string.Empty, null, "Razorpay credentials not configured.");
        }

        SetBasicAuth(keyId, keySecret);

        // Amount in paise (multiply by 100)
        var amountPaise = (long)(request.Amount * 100);

        var payload = new
        {
            type = "upi_qr",
            name = "Restaurant Payment",
            usage = "single_use",
            fixed_amount = true,
            payment_amount = amountPaise,
            description = request.Note ?? $"Bill #{request.BillId}",
            close_by = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds()
        };

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await httpClient.PostAsync("payments/qr_codes", content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Razorpay QR creation failed: {Status} {Body}", response.StatusCode, body);
                return new PaymentInitResult(false, string.Empty, null, $"Razorpay error: {response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var qrId = root.GetProperty("id").GetString() ?? string.Empty;
            var imageUrl = root.TryGetProperty("image_url", out var imgProp) ? imgProp.GetString() : null;

            return new PaymentInitResult(true, qrId, imageUrl, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Razorpay InitiateAsync error");
            return new PaymentInitResult(false, string.Empty, null, ex.Message);
        }
    }

    public async Task<PaymentStatusResult> GetStatusAsync(string providerReference, CancellationToken cancellationToken)
    {
        var keyId = configuration["Razorpay:KeyId"] ?? string.Empty;
        var keySecret = configuration["Razorpay:KeySecret"] ?? string.Empty;
        SetBasicAuth(keyId, keySecret);

        try
        {
            var response = await httpClient.GetAsync($"payments/qr_codes/{providerReference}/payments", cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new PaymentStatusResult(providerReference, "Error", 0);
            }

            using var doc = JsonDocument.Parse(body);
            var items = doc.RootElement.TryGetProperty("items", out var arr) ? arr : doc.RootElement;

            if (items.ValueKind == JsonValueKind.Array && items.GetArrayLength() > 0)
            {
                var first = items[0];
                var status = first.TryGetProperty("status", out var sProp) ? sProp.GetString() ?? "Unknown" : "Unknown";
                var amountPaise = first.TryGetProperty("amount", out var aProp) ? aProp.GetInt64() : 0L;
                var paidAmount = amountPaise / 100m;
                return new PaymentStatusResult(providerReference, status == "captured" ? "Captured" : status, paidAmount);
            }

            return new PaymentStatusResult(providerReference, "Pending", 0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Razorpay GetStatusAsync error");
            return new PaymentStatusResult(providerReference, "Error", 0);
        }
    }

    public async Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken cancellationToken)
    {
        var keyId = configuration["Razorpay:KeyId"] ?? string.Empty;
        var keySecret = configuration["Razorpay:KeySecret"] ?? string.Empty;
        SetBasicAuth(keyId, keySecret);

        // providerReference for refund is the payment_id, not the QR id
        var payload = new { amount = (long)(request.Amount * 100), notes = new { reason = request.Reason } };
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            // The caller must pass the Razorpay payment_id as BillId is not sufficient; we use BillId as stand-in reference key here
            var response = await httpClient.PostAsync($"payments/{request.BillId}/refund", content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Razorpay refund failed: {Status} {Body}", response.StatusCode, body);
                return new RefundResult(false, string.Empty, $"Razorpay refund error: {response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(body);
            var refundId = doc.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? string.Empty : string.Empty;
            return new RefundResult(true, refundId, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Razorpay RefundAsync error");
            return new RefundResult(false, string.Empty, ex.Message);
        }
    }

    private void SetBasicAuth(string keyId, string keySecret)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }
}
