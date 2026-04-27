using Entities.Common;

namespace Entities.Masters;

public class DiningTables : BaseEntity
{
    public int TableMasterId { get; set; }
    public int OutletId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string Area { get; set; } = "Ground";
    public int Capacity { get; set; } = 2;
    public bool IsOccupied { get; set; }
}
