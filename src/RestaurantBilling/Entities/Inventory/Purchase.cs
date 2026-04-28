using Entities.Common;

namespace Entities.Inventory;

public class Purchase : BaseEntity
{
    public long PurchaseId { get; set; }
    public int SupplierId { get; set; }
    public string PurchaseNo { get; set; } = string.Empty;
    public DateOnly BusinessDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }

    public List<PurchaseItem> Items { get; set; } = [];
}
