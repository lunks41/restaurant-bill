using System.ComponentModel.DataAnnotations;

namespace RestaurantBilling.Models.Masters;

public class TaxConfigurationInputModel
{
    public int? TaxConfigurationId { get; set; }

    [Required]
    [StringLength(40)]
    public string ScenarioType { get; set; } = "Standalone";

    [Range(0, 100)]
    public decimal TotalGstPercent { get; set; }

    [Range(0, 100)]
    public decimal CgstPercent { get; set; }

    [Range(0, 100)]
    public decimal SgstPercent { get; set; }

    [Range(0, 100)]
    public decimal IgstPercent { get; set; }

    public bool IsItcAllowed { get; set; }

    [DataType(DataType.Date)]
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;
}

