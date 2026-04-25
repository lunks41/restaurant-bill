using Entities.Common;

namespace Entities.Integration;

public class PaymentCallbackEvent : BaseEntity
{
    public long PaymentCallbackEventId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsValid { get; set; }
    public string ProcessingStatus { get; set; } = "Received";
    public DateTime? ProcessedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public long? MatchedPaymentId { get; set; }
    public long? MatchedBillId { get; set; }
}
