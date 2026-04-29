using Entities.Common;

namespace Entities.Inventory;

public class ItemStock : BaseEntity
{
    public int ItemStockId { get; set; }
    public int ItemId { get; set; }
    public int? UnitId { get; set; }
    public decimal OpeningQty { get; set; }
    public decimal PurchasedQty { get; set; }
    public decimal SoldQty { get; set; }
    public decimal DisposedQty { get; set; }
    public decimal ClosingQty { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateOnly StockDate { get; set; }
}
