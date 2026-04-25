using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Data.Persistence;
using Entities.Configuration;

namespace RestaurantBilling.Controllers;

[Authorize]
public class SettingsController(AppDbContext db) : Controller
{
    [HttpGet("/settings")]
    [HttpGet("/setting/info")]
    [HttpGet("/setting/printplate")]
    public IActionResult Index()
    {
        ViewBag.UseDataTables = true;
        return View();
    }

    [HttpGet("/settings/get")]
    public async Task<IActionResult> Get([FromQuery] int outletId, CancellationToken cancellationToken)
    {
        var map = await db.RestaurantSettings
            .Where(x => x.OutletId == outletId)
            .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue, cancellationToken);

        map.TryGetValue("FssaiLicenseNo", out var fssai);
        map.TryGetValue("Gstin", out var gstin);
        map.TryGetValue("ManagerPin", out var managerPin);

        return Ok(new
        {
            fssai = fssai ?? string.Empty,
            gstin = gstin ?? string.Empty,
            managerPin = managerPin ?? string.Empty
        });
    }

    [HttpPost("/settings/save")]
    public async Task<IActionResult> Save([FromBody] SettingsPayloadDto payload, CancellationToken cancellationToken)
    {
        await Upsert(payload.OutletId, "FssaiLicenseNo", payload.Fssai, cancellationToken);
        await Upsert(payload.OutletId, "Gstin", payload.Gstin, cancellationToken);
        await Upsert(payload.OutletId, "ManagerPin", payload.ManagerPin, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Saved" });
    }

    private async Task Upsert(int outletId, string key, string value, CancellationToken cancellationToken)
    {
        var row = await db.RestaurantSettings.FirstOrDefaultAsync(x => x.OutletId == outletId && x.SettingKey == key, cancellationToken);
        if (row is null)
        {
            db.RestaurantSettings.Add(new RestaurantSetting
            {
                OutletId = outletId,
                SettingKey = key,
                SettingValue = value ?? string.Empty
            });
            return;
        }

        row.SettingValue = value ?? string.Empty;
    }

    public sealed record SettingsPayloadDto(int OutletId, string Fssai, string Gstin, string ManagerPin);
}

