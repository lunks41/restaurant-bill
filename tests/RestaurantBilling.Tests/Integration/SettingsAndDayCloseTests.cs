using Data.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantBilling.Controllers;
using System.Text.Json;

namespace RestaurantBilling.IntegrationTests;

public class SettingsAndDayCloseTests
{
    [Fact]
    public async Task SettingsSaveAndGet_RoundTripsValues()
    {
        await using var db = CreateDb();
        var controller = new SettingsController(db);

        var save = await controller.Save(new SettingsController.SettingsPayloadDto(1, "AA11", "22ABCDE1234F1Z5", "9876"), CancellationToken.None);
        Assert.IsType<OkObjectResult>(save);

        var get = await controller.Get(1, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(get);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        Assert.Equal("AA11", doc.RootElement.GetProperty("fssai").GetString());
        Assert.Equal("22ABCDE1234F1Z5", doc.RootElement.GetProperty("gstin").GetString());
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
