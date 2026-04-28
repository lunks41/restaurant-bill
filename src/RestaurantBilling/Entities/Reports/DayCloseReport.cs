using Entities.Common;

namespace Entities.Reports;

public class DayCloseReport : BaseEntity
{
    public long DayCloseReportId { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DateTime OpenedAtUtc { get; set; }
    public DateTime ClosedAtUtc { get; set; }
    public int ClosedBy { get; set; }
    public decimal OpeningCash { get; set; }
    public decimal ClosingCash { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalTax { get; set; }
    public decimal CashOverShort { get; set; }
    public bool IsLocked { get; set; }
}

