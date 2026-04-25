using Data.Persistence;
using IServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantBilling.Models.Kitchen;
using RestaurantBilling.Controllers;
using System.Text;

namespace RestaurantBilling.IntegrationTests;

public class WorkflowHardeningTests
{
    [Fact]
    public async Task KitchenStatus_ReturnsBadRequest_ForInvalidStatus()
    {
        await using var db = CreateDb();
        var controller = new KOTController(
            db,
            new FakeNumberGenerator(),
            new FakeHubContext<RestaurantBilling.Hubs.KdsHub>(),
            new FakeHubContext<RestaurantBilling.Hubs.AlertHub>());

        var result = await controller.UpdateStatus(new KotStatusUpdateRequest(1, 99, "Unknown"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DailySalesExport_ReturnsCsvFile()
    {
        await using var db = CreateDb();
        var controller = new ReportsController(new FakeSalesReportRepository(), db);
        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var to = DateOnly.FromDateTime(DateTime.UtcNow);

        var result = await controller.DailySalesExport(1, from, to, "csv", CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        var content = Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("Date,Bills,GrossSales,TotalTax,NetSales", content);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private sealed class FakeNumberGenerator : INumberGeneratorService
    {
        public Task<string> GenerateAsync(int outletId, Entities.Enums.NumberSeriesKey key, CancellationToken cancellationToken) => Task.FromResult("K-0001");
    }

    private sealed class FakeSalesReportRepository : ISalesReportRepository
    {
        public Task<IReadOnlyList<IServices.Dtos.DailySalesReportDto>> GetDailySalesAsync(int outletId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<IServices.Dtos.DailySalesReportDto>>([new IServices.Dtos.DailySalesReportDto(from, 2, 1000, 50, 950)]);
        public Task<IReadOnlyList<IServices.Dtos.StockVarianceDto>> GetStockVarianceAsync(int outletId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<IServices.Dtos.StockVarianceDto>>([]);
        public Task<IReadOnlyList<IServices.Dtos.PaymentSummaryDto>> GetPaymentSummaryAsync(int outletId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<IServices.Dtos.PaymentSummaryDto>>([]);
        public Task<IReadOnlyList<IServices.Dtos.VoidReportDto>> GetVoidReportAsync(int outletId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<IServices.Dtos.VoidReportDto>>([]);
        public Task<IReadOnlyList<IServices.Dtos.StockMovementDto>> GetStockMovementAsync(int outletId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<IServices.Dtos.StockMovementDto>>([]);
    }

    private sealed class FakeHubContext<THub> : Microsoft.AspNetCore.SignalR.IHubContext<THub>
        where THub : Microsoft.AspNetCore.SignalR.Hub
    {
        public Microsoft.AspNetCore.SignalR.IHubClients Clients => new FakeHubClients();
        public Microsoft.AspNetCore.SignalR.IGroupManager Groups => new FakeGroupManager();
    }

    private sealed class FakeHubClients : Microsoft.AspNetCore.SignalR.IHubClients
    {
        public Microsoft.AspNetCore.SignalR.IClientProxy All => new FakeClientProxy();
        public Microsoft.AspNetCore.SignalR.IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new FakeClientProxy();
        public Microsoft.AspNetCore.SignalR.IClientProxy Client(string connectionId) => new FakeClientProxy();
        public Microsoft.AspNetCore.SignalR.IClientProxy Clients(IReadOnlyList<string> connectionIds) => new FakeClientProxy();
        public Microsoft.AspNetCore.SignalR.IClientProxy Group(string groupName) => new FakeClientProxy();
        public Microsoft.AspNetCore.SignalR.IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new FakeClientProxy();
        public Microsoft.AspNetCore.SignalR.IClientProxy Groups(IReadOnlyList<string> groupNames) => new FakeClientProxy();
        public Microsoft.AspNetCore.SignalR.IClientProxy User(string userId) => new FakeClientProxy();
        public Microsoft.AspNetCore.SignalR.IClientProxy Users(IReadOnlyList<string> userIds) => new FakeClientProxy();
    }

    private sealed class FakeClientProxy : Microsoft.AspNetCore.SignalR.IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeGroupManager : Microsoft.AspNetCore.SignalR.IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
