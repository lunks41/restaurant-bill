namespace IServices;

public interface IPaymentProvider
{
    Task<PaymentInitResult> InitiateAsync(PaymentRequest request, CancellationToken cancellationToken);
    Task<PaymentStatusResult> GetStatusAsync(string providerReference, CancellationToken cancellationToken);
    Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken cancellationToken);
}

public sealed record PaymentRequest(long BillId, decimal Amount, string Currency, string Note);
public sealed record PaymentInitResult(bool IsSuccess, string ProviderReference, string? QrPayload, string? Error);
public sealed record PaymentStatusResult(string ProviderReference, string Status, decimal PaidAmount);
public sealed record RefundRequest(long BillId, decimal Amount, string Reason);
public sealed record RefundResult(bool IsSuccess, string RefundReference, string? Error);

