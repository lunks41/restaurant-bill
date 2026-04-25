using Entities.Configuration;
using Entities.Inventory;
using Entities.Sales;
using Data.Persistence;
using Services;
using Microsoft.EntityFrameworkCore;

namespace RestaurantBilling.IntegrationTests;

public class StockServiceTests
{
    [Fact]
    public async Task DeductSaleStockAsync_Throws_WhenInsufficientAndNegativeNotAllowed()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);
        db.RestaurantSettings.Add(new RestaurantSetting { OutletId = 1, SettingKey = "AllowNegativeStock", SettingValue = "false" });
        db.StockLots.Add(new StockLot { OutletId = 1, ItemId = 11, ReceivedOn = new DateOnly(2026, 4, 24), QtyReceived = 1, QtyRemaining = 1, CostPerUnit = 50 });
        await db.SaveChangesAsync();

        var service = new StockService(db);
        var item = new BillItem(11, "Rice", 3, 100, 0, 0);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeductSaleStockAsync(1, new DateOnly(2026, 4, 24), [item], CancellationToken.None));
    }
}
