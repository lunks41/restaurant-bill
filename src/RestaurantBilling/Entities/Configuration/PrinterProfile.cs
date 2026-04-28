using Entities.Common;

namespace Entities.Configuration;

public class PrinterProfile : BaseEntity
{
    public int PrinterProfileId { get; set; }
    public string PrinterName { get; set; } = string.Empty;
    public string PrinterType { get; set; } = "Thermal";
    public string? DevicePath { get; set; }
    public bool IsDefault { get; set; }
}
