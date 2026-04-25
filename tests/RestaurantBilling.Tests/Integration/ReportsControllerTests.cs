using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantBilling.Controllers;
using IServices;
using IServices.Dtos;
using Data.Persistence;

namespace RestaurantBilling.IntegrationTests;

public class ReportsControllerTests
{
    [Fact]
    public void ReportViews_EnableDataTablesFlag()
    {
        using var db = CreateDb();
        var controller = new ReportsController(new FakeSalesReportRepository(), db);

        controller.DailySalesView();
        Assert.True((bool?)controller.ViewBag.UseDataTables);

        controller.BillWise();
        Assert.True((bool?)controller.ViewBag.UseDataTables);

        controller.ItemWise();
        Assert.True((bool?)controller.ViewBag.UseDataTables);

        controller.PaymentMode();
        Assert.True((bool?)controller.ViewBag.UseDataTables);

        controller.StockReport();
        Assert.True((bool?)controller.ViewBag.UseDataTables);

        controller.StockLoss();
        Assert.True((bool?)controller.ViewBag.UseDataTables);
    }

    [Fact]
    public async Task ItemWiseData_ReturnsRepositoryRows()
    {
        await using var db = CreateDb();
        var controller = new ReportsController(new FakeSalesReportRepository(), db);
        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var to = DateOnly.FromDateTime(DateTime.UtcNow);

        var result = await controller.ItemWiseData(1, from, to, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var rows = Assert.IsAssignableFrom<IReadOnlyList<StockVarianceDto>>(ok.Value);
        Assert.Single(rows);
        Assert.Equal("Paneer Tikka", rows[0].ItemName);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private sealed class FakeSalesReportRepository : ISalesReportRepository
    {
        public Task<IReadOnlyList<DailySalesReportDto>> GetDailySalesAsync(int outletId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DailySalesReportDto>>(
            [
                new DailySalesReportDto(from, 5, 1000m, 50m, 950m)
            ]);

        public Task<IReadOnlyList<StockVarianceDto>> GetStockVarianceAsync(int outletId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<StockVarianceDto>>(
            [
                new StockVarianceDto(1, "Paneer Tikka", 10m, 9m, -1m, -250m)
            ]);

        public Task<IReadOnlyList<PaymentSummaryDto>> GetPaymentSummaryAsync(int outletId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<PaymentSummaryDto>>(
            [
                new PaymentSummaryDto("UPI", 600m, 3)
            ]);

        public Task<IReadOnlyList<VoidReportDto>> GetVoidReportAsync(int outletId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<VoidReportDto>>(
            [
                new VoidReportDto(1, "B-1", from, 200m, "Cancelled")
            ]);

        public Task<IReadOnlyList<StockMovementDto>> GetStockMovementAsync(int outletId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<StockMovementDto>>(
            [
                new StockMovementDto(1, "Paneer", from, 2m, 1m, 15m, "Purchase")
            ]);
    }
}
