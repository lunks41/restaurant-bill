using Entities.Common;

namespace Entities.Audit;

public class ReprintLog : BaseEntity
{
    public long ReprintLogId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public long DocumentId { get; set; }
    public int ReprintedBy { get; set; }
    public DateTime ReprintedAtUtc { get; set; } = DateTime.UtcNow;
    public string Reason { get; set; } = string.Empty;
}

