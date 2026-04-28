using Entities.Common;

namespace Entities.Masters;

public class Supplier : BaseEntity
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? ContactNo { get; set; }
    public string? Gstin { get; set; }
}
