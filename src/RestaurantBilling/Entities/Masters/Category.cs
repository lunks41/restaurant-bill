using Entities.Common;

namespace Entities.Masters;

public class Category : BaseEntity
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

