using Entities.Common;

namespace Entities.Masters;

public class Customer : BaseEntity
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Gstin { get; set; }
}
