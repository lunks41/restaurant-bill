using Entities.Common;

namespace Entities.Masters;

public class TableMaster : BaseEntity
{
    public int TableMasterId { get; set; }
    public int OutletId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public int Capacity { get; set; } = 2;
    public bool IsOccupied { get; set; }
}
