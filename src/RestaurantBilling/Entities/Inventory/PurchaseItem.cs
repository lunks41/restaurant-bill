using Entities.Common;

namespace Entities.Inventory;

public class PurchaseItem : BaseEntity
{
    public long PurchaseItemId { get; set; }
    public long PurchaseId { get; set; }
    public int ItemId { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal LineTotal { get; set; }
}
