using IServices;
using Entities.Sales;

namespace Services;

public class StockService : IStockService
{
    public async Task DeductSaleStockAsync(DateOnly businessDate, IReadOnlyCollection<BillItem> billItems, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }

    public async Task ReverseSaleStockAsync(long billId, DateOnly businessDate, IReadOnlyCollection<BillItem> billItems, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }
}

