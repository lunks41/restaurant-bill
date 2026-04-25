namespace RestaurantBilling.Models.Stock;

public sealed record StockEntryRequest(
    int OutletId,
    int ItemId,
    decimal Qty,
    decimal CostPerUnit,
    DateOnly BusinessDate,
    DateOnly? ExpiryOn);

public sealed record StockAdjustmentRequest(
    int OutletId,
    int ItemId,
    decimal Qty,
    string AdjustmentType,
    decimal Rate,
    DateOnly BusinessDate,
    string Reason);

public sealed record StockLossRequest(
    int OutletId,
    int ItemId,
    decimal Qty,
    decimal Rate,
    DateOnly BusinessDate,
    string Reason);

public sealed record StockTakeRequest(
    int OutletId,
    int ItemId,
    decimal PhysicalQty,
    DateOnly BusinessDate);

