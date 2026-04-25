using Entities.Sales;

namespace IServices;

public interface IStockService
{
    Task DeductSaleStockAsync(int outletId, DateOnly businessDate, IReadOnlyCollection<BillItem> billItems, CancellationToken cancellationToken);
    Task ReverseSaleStockAsync(int outletId, long billId, DateOnly businessDate, IReadOnlyCollection<BillItem> billItems, CancellationToken cancellationToken);
}

