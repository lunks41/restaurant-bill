using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using RestaurantBilling.Controllers;
using Data.Persistence;
using Entities.Masters;
using System.Text.Json;

namespace RestaurantBilling.IntegrationTests;

public class MastersControllerTests
{
    [Fact]
    public async Task UnitsData_ReturnsUnitsForOutlet()
    {
        await using var db = CreateDb();
        db.Units.AddRange(
            new Unit { OutletId = 1, UnitName = "Kilogram", UnitCode = "KG" },
            new Unit { OutletId = 2, UnitName = "Litre", UnitCode = "L" });
        await db.SaveChangesAsync();

        var controller = new MastersController(db, CreateHostEnvironment());
        var result = await controller.UnitsData(1, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        Assert.Equal(1, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task CreateUnit_PersistsAndReturnsOk()
    {
        await using var db = CreateDb();
        var controller = new MastersController(db, CreateHostEnvironment());

        var result = await controller.CreateUnit(new MastersController.MasterInputDto("Unit", "PCS", null, null, null, null, null, null, null), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, await db.Units.CountAsync());
    }

    [Fact]
    public void MasterViews_EnableDataTables()
    {
        using var db = CreateDb();
        var controller = new MastersController(db, CreateHostEnvironment());

        controller.Tables();
        Assert.True((bool?)controller.ViewBag.UseDataTables);
        controller.Units();
        Assert.True((bool?)controller.ViewBag.UseDataTables);
        controller.Printers();
        Assert.True((bool?)controller.ViewBag.UseDataTables);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IWebHostEnvironment CreateHostEnvironment()
    {
        return new TestHostEnvironment();
    }

    private sealed class TestHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "RestaurantBilling.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
