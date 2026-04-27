using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using IServices;
using IServices.Dtos;

namespace Repository;

public class SalesReportRepository(IConfiguration configuration) : ISalesReportRepository
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection missing.");

    public async Task<IReadOnlyList<DailySalesReportDto>> GetDailySalesAsync(int outletId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT b.BusinessDate,
                                  COUNT(1) AS TotalBills,
                                  SUM(b.SubTotal) AS GrossSales,
                                  SUM(b.TaxAmount) AS TotalTax,
                                  SUM(b.GrandTotal) AS NetSales
                           FROM Bills b
                           WHERE b.OutletId = @outletId
                             AND b.BusinessDate BETWEEN @from AND @to
                             AND b.Status IN (1,2)
                           GROUP BY b.BusinessDate
                           ORDER BY b.BusinessDate DESC
                           """;

        var fromDate = from.ToDateTime(TimeOnly.MinValue);
        var toDate = to.ToDateTime(TimeOnly.MinValue);
        await using var conn = new SqlConnection(_connectionString);
        var rows = await conn.QueryAsync<DailySalesReportDto>(new CommandDefinition(sql, new { outletId, from = fromDate, to = toDate }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<StockVarianceDto>> GetStockVarianceAsync(int outletId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return [];
    }

    public async Task<IReadOnlyList<PaymentSummaryDto>> GetPaymentSummaryAsync(int outletId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT CAST(p.PaymentMode AS nvarchar(50)) AS PaymentMode,
                                  SUM(p.Amount) AS TotalAmount,
                                  COUNT(1) AS TransactionCount
                           FROM Payments p
                           INNER JOIN Bills b ON b.BillId = p.BillId
                           WHERE b.OutletId = @outletId
                             AND b.BusinessDate BETWEEN @from AND @to
                           GROUP BY p.PaymentMode
                           ORDER BY SUM(p.Amount) DESC
                           """;
        var fromDate = from.ToDateTime(TimeOnly.MinValue);
        var toDate = to.ToDateTime(TimeOnly.MinValue);
        await using var conn = new SqlConnection(_connectionString);
        var rows = await conn.QueryAsync<PaymentSummaryDto>(new CommandDefinition(sql, new { outletId, from = fromDate, to = toDate }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<VoidReportDto>> GetVoidReportAsync(int outletId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT b.BillId,
                                  b.BillNo,
                                  b.BusinessDate,
                                  b.GrandTotal,
                                  CAST(b.Status AS nvarchar(30)) AS Status
                           FROM Bills b
                           WHERE b.OutletId = @outletId
                             AND b.BusinessDate BETWEEN @from AND @to
                             AND b.Status = 3
                           ORDER BY b.BusinessDate DESC, b.BillId DESC
                           """;
        var fromDate = from.ToDateTime(TimeOnly.MinValue);
        var toDate = to.ToDateTime(TimeOnly.MinValue);
        await using var conn = new SqlConnection(_connectionString);
        var rows = await conn.QueryAsync<VoidReportDto>(new CommandDefinition(sql, new { outletId, from = fromDate, to = toDate }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<StockMovementDto>> GetStockMovementAsync(int outletId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return [];
    }
}

