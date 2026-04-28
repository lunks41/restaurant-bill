using Entities.Sales;

namespace IServices;

public interface IBillingService
{
    Task<Bill> CreateDraftAsync(DateOnly businessDate, IEnumerable<BillItem> items, CancellationToken cancellationToken);
    Task<long> SettleAsync(long billId, IEnumerable<Payment> payments, CancellationToken cancellationToken);
}
