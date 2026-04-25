using Entities.Common;

namespace Entities.Kitchen;

public class KotItem : BaseEntity
{
    public long KotItemId { get; set; }
    public long KotHeaderId { get; set; }
    public int ItemId { get; set; }
    public string ItemNameSnapshot { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public string? SpecialInstructions { get; set; }
    public bool IsVoid { get; set; }
}

