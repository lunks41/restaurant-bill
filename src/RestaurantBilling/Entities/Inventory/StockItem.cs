using Entities.Common;

namespace Entities.Inventory;

public class StockItem : BaseEntity
{
    public int StockItemId { get; set; }
    public int OutletId { get; set; }
    public int ItemId { get; set; }
    public decimal CurrentQty { get; set; }
    public decimal ReorderLevel { get; set; }
}
