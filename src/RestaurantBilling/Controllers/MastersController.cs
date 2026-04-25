using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Entities.Configuration;
using Entities.Masters;
using Data.Persistence;
using RestaurantBilling.Models.Masters;

namespace RestaurantBilling.Controllers;

[Authorize]
[Route("masters")]
public class MastersController(AppDbContext db) : Controller
{
    [HttpGet("categories")]
    public async Task<IActionResult> Categories([FromQuery] int? editId, CancellationToken cancellationToken)
    {
        var categories = await db.Categories
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CategoryName)
            .ToListAsync(cancellationToken);
        ViewBag.Rows = categories;

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
            category.IsDeleted = true;
            category.DeletedAtUtc = DateTime.UtcNow;
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
        ViewBag.Categories = await db.Categories.OrderBy(x => x.CategoryName).ToListAsync(cancellationToken);

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
                ReorderLevel = model.ReorderLevel
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
            item.IsDeleted = true;
            item.DeletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        return RedirectToAction(nameof(Items));
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
    public IActionResult Tables()
    {
        ViewBag.UseDataTables = true;
        return View();
    }

    [HttpGet("customers")]
    public IActionResult Customers()
    {
        ViewBag.UseDataTables = true;
        return View();
    }

    [HttpGet("suppliers")]
    public IActionResult Suppliers()
    {
        ViewBag.UseDataTables = true;
        return View();
    }

    [HttpGet("units")]
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
            .Select(x => new { x.UnitId, x.UnitName, x.UnitCode })
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
            UnitCode = request.Code?.Trim() ?? request.Name[..Math.Min(3, request.Name.Length)].ToUpperInvariant()
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
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Updated" });
    }

    [HttpPost("units-delete/{id:int}")]
    public async Task<IActionResult> DeleteUnit(int id, CancellationToken cancellationToken)
    {
        var unit = await db.Units.FirstOrDefaultAsync(x => x.UnitId == id, cancellationToken);
        if (unit is null) return NotFound();
        unit.IsDeleted = true;
        unit.DeletedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Deleted" });
    }

    [HttpGet("tables-data")]
    public async Task<IActionResult> TablesData([FromQuery] int outletId, CancellationToken cancellationToken)
    {
        var rows = await db.TableMasters
            .Where(x => x.OutletId == outletId)
            .OrderBy(x => x.TableName)
            .Select(x => new { x.TableMasterId, x.TableName, x.Capacity, x.IsOccupied })
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
            Capacity = request.Capacity ?? 2
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
        row.Capacity = request.Capacity ?? row.Capacity;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Updated" });
    }

    [HttpPost("tables-delete/{id:int}")]
    public async Task<IActionResult> DeleteTable(int id, CancellationToken cancellationToken)
    {
        var row = await db.TableMasters.FirstOrDefaultAsync(x => x.TableMasterId == id, cancellationToken);
        if (row is null) return NotFound();
        row.IsDeleted = true;
        row.DeletedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Deleted" });
    }

    [HttpGet("customers-data")]
    public async Task<IActionResult> CustomersData([FromQuery] int outletId, CancellationToken cancellationToken)
    {
        var rows = await db.Customers
            .Where(x => x.OutletId == outletId)
            .OrderBy(x => x.CustomerName)
            .Select(x => new { x.CustomerId, x.CustomerName, x.Phone, x.Gstin })
            .ToListAsync(cancellationToken);
        return Ok(rows);
    }

    [HttpPost("customers-create")]
    public async Task<IActionResult> CreateCustomer([FromBody] MasterInputDto request, CancellationToken cancellationToken)
    {
        db.Customers.Add(new Customer
        {
            OutletId = 1,
            CustomerName = request.Name.Trim(),
            Phone = request.Phone?.Trim(),
            Gstin = request.Gstin?.Trim()
        });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Created" });
    }

    [HttpPost("customers-update/{id:int}")]
    public async Task<IActionResult> UpdateCustomer(int id, [FromBody] MasterInputDto request, CancellationToken cancellationToken)
    {
        var row = await db.Customers.FirstOrDefaultAsync(x => x.CustomerId == id, cancellationToken);
        if (row is null) return NotFound();
        row.CustomerName = request.Name.Trim();
        row.Phone = request.Phone?.Trim();
        row.Gstin = request.Gstin?.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Updated" });
    }

    [HttpPost("customers-delete/{id:int}")]
    public async Task<IActionResult> DeleteCustomer(int id, CancellationToken cancellationToken)
    {
        var row = await db.Customers.FirstOrDefaultAsync(x => x.CustomerId == id, cancellationToken);
        if (row is null) return NotFound();
        row.IsDeleted = true;
        row.DeletedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Deleted" });
    }

    [HttpGet("suppliers-data")]
    public async Task<IActionResult> SuppliersData([FromQuery] int outletId, CancellationToken cancellationToken)
    {
        var rows = await db.Suppliers
            .Where(x => x.OutletId == outletId)
            .OrderBy(x => x.SupplierName)
            .Select(x => new { x.SupplierId, x.SupplierName, x.ContactNo, x.Gstin })
            .ToListAsync(cancellationToken);
        return Ok(rows);
    }

    [HttpPost("suppliers-create")]
    public async Task<IActionResult> CreateSupplier([FromBody] MasterInputDto request, CancellationToken cancellationToken)
    {
        db.Suppliers.Add(new Supplier
        {
            OutletId = 1,
            SupplierName = request.Name.Trim(),
            ContactNo = request.Phone?.Trim(),
            Gstin = request.Gstin?.Trim()
        });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Created" });
    }

    [HttpPost("suppliers-update/{id:int}")]
    public async Task<IActionResult> UpdateSupplier(int id, [FromBody] MasterInputDto request, CancellationToken cancellationToken)
    {
        var row = await db.Suppliers.FirstOrDefaultAsync(x => x.SupplierId == id, cancellationToken);
        if (row is null) return NotFound();
        row.SupplierName = request.Name.Trim();
        row.ContactNo = request.Phone?.Trim();
        row.Gstin = request.Gstin?.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Updated" });
    }

    [HttpPost("suppliers-delete/{id:int}")]
    public async Task<IActionResult> DeleteSupplier(int id, CancellationToken cancellationToken)
    {
        var row = await db.Suppliers.FirstOrDefaultAsync(x => x.SupplierId == id, cancellationToken);
        if (row is null) return NotFound();
        row.IsDeleted = true;
        row.DeletedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Deleted" });
    }

    [HttpGet("printers-data")]
    public async Task<IActionResult> PrintersData([FromQuery] int outletId, CancellationToken cancellationToken)
    {
        var rows = await db.PrinterProfiles
            .Where(x => x.OutletId == outletId)
            .OrderBy(x => x.PrinterName)
            .Select(x => new { x.PrinterProfileId, x.PrinterName, x.PrinterType, x.DevicePath, x.IsDefault })
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
            IsDefault = request.IsDefault ?? false
        });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Created" });
    }

    public sealed record MasterInputDto(
        string Name,
        string? Code,
        int? Capacity,
        string? Phone,
        string? Gstin,
        string? PrinterType,
        string? DevicePath,
        bool? IsDefault);
}

