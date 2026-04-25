namespace RestaurantBilling.Models.Kitchen;

public sealed record KotStatusUpdateRequest(int OutletId, long KotId, string Status);

