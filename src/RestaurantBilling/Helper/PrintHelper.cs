namespace Helper;

public static class PrintHelper
{
    public static string BuildBillQrPayload(long billId, decimal grandTotal, DateOnly businessDate)
        => $"BILL:{billId}|TOTAL:{grandTotal:0.00}|DATE:{businessDate:dd-MMM-yyyy}";
}
