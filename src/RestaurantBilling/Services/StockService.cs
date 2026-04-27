using IServices;
using Entities.Sales;

namespace Services;

public class StockService : IStockService
{
    public async Task DeductSaleStockAsync(int outletId, DateOnly businessDate, IReadOnlyCollection<BillItem> billItems, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }

    public async Task ReverseSaleStockAsync(int outletId, long billId, DateOnly businessDate, IReadOnlyCollection<BillItem> billItems, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }
}

