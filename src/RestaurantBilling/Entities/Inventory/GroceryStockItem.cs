using Entities.Common;

namespace Entities.Inventory;

public class GroceryStockItem : BaseEntity
{
    public int GroceryStockItemId { get; set; }
    public int GroceryId { get; set; }
    public int? UnitId { get; set; }
    public decimal CurrentQty { get; set; }
    public decimal ReorderLevel { get; set; }
}
