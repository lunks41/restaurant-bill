namespace RestaurantBilling.Models.Billing;

public sealed record VoidBillRequest(
    long BillId,
    int UserId,
    string ManagerPin,
    string Reason);
