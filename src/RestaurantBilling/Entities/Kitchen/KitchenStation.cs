using Entities.Common;

namespace Entities.Kitchen;

public class KitchenStation : BaseEntity
{
    public int KitchenStationId { get; set; }
    public int OutletId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
