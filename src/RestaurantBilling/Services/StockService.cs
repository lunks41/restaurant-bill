using IServices;
using Data.Persistence;
using Microsoft.EntityFrameworkCore;
using Entities.Inventory;
using Entities.Sales;

namespace Services;

public class StockService(AppDbContext db) : IStockService
{
    public async Task DeductSaleStockAsync(DateOnly businessDate, IReadOnlyCollection<BillItem> billItems, CancellationToken cancellationToken)
    {
        if (billItems.Count == 0) return;

        var itemIds = billItems.Select(x => x.ItemId).Distinct().ToList();
        if (itemIds.Count == 0) return;

        var stockItemIds = (await db.Items
            .Where(x => !x.IsDeleted && x.IsStock && itemIds.Contains(x.ItemId))
            .Select(x => x.ItemId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        if (stockItemIds.Count == 0) return;

        var stockRows = await db.ItemStocks
            .Where(x => !x.IsDeleted && stockItemIds.Contains(x.ItemId))
            .ToListAsync(cancellationToken);

        foreach (var billItem in billItems)
        {
            if (billItem.IsStockReduced) continue;
            if (!stockItemIds.Contains(billItem.ItemId)) continue;

            var row = stockRows.FirstOrDefault(x => x.ItemId == billItem.ItemId);
            if (row is null)
            {
                row = new ItemStock
                {
                    ItemId = billItem.ItemId,
                    OpeningQty = 0m,
                    PurchasedQty = 0m,
                    SoldQty = 0m,
                    DisposedQty = 0m,
                    ClosingQty = 0m,
                    Type = "Opening",
                    StockDate = businessDate,
                    IsActive = true,
                    IsDeleted = false
                };
                db.ItemStocks.Add(row);
                stockRows.Add(row);
            }

            row.SoldQty += billItem.Qty;
            row.ClosingQty = row.OpeningQty + row.PurchasedQty - row.SoldQty - row.DisposedQty;
            row.IsActive = true;
            row.StockDate = businessDate;
            billItem.MarkStockReduced();
        }
    }

    public async Task ReverseSaleStockAsync(long billId, DateOnly businessDate, IReadOnlyCollection<BillItem> billItems, CancellationToken cancellationToken)
    {
        if (billItems.Count == 0) return;

        var itemIds = billItems.Select(x => x.ItemId).Distinct().ToList();
        if (itemIds.Count == 0) return;

        var stockItemIds = (await db.Items
            .Where(x => !x.IsDeleted && x.IsStock && itemIds.Contains(x.ItemId))
            .Select(x => x.ItemId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        if (stockItemIds.Count == 0) return;

        var stockRows = await db.ItemStocks
            .Where(x => !x.IsDeleted && stockItemIds.Contains(x.ItemId))
            .ToListAsync(cancellationToken);

        foreach (var billItem in billItems)
        {
            if (!billItem.IsStockReduced) continue;
            if (!stockItemIds.Contains(billItem.ItemId)) continue;

            var row = stockRows.FirstOrDefault(x => x.ItemId == billItem.ItemId);
            if (row is null)
            {
                row = new ItemStock
                {
                    ItemId = billItem.ItemId,
                    OpeningQty = 0m,
                    PurchasedQty = 0m,
                    SoldQty = 0m,
                    DisposedQty = 0m,
                    ClosingQty = 0m,
                    Type = "Opening",
                    StockDate = businessDate,
                    IsActive = true,
                    IsDeleted = false
                };
                db.ItemStocks.Add(row);
                stockRows.Add(row);
            }

            row.SoldQty = Math.Max(0m, row.SoldQty - billItem.Qty);
            row.ClosingQty = row.OpeningQty + row.PurchasedQty - row.SoldQty - row.DisposedQty;
            row.IsActive = true;
            row.StockDate = businessDate;
            billItem.MarkStockRestored();
        }
    }
}

