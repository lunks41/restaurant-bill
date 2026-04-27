using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Entities.Masters;
using Data.Persistence;
using RestaurantBilling.Models.Masters;

namespace RestaurantBilling.Controllers;

[Authorize]
[Route("masters")]
[Route("master")]
public class MastersController(AppDbContext db, IWebHostEnvironment env) : Controller
{
    [HttpGet("menu-categories")]
    public IActionResult MenuCategories()
    {
        ViewData["Title"] = "Menu & Categories";
        return View();
    }

    [HttpGet("groceries-units")]
    public IActionResult GroceriesUnits()
    {
        ViewData["Title"] = "Groceries & Unit";
        return View();
    }

    [HttpGet("categories")]
    public async Task<IActionResult> Categories([FromQuery] int? editId, [FromQuery] bool embed, CancellationToken cancellationToken)
    {
        var categories = await db.Categories
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CategoryName)
            .ToListAsync(cancellationToken);
        ViewBag.Rows = categories;
        ViewBag.UseDataTables = true;
        ViewData["EmbedMode"] = embed;

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
            return await Categories(editId: null, embed: false, cancellationToken);
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
    public async Task<IActionResult> Items([FromQuery] int? editId, [FromQuery] bool embed, CancellationToken cancellationToken)
    {
        var rows = await db.Items
            .Join(db.Categories, i => i.CategoryId, c => c.CategoryId, (i, c) => new { i, CategoryName = c.CategoryName })
            .OrderBy(x => x.i.ItemName)
            .ToListAsync(cancellationToken);
        ViewBag.Rows = rows;
        ViewBag.Categories = await db.Categories.Where(x => x.IsActive && !x.IsDeleted).OrderBy(x => x.CategoryName).ToListAsync(cancellationToken);
        ViewBag.UseDataTables = true;
        ViewData["EmbedMode"] = embed;

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
                    GstPercent = item.GstPercent,
                    IsTaxInclusive = item.IsTaxInclusive,
                    TaxType = item.TaxType
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
            return await Items(editId: null, embed: false, cancellationToken);
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
                existing.GstPercent = model.GstPercent;
                existing.IsTaxInclusive = model.IsTaxInclusive;
                existing.TaxType = model.TaxType;
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
                GstPercent = model.GstPercent,
                IsTaxInclusive = model.IsTaxInclusive,
                TaxType = model.TaxType,
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

    [HttpGet("tables")]
    [HttpGet("table")]
    public IActionResult Tables()
    {
        ViewBag.UseDataTables = true;
        return View();
    }

    [HttpGet("units")]
    public IActionResult Units([FromQuery] bool embed)
    {
        ViewBag.UseDataTables = true;
        ViewData["EmbedMode"] = embed;
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

