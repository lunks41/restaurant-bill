using Entities.Common;

namespace Entities.Inventory;

public class ItemStock : BaseEntity
{
    public int ItemStockId { get; set; }
    public int ItemId { get; set; }
    public int? UnitId { get; set; }
    public decimal CurrentQty { get; set; }
    public decimal ReorderLevel { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateOnly StockDate { get; set; }
}
