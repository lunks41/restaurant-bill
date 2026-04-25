using Data.Persistence;
using IServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantBilling.Controllers;
using RestaurantBilling.Models.DayClose;
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

    [Fact]
    public async Task DayCloseFinalize_CreatesLockAndAdvancesBusinessDate()
    {
        await using var db = CreateDb();
        var controller = new DayCloseController(db, new NoOpAuditService());
        var businessDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var result = await controller.FinalizeClose(new DayCloseFinalizeRequest(1, businessDate, 1, 1000m, 1100m), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.True(await db.DayCloseReports.AnyAsync(x => x.OutletId == 1 && x.BusinessDate == businessDate && x.IsLocked));
        var setting = await db.RestaurantSettings.FirstOrDefaultAsync(x => x.OutletId == 1 && x.SettingKey == "CurrentBusinessDate");
        Assert.NotNull(setting);
        Assert.Equal(businessDate.AddDays(1).ToString("yyyy-MM-dd"), setting!.SettingValue);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private sealed class NoOpAuditService : IAuditService
    {
        public Task LogAsync(int outletId, int userId, string action, string entityType, string entityId, string? oldValuesJson, string? newValuesJson, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
