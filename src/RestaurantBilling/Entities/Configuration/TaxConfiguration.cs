using Entities.Common;

namespace Entities.Configuration;

public class TaxConfiguration : BaseEntity
{
    public int TaxConfigurationId { get; set; }
    public string ScenarioType { get; set; } = "Standalone";
    public decimal TotalGstPercent { get; set; }
    public decimal CgstPercent { get; set; }
    public decimal SgstPercent { get; set; }
    public decimal IgstPercent { get; set; }
    public bool IsItcAllowed { get; set; }
    public DateTime EffectiveFrom { get; set; }
}

