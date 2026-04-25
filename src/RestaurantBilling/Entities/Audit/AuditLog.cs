using Entities.Common;

namespace Entities.Audit;

public class AuditLog : BaseEntity
{
    public long AuditLogId { get; set; }
    public int OutletId { get; set; }
    public int UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? PreviousHash { get; set; }
    public string EntryHash { get; set; } = string.Empty;
}

