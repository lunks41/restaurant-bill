using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Entities.Configuration;
using Entities.Masters;
using Data.Persistence;
using RestaurantBilling.Models.Masters;

namespace RestaurantBilling.Controllers;

[Authorize]
[Route("masters")]
[Route("master")]
public class MastersController(AppDbContext db, IWebHostEnvironment env) : Controller
{
    [HttpGet("categories")]
    [HttpGet("ctaeorgies")]
    [HttpGet("stockcateogries")]
    public async Task<IActionResult> Categories([FromQuery] int? editId, CancellationToken cancellationToken)
    {
        var categories = await db.Categories
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CategoryName)
            .ToListAsync(cancellationToken);
        ViewBag.Rows = categories;
        ViewBag.UseDataTables = true;

        if (editId.HasValue)
        {
            var existing = await db.Categories.FirstOrDefaultAsync(x => x.CategoryId == editId.Value, cancellationToken);
            if (existing is not null)
            {
                return View(new CategoryInputModel
                {
                    CategoryId = existing.CategoryId,
                    CategoryName = existing.CategoryName,
                    SortOrder = existing.SortOrder
                });
            }
        }

        return View(new CategoryInputModel());
    }

    [HttpPost("categories")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Categories(CategoryInputModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await Categories(editId: null, cancellationToken);
        }

        if (model.CategoryId.HasValue && model.CategoryId.Value > 0)
        {
            var existing = await db.Categories.FirstOrDefaultAsync(x => x.CategoryId == model.CategoryId.Value, cancellationToken);
            if (existing is not null)
            {
                existing.CategoryName = model.CategoryName.Trim();
                existing.SortOrder = model.SortOrder;
                existing.IsActive = true;
            }
        }
        else
        {
            var outletId = await db.Outlets.Select(x => x.OutletId).FirstAsync(cancellationToken);
            db.Categories.Add(new Category
            {
                OutletId = outletId,
                CategoryName = model.CategoryName.Trim(),
                SortOrder = model.SortOrder
            });
        }
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost("categories/{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken cancellationToken)
    {
        var category = await db.Categories.FirstOrDefaultAsync(x => x.CategoryId == id, cancellationToken);
        if (category is not null)
        {
            category.IsActive = false;
            category.IsDeleted = false;
            category.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        return RedirectToAction(nameof(Categories));
    }

    [HttpGet("items")]
    public async Task<IActionResult> Items([FromQuery] int? editId, CancellationToken cancellationToken)
    {
        var rows = await db.Items
            .Join(db.Categories, i => i.CategoryId, c => c.CategoryId, (i, c) => new { i, CategoryName = c.CategoryName })
            .OrderBy(x => x.i.ItemName)
            .ToListAsync(cancellationToken);
        ViewBag.Rows = rows;
        ViewBag.Categories = await db.Categories.Where(x => x.IsActive && !x.IsDeleted).OrderBy(x => x.CategoryName).ToListAsync(cancellationToken);
        ViewBag.UseDataTables = true;

        if (editId.HasValue)
        {
            var item = await db.Items.FirstOrDefaultAsync(x => x.ItemId == editId.Value, cancellationToken);
            if (item is not null)
            {
                return View(new ItemInputModel
                {
                    ItemId = item.ItemId,
                    CategoryId = item.CategoryId,
                    ItemCode = item.ItemCode,
                    ItemName = item.ItemName,
                    SalePrice = item.SalePrice,
                    PurchasePrice = item.PurchasePrice,
                    GstPercent = item.GstPercent,
                    IsTaxInclusive = item.IsTaxInclusive,
                    TaxType = item.TaxType,
                    SacCode = item.SacCode,
                    IsStockTracked = item.IsStockTracked,
                    ReorderLevel = item.ReorderLevel
                });
            }
        }

        return View(new ItemInputModel());
    }

    [HttpPost("items")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Items(ItemInputModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await Items(editId: null, cancellationToken);
        }

        var uploadedImagePath = await SaveItemImageAsync(model.ImageFile, cancellationToken);

        if (model.ItemId.HasValue && model.ItemId.Value > 0)
        {
            var existing = await db.Items.FirstOrDefaultAsync(x => x.ItemId == model.ItemId.Value, cancellationToken);
            if (existing is not null)
            {
                existing.CategoryId = model.CategoryId;
                existing.ItemCode = model.ItemCode.Trim();
                existing.ItemName = model.ItemName.Trim();
                existing.SalePrice = model.SalePrice;
                existing.PurchasePrice = model.PurchasePrice;
                existing.GstPercent = model.GstPercent;
                existing.IsTaxInclusive = model.IsTaxInclusive;
                existing.TaxType = model.TaxType;
                existing.SacCode = model.SacCode.Trim();
                existing.IsStockTracked = model.IsStockTracked;
                existing.ReorderLevel = model.ReorderLevel;
                existing.IsActive = true;
                if (!string.IsNullOrWhiteSpace(uploadedImagePath))
                {
                    TryDeleteItemImage(existing.ImagePath);
                    existing.ImagePath = uploadedImagePath;
                }
                else if (!string.IsNullOrWhiteSpace(model.ImagePath))
                {
                    existing.ImagePath = model.ImagePath;
                }
            }
        }
        else
        {
            var outletId = await db.Outlets.Select(x => x.OutletId).FirstAsync(cancellationToken);
            db.Items.Add(new Item
            {
                OutletId = outletId,
                CategoryId = model.CategoryId,
                ItemCode = model.ItemCode.Trim(),
                ItemName = model.ItemName.Trim(),
                SalePrice = model.SalePrice,
                PurchasePrice = model.PurchasePrice,
                GstPercent = model.GstPercent,
                IsTaxInclusive = model.IsTaxInclusive,
                TaxType = model.TaxType,
                SacCode = model.SacCode.Trim(),
                IsStockTracked = model.IsStockTracked,
                ReorderLevel = model.ReorderLevel,
                ImagePath = uploadedImagePath
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Items));
    }

    [HttpPost("items/{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteItem(int id, CancellationToken cancellationToken)
    {
        var item = await db.Items.FirstOrDefaultAsync(x => x.ItemId == id, cancellationToken);
        if (item is not null)
        {
            item.IsActive = false;
            item.IsDeleted = false;
            item.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        return RedirectToAction(nameof(Items));
    }

    private async Task<string?> SaveItemImageAsync(IFormFile? imageFile, CancellationToken cancellationToken)
    {
        if (imageFile is null || imageFile.Length == 0) return null;

        var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        if (!allowed.Contains(ext)) return null;

        var webRoot = env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var relativeDir = Path.Combine("images", "menu", "items");
        var targetDir = Path.Combine(webRoot, relativeDir);
        Directory.CreateDirectory(targetDir);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(targetDir, fileName);
        await using var stream = new FileStream(fullPath, FileMode.Create);
        await imageFile.CopyToAsync(stream, cancellationToken);
        return "/" + Path.Combine(relativeDir, fileName).Replace("\\", "/");
    }

    private void TryDeleteItemImage(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !imagePath.StartsWith("/images/", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            var webRoot = env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var fullPath = Path.Combine(webRoot, imagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
        }
        catch
        {
            // keep old image if cleanup fails
        }
    }

    [HttpGet("taxes")]
    public async Task<IActionResult> Taxes([FromQuery] int? editId, CancellationToken cancellationToken)
    {
        var rows = await db.TaxConfigurations
            .OrderByDescending(x => x.EffectiveFrom)
            .ToListAsync(cancellationToken);
        ViewBag.Rows = rows;

        if (editId.HasValue)
        {
            var existing = await db.TaxConfigurations.FirstOrDefaultAsync(x => x.TaxConfigurationId == editId.Value, cancellationToken);
            if (existing is not null)
            {
                return View(new TaxConfigurationInputModel
                {
                    TaxConfigurationId = existing.TaxConfigurationId,
                    ScenarioType = existing.ScenarioType,
                    TotalGstPercent = existing.TotalGstPercent,
                    CgstPercent = existing.CgstPercent,
                    SgstPercent = existing.SgstPercent,
                    IgstPercent = existing.IgstPercent,
                    IsItcAllowed = existing.IsItcAllowed,
                    EffectiveFrom = existing.EffectiveFrom
                });
            }
        }

        return View(new TaxConfigurationInputModel());
    }

    [HttpPost("taxes")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Taxes(TaxConfigurationInputModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await Taxes(editId: null, cancellationToken);
        }

        if (model.TaxConfigurationId.HasValue && model.TaxConfigurationId.Value > 0)
        {
            var existing = await db.TaxConfigurations.FirstOrDefaultAsync(x => x.TaxConfigurationId == model.TaxConfigurationId.Value, cancellationToken);
            if (existing is not null)
            {
                existing.ScenarioType = model.ScenarioType.Trim();
                existing.TotalGstPercent = model.TotalGstPercent;
                existing.CgstPercent = model.CgstPercent;
                existing.SgstPercent = model.SgstPercent;
                existing.IgstPercent = model.IgstPercent;
                existing.IsItcAllowed = model.IsItcAllowed;
                existing.EffectiveFrom = model.EffectiveFrom;
            }
        }
        else
        {
            var outletId = await db.Outlets.Select(x => x.OutletId).FirstAsync(cancellationToken);
            db.TaxConfigurations.Add(new TaxConfiguration
            {
                OutletId = outletId,
                ScenarioType = model.ScenarioType.Trim(),
                TotalGstPercent = model.TotalGstPercent,
                CgstPercent = model.CgstPercent,
                SgstPercent = model.SgstPercent,
                IgstPercent = model.IgstPercent,
                IsItcAllowed = model.IsItcAllowed,
                EffectiveFrom = model.EffectiveFrom
            });
        }
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Taxes));
    }

    [HttpPost("taxes/{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTax(int id, CancellationToken cancellationToken)
    {
        var row = await db.TaxConfigurations.FirstOrDefaultAsync(x => x.TaxConfigurationId == id, cancellationToken);
        if (row is not null)
        {
            row.IsDeleted = true;
            row.DeletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        return RedirectToAction(nameof(Taxes));
    }

    [HttpGet("printers")]
    public IActionResult Printers()
    {
        ViewBag.UseDataTables = true;
        return View();
    }

    [HttpGet("tables")]
    [HttpGet("table")]
    public IActionResult Tables()
    {
        ViewBag.UseDataTables = true;
        return View();
    }

    [HttpGet("units")]
    [HttpGet("stock")]
    public IActionResult Units()
    {
        ViewBag.UseDataTables = true;
        return View();
    }

    [HttpGet("units-data")]
    public async Task<IActionResult> UnitsData([FromQuery] int outletId, CancellationToken cancellationToken)
    {
        var rows = await db.Units
            .Where(x => x.OutletId == outletId)
            .OrderBy(x => x.UnitName)
            .Select(x => new { x.UnitId, x.UnitName, x.UnitCode, x.IsActive })
            .ToListAsync(cancellationToken);
        return Ok(rows);
    }

    [HttpPost("units-create")]
    public async Task<IActionResult> CreateUnit([FromBody] MasterInputDto request, CancellationToken cancellationToken)
    {
        db.Units.Add(new Unit
        {
            OutletId = 1,
            UnitName = request.Name.Trim(),
            UnitCode = request.Code?.Trim() ?? request.Name[..Math.Min(3, request.Name.Length)].ToUpperInvariant(),
            IsActive = true,
            IsDeleted = false
        });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Created" });
    }

    [HttpPost("units-update/{id:int}")]
    public async Task<IActionResult> UpdateUnit(int id, [FromBody] MasterInputDto request, CancellationToken cancellationToken)
    {
        var unit = await db.Units.FirstOrDefaultAsync(x => x.UnitId == id, cancellationToken);
        if (unit is null) return NotFound();
        unit.UnitName = request.Name.Trim();
        unit.UnitCode = request.Code?.Trim() ?? unit.UnitCode;
        unit.IsActive = true;
        unit.IsDeleted = false;
        unit.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Updated" });
    }

    [HttpPost("units-delete/{id:int}")]
    public async Task<IActionResult> DeleteUnit(int id, CancellationToken cancellationToken)
    {
        var unit = await db.Units.FirstOrDefaultAsync(x => x.UnitId == id, cancellationToken);
        if (unit is null) return NotFound();
        unit.IsActive = false;
        unit.IsDeleted = false;
        unit.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Inactivated" });
    }

    [HttpGet("tables-data")]
    public async Task<IActionResult> TablesData([FromQuery] int outletId, CancellationToken cancellationToken)
    {
        var rows = await db.TableMasters
            .Where(x => x.OutletId == outletId && x.IsActive)
            .OrderBy(x => x.TableName)
            .Select(x => new
            {
                x.TableMasterId,
                x.TableName,
                Area = x.Area == null || x.Area == "" ? "Ground" : x.Area,
                x.Capacity,
                x.IsOccupied,
                x.IsActive
            })
            .ToListAsync(cancellationToken);
        return Ok(rows);
    }

    [HttpPost("tables-create")]
    public async Task<IActionResult> CreateTable([FromBody] MasterInputDto request, CancellationToken cancellationToken)
    {
        db.TableMasters.Add(new TableMaster
        {
            OutletId = 1,
            TableName = request.Name.Trim(),
            Area = NormalizeTableArea(request.Area),
            Capacity = request.Capacity ?? 2,
            IsActive = true,
            IsDeleted = false
        });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Created" });
    }

    [HttpPost("tables-update/{id:int}")]
    public async Task<IActionResult> UpdateTable(int id, [FromBody] MasterInputDto request, CancellationToken cancellationToken)
    {
        var row = await db.TableMasters.FirstOrDefaultAsync(x => x.TableMasterId == id, cancellationToken);
        if (row is null) return NotFound();
        row.TableName = request.Name.Trim();
        row.Area = NormalizeTableArea(request.Area);
        row.Capacity = request.Capacity ?? row.Capacity;
        row.IsActive = true;
        row.IsDeleted = false;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Updated" });
    }

    [HttpPost("tables-delete/{id:int}")]
    public async Task<IActionResult> DeleteTable(int id, CancellationToken cancellationToken)
    {
        var row = await db.TableMasters.FirstOrDefaultAsync(x => x.TableMasterId == id, cancellationToken);
        if (row is null) return NotFound();
        row.IsActive = false;
        row.IsDeleted = false;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Inactivated" });
    }

    private static string NormalizeTableArea(string? area)
    {
        var normalized = (area ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "ground" => "Ground",
            "ac" => "AC",
            "non-ac" => "Non-AC",
            "outdoor" => "Outdoor",
            _ => "Ground"
        };
    }

    [HttpGet("printers-data")]
    public async Task<IActionResult> PrintersData([FromQuery] int outletId, CancellationToken cancellationToken)
    {
        var rows = await db.PrinterProfiles
            .Where(x => x.OutletId == outletId)
            .OrderBy(x => x.PrinterName)
            .Select(x => new { x.PrinterProfileId, x.PrinterName, x.PrinterType, x.DevicePath, x.IsDefault, x.IsActive })
            .ToListAsync(cancellationToken);
        return Ok(rows);
    }

    [HttpPost("printers-create")]
    public async Task<IActionResult> CreatePrinter([FromBody] MasterInputDto request, CancellationToken cancellationToken)
    {
        db.PrinterProfiles.Add(new PrinterProfile
        {
            OutletId = 1,
            PrinterName = request.Name.Trim(),
            PrinterType = request.PrinterType?.Trim() ?? "Thermal",
            DevicePath = request.DevicePath?.Trim(),
            IsDefault = request.IsDefault ?? false,
            IsActive = true,
            IsDeleted = false
        });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Created" });
    }

    [HttpPost("printers-update/{id:int}")]
    public async Task<IActionResult> UpdatePrinter(int id, [FromBody] MasterInputDto request, CancellationToken cancellationToken)
    {
        var row = await db.PrinterProfiles.FirstOrDefaultAsync(x => x.PrinterProfileId == id, cancellationToken);
        if (row is null) return NotFound();
        row.PrinterName = request.Name.Trim();
        row.PrinterType = request.PrinterType?.Trim() ?? row.PrinterType;
        row.DevicePath = request.DevicePath?.Trim();
        row.IsDefault = request.IsDefault ?? row.IsDefault;
        row.IsActive = true;
        row.IsDeleted = false;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Updated" });
    }

    [HttpPost("printers-delete/{id:int}")]
    public async Task<IActionResult> DeletePrinter(int id, CancellationToken cancellationToken)
    {
        var row = await db.PrinterProfiles.FirstOrDefaultAsync(x => x.PrinterProfileId == id, cancellationToken);
        if (row is null) return NotFound();
        row.IsActive = false;
        row.IsDeleted = false;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Inactivated" });
    }

    public sealed record MasterInputDto(
        string Name,
        string? Code,
        int? Capacity,
        string? Area,
        string? Phone,
        string? Gstin,
        string? PrinterType,
        string? DevicePath,
        bool? IsDefault);
}

