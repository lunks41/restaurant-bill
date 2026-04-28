using Entities.Common;

namespace Entities.Masters;

public class Unit : BaseEntity
{
    public int UnitId { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public string UnitCode { get; set; } = string.Empty;
}
