using Entities.Common;

namespace Entities.Configuration;

public class TaxMaster : BaseEntity
{
    public int TaxMasterId { get; set; }
    public int OutletId { get; set; }
    public string TaxName { get; set; } = "GST";
    public decimal TaxPercent { get; set; }
    public bool IsInclusive { get; set; }
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;
}
