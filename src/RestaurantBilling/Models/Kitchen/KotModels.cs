namespace RestaurantBilling.Models.Kitchen;

public sealed record GenerateKotRequest(int OutletId, long BillId, int CaptainUserId);
public sealed record ReprintKotRequest(int OutletId, long KotId, int UserId, string Reason, string ManagerPin);

