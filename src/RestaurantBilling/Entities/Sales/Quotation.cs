using Entities.Common;

namespace Entities.Sales;

public class Quotation : BaseEntity
{
    public long QuotationId { get; set; }
    public int OutletId { get; set; }
    public string QuoteNo { get; set; } = string.Empty;
    public DateTime QuoteDate { get; set; } = DateTime.UtcNow;
    public DateOnly BusinessDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public string Status { get; set; } = "Draft";
    public long? ConvertedToBillId { get; set; }

    public List<QuotationItem> Items { get; set; } = [];
}

