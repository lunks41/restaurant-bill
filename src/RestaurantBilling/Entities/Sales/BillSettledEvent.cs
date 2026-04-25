using Entities.Common;

namespace Entities.Sales;

public sealed record BillSettledEvent(long BillId, int OutletId, DateOnly BusinessDate) : IDomainEvent
{
    public DateTime OccurredAtUtc { get; } = DateTime.UtcNow;
}

