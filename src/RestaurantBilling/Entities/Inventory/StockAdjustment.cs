using Entities.Common;

namespace Entities.Inventory;

public class StockAdjustment : BaseEntity
{
    public long StockAdjustmentId { get; set; }
    public int ItemId { get; set; }
    public DateOnly BusinessDate { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public string AdjustmentType { get; set; } = "Add";
    public string Reason { get; set; } = string.Empty;
}
