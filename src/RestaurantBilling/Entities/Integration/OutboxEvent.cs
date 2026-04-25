using Entities.Common;

namespace Entities.Integration;

public class OutboxEvent : BaseEntity
{
    public long OutboxEventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime? ProcessedAtUtc { get; set; }
    public int RetryCount { get; set; }
    public string? Error { get; set; }
}

