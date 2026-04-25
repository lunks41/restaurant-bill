using IServices;

namespace Services;

public class CashPaymentProvider : IPaymentProvider
{
    public Task<PaymentInitResult> InitiateAsync(PaymentRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new PaymentInitResult(true, $"CASH-{request.BillId}", null, null));

    public Task<PaymentStatusResult> GetStatusAsync(string providerReference, CancellationToken cancellationToken)
        => Task.FromResult(new PaymentStatusResult(providerReference, "Success", 0));

    public Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new RefundResult(true, $"REF-CASH-{request.BillId}", null));
}

