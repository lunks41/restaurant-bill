using Entities.Common;

namespace Entities.Sales;

public class BillItem : BaseEntity
{
    public long BillItemId { get; private set; }
    public long BillId { get; private set; }
    public int ItemId { get; private set; }
    public string ItemNameSnapshot { get; private set; } = string.Empty;
    public decimal Qty { get; private set; }
    public decimal RateSnapshot { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TaxableAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal LineTotal { get; private set; }
    public bool IsStockReduced { get; private set; }

    private BillItem() { }

    public BillItem(int itemId, string nameSnapshot, decimal qty, decimal rate, decimal discountAmount, decimal taxAmount)
    {
        ItemId = itemId;
        ItemNameSnapshot = nameSnapshot;
        Qty = qty;
        RateSnapshot = rate;
        DiscountAmount = discountAmount;
        TaxableAmount = (rate * qty) - discountAmount;
        TaxAmount = taxAmount;
        LineTotal = TaxableAmount + TaxAmount;
    }

    public void MarkStockReduced()
    {
        IsStockReduced = true;
    }
}

