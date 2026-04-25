namespace Entities.Common;

public interface IDomainEvent
{
    DateTime OccurredAtUtc { get; }
}

