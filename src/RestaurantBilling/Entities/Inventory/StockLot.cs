using Entities.Common;

namespace Entities.Inventory;

public class StockLot : BaseEntity
{
    public long StockLotId { get; set; }
    public int OutletId { get; set; }
    public int ItemId { get; set; }
    public DateOnly ReceivedOn { get; set; }
    public DateOnly? ExpiryOn { get; set; }
    public decimal QtyReceived { get; set; }
    public decimal QtyRemaining { get; set; }
    public decimal CostPerUnit { get; set; }
}

