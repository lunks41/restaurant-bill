using Entities.Common;

namespace Entities.Organisation;

public class Outlet : BaseEntity
{
    public int OutletId { get; set; }
    public int TenantId { get; set; }
    public string OutletName { get; set; } = string.Empty;
    public string StateCode { get; set; } = "27";
    public string? Gstin { get; set; }
    public string FssaiNumber { get; set; } = string.Empty;
    public bool IsSpecifiedPremises { get; set; }
    public bool IsCompositionScheme { get; set; }
    public bool EInvoicingEnabled { get; set; }
}

