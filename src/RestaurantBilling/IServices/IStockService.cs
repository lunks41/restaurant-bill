using Entities.Sales;

namespace IServices;

public interface IStockService
{
    Task DeductSaleStockAsync(DateOnly businessDate, IReadOnlyCollection<BillItem> billItems, CancellationToken cancellationToken);
    Task ReverseSaleStockAsync(long billId, DateOnly businessDate, IReadOnlyCollection<BillItem> billItems, CancellationToken cancellationToken);
}

