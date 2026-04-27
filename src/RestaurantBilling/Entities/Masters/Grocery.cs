using Entities.Common;

namespace Entities.Masters;

public class Grocery : BaseEntity
{
    public int GroceryId { get; set; }
    public int OutletId { get; set; }
    public string GroceryName { get; set; } = string.Empty;
}
