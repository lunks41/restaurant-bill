using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Data.Persistence;
using Entities.Configuration;
using Services.Jobs;
using System.Text.RegularExpressions;

namespace RestaurantBilling.Controllers;

[Authorize]
public class SettingsController(AppDbContext db, IWebHostEnvironment env, PerishableStockExpiryJob perishableStockExpiryJob) : Controller
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
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var map = await db.RestaurantSettings
            .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue, cancellationToken);

        map.TryGetValue("FssaiLicenseNo", out var fssai);
        map.TryGetValue("Gstin", out var gstin);
        map.TryGetValue("ManagerPin", out var managerPin);
        map.TryGetValue("RestaurantName", out var restaurantName);
        map.TryGetValue("LogoUrl", out var logoUrl);
        map.TryGetValue("ClosingTime", out var closingTime);
        var safeLogoUrl = SanitizeLogoUrl(logoUrl);

        return Ok(new
        {
            restaurantName = restaurantName ?? "RestoBill",
            logoUrl = safeLogoUrl,
            fssai = fssai ?? string.Empty,
            gstin = gstin ?? string.Empty,
            managerPin = managerPin ?? string.Empty,
            closingTime = string.IsNullOrWhiteSpace(closingTime) ? "02:00" : closingTime
        });
    }

    [HttpPost("/settings/save")]
    public async Task<IActionResult> Save([FromBody] SettingsPayloadDto payload, CancellationToken cancellationToken)
    {
        var closingTime = NormalizeClosingTime(payload.ClosingTime);
        if (closingTime is null)
        {
            return BadRequest("ClosingTime must be in HH:mm format.");
        }

        await Upsert("RestaurantName", payload.RestaurantName, cancellationToken);
        await Upsert("LogoUrl", SanitizeLogoUrl(payload.LogoUrl), cancellationToken);
        await Upsert("FssaiLicenseNo", payload.Fssai, cancellationToken);
        await Upsert("Gstin", payload.Gstin, cancellationToken);
        await Upsert("ManagerPin", payload.ManagerPin, cancellationToken);
        await Upsert("ClosingTime", closingTime, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Saved" });
    }

    [HttpPost("/settings/upload-logo")]
    public async Task<IActionResult> UploadLogo(IFormFile? file, CancellationToken cancellationToken)
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
        await Upsert("LogoUrl", logoUrl, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { logoUrl });
    }

    [HttpPost("/settings/run-expiry-now")]
    public async Task<IActionResult> RunExpiryNow(CancellationToken cancellationToken)
    {
        await perishableStockExpiryJob.Execute(cancellationToken);
        return Ok(new { status = "Triggered" });
    }

    private async Task Upsert(string key, string value, CancellationToken cancellationToken)
    {
        var row = await db.RestaurantSettings.FirstOrDefaultAsync(x => x.SettingKey == key, cancellationToken);
        if (row is null)
        {
            db.RestaurantSettings.Add(new RestaurantSetting
            {
                SettingKey = key,
                SettingValue = value ?? string.Empty
            });
            return;
        }

        row.SettingValue = value ?? string.Empty;
    }

    private static string SanitizeLogoUrl(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        // Block malformed placeholders and unsafe characters.
        if (raw.Contains("${", StringComparison.Ordinal) ||
            raw.Contains('}') ||
            raw.Contains('\n') ||
            raw.Contains('\r') ||
            raw.Contains('"') ||
            raw.Contains('\''))
        {
            return string.Empty;
        }

        // Allow uploaded/static app paths.
        if (raw.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) ||
            raw.StartsWith("/images/", StringComparison.OrdinalIgnoreCase))
        {
            return raw;
        }

        // Allow absolute web URLs only for common image types.
        if (Uri.TryCreate(raw, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            var ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
            if (ext is ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" or ".svg")
            {
                return raw;
            }
        }

        return string.Empty;
    }

    private static string? NormalizeClosingTime(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return TimeOnly.TryParse(raw, out var parsed) ? parsed.ToString("HH:mm") : null;
    }

    public sealed record SettingsPayloadDto(
        string RestaurantName,
        string LogoUrl,
        string Fssai,
        string Gstin,
        string ManagerPin,
        string ClosingTime);
}

