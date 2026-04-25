namespace Entities.Common;

public abstract class BaseEntity
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int CreatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public int? UpdatedBy { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public int? DeletedBy { get; set; }
    public byte[] RowVersion { get; set; } = [];

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

