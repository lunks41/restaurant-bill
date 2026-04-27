using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Data.Persistence;
using Entities.Configuration;
using System.Text.RegularExpressions;

namespace RestaurantBilling.Controllers;

[Authorize]
public class SettingsController(AppDbContext db, IWebHostEnvironment env) : Controller
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
        map.TryGetValue("RestaurantName", out var restaurantName);
        map.TryGetValue("LogoUrl", out var logoUrl);

        return Ok(new
        {
            restaurantName = restaurantName ?? "RestoBill",
            logoUrl = logoUrl ?? string.Empty,
            fssai = fssai ?? string.Empty,
            gstin = gstin ?? string.Empty,
            managerPin = managerPin ?? string.Empty
        });
    }

    [HttpPost("/settings/save")]
    public async Task<IActionResult> Save([FromBody] SettingsPayloadDto payload, CancellationToken cancellationToken)
    {
        await Upsert(payload.OutletId, "RestaurantName", payload.RestaurantName, cancellationToken);
        await Upsert(payload.OutletId, "LogoUrl", payload.LogoUrl, cancellationToken);
        await Upsert(payload.OutletId, "FssaiLicenseNo", payload.Fssai, cancellationToken);
        await Upsert(payload.OutletId, "Gstin", payload.Gstin, cancellationToken);
        await Upsert(payload.OutletId, "ManagerPin", payload.ManagerPin, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Saved" });
    }

    [HttpPost("/settings/upload-logo")]
    public async Task<IActionResult> UploadLogo([FromQuery] int outletId, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0) return BadRequest("No file uploaded.");
        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return BadRequest("Only image files are allowed.");

        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext) || !new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif" }.Contains(ext))
            return BadRequest("Invalid file type.");

        var webRoot = env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var folder = Path.Combine(webRoot, "uploads", "branding");
        Directory.CreateDirectory(folder);

        var safeName = Regex.Replace(Path.GetFileNameWithoutExtension(file.FileName), @"[^a-zA-Z0-9_-]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "logo";
        var fileName = $"{safeName}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, fileName);

        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var logoUrl = $"/uploads/branding/{fileName}";
        await Upsert(outletId, "LogoUrl", logoUrl, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { logoUrl });
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

    public sealed record SettingsPayloadDto(
        int OutletId,
        string RestaurantName,
        string LogoUrl,
        string Fssai,
        string Gstin,
        string ManagerPin);
}

