namespace IServices.Dtos;

public sealed record DailySalesReportDto(DateOnly BusinessDate, int TotalBills, decimal GrossSales, decimal TotalTax, decimal NetSales);
public sealed record StockVarianceDto(int ItemId, string ItemName, decimal TheoreticalConsumption, decimal ActualConsumption, decimal VarianceQty, decimal VarianceValue);
public sealed record PaymentSummaryDto(string PaymentMode, decimal TotalAmount, int TransactionCount);
public sealed record VoidReportDto(long BillId, string BillNo, DateOnly BusinessDate, decimal GrandTotal, string Status);
public sealed record StockMovementDto(int ItemId, string ItemName, DateOnly BusinessDate, decimal InQty, decimal OutQty, decimal RunningBalance, string ReferenceType);

