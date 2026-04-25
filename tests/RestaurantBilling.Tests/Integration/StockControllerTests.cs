using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantBilling.Controllers;
using Data.Persistence;
using Entities.Enums;
using Entities.Inventory;
using Entities.Masters;
using System.Text.Json;

namespace RestaurantBilling.IntegrationTests;

public class StockControllerTests
{
    [Fact]
    public void StockViews_EnableDataTablesFlag()
    {
        using var db = CreateDb();
        var controller = new StockController(db);

        controller.Balance();
        Assert.True((bool?)controller.ViewBag.UseDataTables);

        controller.Entry();
        Assert.True((bool?)controller.ViewBag.UseDataTables);

        controller.Adjustment();
        Assert.True((bool?)controller.ViewBag.UseDataTables);

        controller.Loss();
        Assert.True((bool?)controller.ViewBag.UseDataTables);

        controller.Take();
        Assert.True((bool?)controller.ViewBag.UseDataTables);
    }

    [Fact]
    public async Task BalanceData_ReturnsLatestRunningBalancePerItem()
    {
        await using var db = CreateDb();
        db.Items.Add(new Item
        {
            ItemId = 10,
            OutletId = 1,
            CategoryId = 1,
            ItemCode = "ITM-10",
            ItemName = "Tomato",
            IsStockTracked = true,
            ReorderLevel = 5
        });
        db.StockLedger.Add(StockLedgerEntry.Add(1, 10, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), StockReferenceType.Purchase, 0, 20, 10, 0, "open"));
        db.StockLedger.Add(StockLedgerEntry.Deduct(1, 10, DateOnly.FromDateTime(DateTime.UtcNow), StockReferenceType.Sale, 0, 3, 10, 20, "sale"));
        await db.SaveChangesAsync();

        var controller = new StockController(db);
        var result = await controller.BalanceData(1, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        using var doc = JsonDocument.Parse(json);
        var rows = doc.RootElement;
        Assert.Equal(1, rows.GetArrayLength());
        Assert.Equal("Tomato", rows[0].GetProperty("ItemName").GetString());
        Assert.Equal(17m, rows[0].GetProperty("currentQty").GetDecimal());
    }

    [Fact]
    public async Task Items_ReturnsStockTrackedItems()
    {
        await using var db = CreateDb();
        db.Items.AddRange(
            new Item { ItemId = 10, OutletId = 1, CategoryId = 1, ItemCode = "ITM-10", ItemName = "Tomato", IsStockTracked = true, ReorderLevel = 2 },
            new Item { ItemId = 11, OutletId = 1, CategoryId = 1, ItemCode = "ITM-11", ItemName = "Paneer", IsStockTracked = true, ReorderLevel = 2 },
            new Item { ItemId = 12, OutletId = 1, CategoryId = 1, ItemCode = "ITM-12", ItemName = "Water", IsStockTracked = false, ReorderLevel = 0 }
        );
        await db.SaveChangesAsync();

        var controller = new StockController(db);
        var result = await controller.Items(1, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(2, doc.RootElement.GetArrayLength());
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
