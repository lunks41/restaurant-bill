using Entities.Common;
using Entities.Enums;

namespace Entities.Masters;

public class Item : BaseEntity
{
    public int ItemId { get; set; }
    public int OutletId { get; set; }
    public int CategoryId { get; set; }
    public int? UnitId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal SalePrice { get; set; }
    public decimal GstPercent { get; set; }
    public bool IsTaxInclusive { get; set; }
    public TaxType TaxType { get; set; } = TaxType.GST;
    public string? ImagePath { get; set; }
}

