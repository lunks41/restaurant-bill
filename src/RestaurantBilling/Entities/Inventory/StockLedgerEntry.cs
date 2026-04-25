using Entities.Common;
using Entities.Enums;

namespace Entities.Inventory;

public class StockLedgerEntry : BaseEntity
{
    public long StockLedgerEntryId { get; private set; }
    public int OutletId { get; private set; }
    public int ItemId { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public StockReferenceType ReferenceType { get; private set; }
    public long ReferenceId { get; private set; }
    public decimal InQty { get; private set; }
    public decimal OutQty { get; private set; }
    public decimal Rate { get; private set; }
    public decimal RunningBalance { get; private set; }
    public string Remarks { get; private set; } = string.Empty;

    private StockLedgerEntry() { }

    public static StockLedgerEntry Deduct(
        int outletId,
        int itemId,
        DateOnly businessDate,
        StockReferenceType referenceType,
        long referenceId,
        decimal qty,
        decimal rate,
        decimal currentBalance,
        string remarks)
    {
        var newBalance = currentBalance - qty;
        return new StockLedgerEntry
        {
            OutletId = outletId,
            ItemId = itemId,
            BusinessDate = businessDate,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            OutQty = qty,
            Rate = rate,
            RunningBalance = newBalance,
            Remarks = remarks
        };
    }

    public static StockLedgerEntry Add(
        int outletId,
        int itemId,
        DateOnly businessDate,
        StockReferenceType referenceType,
        long referenceId,
        decimal qty,
        decimal rate,
        decimal currentBalance,
        string remarks)
    {
        var newBalance = currentBalance + qty;
        return new StockLedgerEntry
        {
            OutletId = outletId,
            ItemId = itemId,
            BusinessDate = businessDate,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            InQty = qty,
            Rate = rate,
            RunningBalance = newBalance,
            Remarks = remarks
        };
    }
}

