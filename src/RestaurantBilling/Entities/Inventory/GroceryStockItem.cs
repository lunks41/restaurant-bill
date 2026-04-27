using Entities.Common;

namespace Entities.Inventory;

public class GroceryStockItem : BaseEntity
{
    public int GroceryStockItemId { get; set; }
    public int OutletId { get; set; }
    public string GroceryName { get; set; } = string.Empty;
    public int? UnitId { get; set; }
    public decimal PurchaseRate { get; set; }
    public decimal CurrentQty { get; set; }
    public decimal ReorderLevel { get; set; }
}
