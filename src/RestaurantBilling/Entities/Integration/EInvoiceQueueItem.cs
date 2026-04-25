using Entities.Common;

namespace Entities.Integration;

public class EInvoiceQueueItem : BaseEntity
{
    public long EInvoiceQueueItemId { get; set; }
    public long BillId { get; set; }
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public string? Irn { get; set; }
}

