using Entities.Common;

namespace Entities.Kitchen;

public class KotHeader : BaseEntity
{
    public long KotHeaderId { get; set; }
    public int OutletId { get; set; }
    public string KotNo { get; set; } = string.Empty;
    public DateTime KotDate { get; set; }
    public DateOnly BusinessDate { get; set; }
    public long? BillId { get; set; }
    public int KitchenStationId { get; set; }
    public string KotEventType { get; set; } = "New";
    public string Status { get; set; } = "Pending";
    public DateTime? KotPrintedAtUtc { get; set; }
    public DateTime? ServedAtUtc { get; set; }
    public string? ServedByUserId { get; set; }
}

