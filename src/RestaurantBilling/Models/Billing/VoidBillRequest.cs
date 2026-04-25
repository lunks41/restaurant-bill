namespace RestaurantBilling.Models.Billing;

public sealed record VoidBillRequest(
    int OutletId,
    long BillId,
    int UserId,
    string ManagerPin,
    string Reason);
