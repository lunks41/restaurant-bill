using Entities.Common;
using Entities.Enums;

namespace Entities.Inventory;

public class StockLedger : BaseEntity
{
    public long StockLedgerId { get; set; }
    public int OutletId { get; set; }
    public int ItemId { get; set; }
    public DateOnly BusinessDate { get; set; }
    public StockReferenceType ReferenceType { get; set; }
    public long ReferenceId { get; set; }
    public decimal InQty { get; set; }
    public decimal OutQty { get; set; }
    public decimal Rate { get; set; }
    public decimal RunningBalance { get; set; }
    public string? Remarks { get; set; }
}
