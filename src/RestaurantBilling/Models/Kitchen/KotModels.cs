namespace RestaurantBilling.Models.Kitchen;

public sealed record GenerateKotRequest(long BillId, int CaptainUserId);
public sealed record ReprintKotRequest(long KotId, int UserId, string Reason, string ManagerPin);
public sealed record MarkKotPrintedRequest(IReadOnlyList<long> KotIds);

