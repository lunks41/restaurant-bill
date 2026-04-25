using Services.Billing.Commands.SettleBill;
using Services.Billing;
using Helper;
using IServices;
using Entities.Enums;
using Entities.Reports;
using Entities.Sales;
using Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace RestaurantBilling.Application.Tests;

public class SettleBillCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsConflict_WhenBusinessDateLocked()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);
        db.DayCloseReports.Add(new DayCloseReport { OutletId = 1, BusinessDate = new DateOnly(2026, 4, 24), IsLocked = true });
        await db.SaveChangesAsync();

        var handler = new SettleBillCommandHandler(
            db,
            new BillingCalculatorService(),
            new TestNumberGeneratorService(),
            new TestStockService());

        var cmd = new SettleBillCommand(
            1,
            BillType.Takeaway,
            new DateOnly(2026, 4, 24),
            false,
            0,
            false,
            0,
            [new SettleBillItemInput(1, "Item", 1, 100, 0, 5, false, TaxType.GST, false)],
            [new SettleBillPaymentInput(PaymentMode.Cash, 105, null, null, null)]);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    private sealed class TestNumberGeneratorService : INumberGeneratorService
    {
        public Task<string> GenerateAsync(int outletId, NumberSeriesKey key, CancellationToken cancellationToken) => Task.FromResult("TI-2026-000001");
    }

    private sealed class TestStockService : IStockService
    {
        public Task DeductSaleStockAsync(int outletId, DateOnly businessDate, IReadOnlyCollection<BillItem> billItems, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ReverseSaleStockAsync(int outletId, long billId, DateOnly businessDate, IReadOnlyCollection<BillItem> billItems, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
