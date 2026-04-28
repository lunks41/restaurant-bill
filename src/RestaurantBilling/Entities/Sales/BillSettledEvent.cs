using Entities.Common;

namespace Entities.Sales;

public sealed record BillSettledEvent(long BillId, DateOnly BusinessDate) : IDomainEvent
{
    public DateTime OccurredAtUtc { get; } = DateTime.UtcNow;
}

