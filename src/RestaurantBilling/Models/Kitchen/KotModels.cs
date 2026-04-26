namespace RestaurantBilling.Models.Kitchen;

public sealed record GenerateKotRequest(int OutletId, long BillId, int CaptainUserId);
public sealed record ReprintKotRequest(int OutletId, long KotId, int UserId, string Reason, string ManagerPin);
public sealed record MarkKotPrintedRequest(int OutletId, IReadOnlyList<long> KotIds);

