namespace RestaurantBilling.Models.DayClose;

public sealed record DayClosePreviewRequest(int OutletId, DateOnly BusinessDate);

public sealed record DayCloseFinalizeRequest(
    int OutletId,
    DateOnly BusinessDate,
    int ClosedBy,
    decimal OpeningCash,
    decimal ClosingCash);

