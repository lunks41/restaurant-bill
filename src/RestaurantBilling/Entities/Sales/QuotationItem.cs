using Entities.Common;

namespace Entities.Sales;

public class QuotationItem : BaseEntity
{
    public long QuotationItemId { get; set; }
    public long QuotationId { get; set; }
    public int ItemId { get; set; }
    public string ItemNameSnapshot { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
}

