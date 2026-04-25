using Microsoft.EntityFrameworkCore;
using IServices;
using Entities.Enums;
using Entities.Inventory;
using Entities.Sales;
using Data.Persistence;

namespace Services;

public class StockService(AppDbContext db) : IStockService
{
    public async Task DeductSaleStockAsync(int outletId, DateOnly businessDate, IReadOnlyCollection<BillItem> billItems, CancellationToken cancellationToken)
    {
        var allowNegative = await db.RestaurantSettings
            .Where(x => x.OutletId == outletId && x.SettingKey == "AllowNegativeStock")
            .Select(x => x.SettingValue == "true")
            .FirstOrDefaultAsync(cancellationToken);

        foreach (var billItem in billItems.Where(x => !x.IsStockReduced))
        {
            var pendingQty = billItem.Qty;
            var lots = await db.StockLots
                .Where(x => x.OutletId == outletId && x.ItemId == billItem.ItemId && x.QtyRemaining > 0)
                .OrderBy(x => x.ReceivedOn)
                .ToListAsync(cancellationToken);

            foreach (var lot in lots)
            {
                if (pendingQty <= 0)
                {
                    break;
                }

                var consumeQty = Math.Min(pendingQty, lot.QtyRemaining);
                lot.QtyRemaining -= consumeQty;
                pendingQty -= consumeQty;

                var latestBalance = await db.StockLedger
                    .Where(x => x.OutletId == outletId && x.ItemId == billItem.ItemId)
                    .OrderByDescending(x => x.StockLedgerEntryId)
                    .Select(x => x.RunningBalance)
                    .FirstOrDefaultAsync(cancellationToken);

                var ledgerEntry = StockLedgerEntry.Deduct(
                    outletId,
                    billItem.ItemId,
                    businessDate,
                    StockReferenceType.Sale,
                    0,
                    consumeQty,
                    lot.CostPerUnit,
                    latestBalance,
                    "Bill settlement stock deduction");

                db.StockLedger.Add(ledgerEntry);
            }

            if (pendingQty > 0 && !allowNegative)
            {
                throw new InvalidOperationException($"Insufficient stock for item {billItem.ItemId}");
            }

            if (pendingQty > 0 && allowNegative)
            {
                var latestBalance = await db.StockLedger
                    .Where(x => x.OutletId == outletId && x.ItemId == billItem.ItemId)
                    .OrderByDescending(x => x.StockLedgerEntryId)
                    .Select(x => x.RunningBalance)
                    .FirstOrDefaultAsync(cancellationToken);
                db.StockLedger.Add(StockLedgerEntry.Deduct(
                    outletId, billItem.ItemId, businessDate, StockReferenceType.Sale, 0, pendingQty, 0m, latestBalance, "Negative stock deduction"));
            }

            billItem.MarkStockReduced();
        }
    }

    public async Task ReverseSaleStockAsync(int outletId, long billId, DateOnly businessDate, IReadOnlyCollection<BillItem> billItems, CancellationToken cancellationToken)
    {
        foreach (var billItem in billItems)
        {
            var latestBalance = await db.StockLedger
                .Where(x => x.OutletId == outletId && x.ItemId == billItem.ItemId)
                .OrderByDescending(x => x.StockLedgerEntryId)
                .Select(x => x.RunningBalance)
                .FirstOrDefaultAsync(cancellationToken);

            var restoreRate = await db.StockLots
                .Where(x => x.OutletId == outletId && x.ItemId == billItem.ItemId)
                .OrderByDescending(x => x.StockLotId)
                .Select(x => x.CostPerUnit)
                .FirstOrDefaultAsync(cancellationToken);

            db.StockLedger.Add(StockLedgerEntry.Add(
                outletId,
                billItem.ItemId,
                businessDate,
                StockReferenceType.VoidReversal,
                billId,
                billItem.Qty,
                restoreRate,
                latestBalance,
                "Bill void stock reversal"));

            db.StockLots.Add(new StockLot
            {
                OutletId = outletId,
                ItemId = billItem.ItemId,
                ReceivedOn = businessDate,
                QtyReceived = billItem.Qty,
                QtyRemaining = billItem.Qty,
                CostPerUnit = restoreRate
            });
        }
    }
}

