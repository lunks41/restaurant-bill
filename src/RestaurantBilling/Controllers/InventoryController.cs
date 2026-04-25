using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Data.Persistence;
using Entities.Enums;
using Entities.Inventory;
using Entities.Masters;

namespace RestaurantBilling.Controllers;

[Authorize]
[Route("master")]
[Route("masters")]
public class InventoryController(AppDbContext db) : Controller
{
    [HttpGet("stock")]
    public IActionResult Stock()
    {
        ViewBag.UseDataTables = true;
        return View();
    }

    [HttpGet("stock-items-data")]
    public async Task<IActionResult> StockItemsData(
        [FromQuery] int outletId,
        [FromQuery] string? search,
        [FromQuery] int? unitId,
        CancellationToken cancellationToken)
    {
        var stockCategoryId = await db.Categories
            .Where(x => x.OutletId == outletId && x.CategoryName == "Stock Items")
            .Select(x => x.CategoryId)
            .FirstOrDefaultAsync(cancellationToken);

        if (stockCategoryId <= 0)
        {
            return Ok(Array.Empty<object>());
        }

        var rows = await db.Items
            .Where(x => x.OutletId == outletId && x.CategoryId == stockCategoryId)
            .GroupJoin(
                db.StockItems.Where(s => s.OutletId == outletId),
                i => i.ItemId,
                s => s.ItemId,
                (i, s) => new { Item = i, Stock = s.FirstOrDefault() })
            .GroupJoin(
                db.Units.Where(u => u.OutletId == outletId),
                x => x.Item.UnitId,
                u => (int?)u.UnitId,
                (x, u) => new { x.Item, x.Stock, Unit = u.FirstOrDefault() })
            .OrderBy(x => x.Item.ItemName)
            .Select(x => new
            {
                x.Item.ItemId,
                x.Item.ItemCode,
                x.Item.ItemName,
                x.Item.PurchasePrice,
                x.Item.ReorderLevel,
                x.Item.IsActive,
                UnitId = x.Item.UnitId,
                UnitName = x.Unit == null ? "" : x.Unit.UnitName,
                CurrentQty = x.Stock == null ? 0m : x.Stock.CurrentQty
            })
            .ToListAsync(cancellationToken);

        var filtered = rows.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            filtered = filtered.Where(x =>
                (x.ItemName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.ItemCode?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        if (unitId.HasValue && unitId.Value > 0)
        {
            filtered = filtered.Where(x => x.UnitId == unitId.Value);
        }

        return Ok(filtered);
    }

    [HttpGet("stock-items-export")]
    public async Task<IActionResult> StockItemsExport(
        [FromQuery] int outletId,
        [FromQuery] string? search,
        [FromQuery] int? unitId,
        CancellationToken cancellationToken)
    {
        var dataResult = await StockItemsData(outletId, search, unitId, cancellationToken);
        if (dataResult is not OkObjectResult ok || ok.Value is null)
        {
            return BadRequest("Unable to export stock items.");
        }

        var rows = ((IEnumerable<object>)ok.Value).ToList();
        var csv = new StringBuilder("Code,Name,Unit,PurchaseRate,CurrentQty,ReorderLevel,Status\n");
        foreach (var row in rows)
        {
            var props = row.GetType().GetProperties();
            string Get(string key) => props.FirstOrDefault(p => p.Name == key)?.GetValue(row)?.ToString() ?? string.Empty;
            var status = Get("IsActive") == "True" ? "Active" : "Inactive";
            csv.AppendLine($"{Escape(Get("ItemCode"))},{Escape(Get("ItemName"))},{Escape(Get("UnitName"))},{Get("PurchasePrice")},{Get("CurrentQty")},{Get("ReorderLevel")},{status}");
        }
        var fileName = $"stock-items-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
    }

    [HttpGet("stock-units-data")]
    public async Task<IActionResult> StockUnitsData([FromQuery] int outletId, CancellationToken cancellationToken)
    {
        var units = await db.Units
            .Where(x => x.OutletId == outletId && x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.UnitName)
            .Select(x => new { x.UnitId, x.UnitName, x.UnitCode })
            .ToListAsync(cancellationToken);
        return Ok(units);
    }

    [HttpPost("stock-items-create")]
    public async Task<IActionResult> CreateStockItem([FromBody] StockItemInputDto request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return BadRequest("Name is required.");
        }

        var outletId = request.OutletId ?? 1;
        var stockCategoryId = await EnsureStockCategoryAsync(outletId, cancellationToken);
        var itemCode = (request.Code?.Trim() ?? string.Empty).Length > 0 ? request.Code!.Trim() : await GenerateNextStockCodeAsync(outletId, cancellationToken);
        var reorderLevel = request.ReorderLevel ?? 0m;
        var openingQty = request.CurrentQty ?? reorderLevel;
        var purchaseRate = request.PurchasePrice ?? 0m;

        var item = new Item
        {
            OutletId = outletId,
            CategoryId = stockCategoryId,
            UnitId = request.UnitId,
            ItemCode = itemCode,
            ItemName = name,
            SalePrice = purchaseRate,
            PurchasePrice = purchaseRate,
            GstPercent = 0m,
            IsTaxInclusive = false,
            TaxType = TaxType.GST,
            SacCode = "996331",
            IsStockTracked = true,
            ReorderLevel = reorderLevel,
            IsActive = true,
            IsDeleted = false
        };
        db.Items.Add(item);
        await db.SaveChangesAsync(cancellationToken);

        db.StockItems.Add(new StockItem
        {
            OutletId = outletId,
            ItemId = item.ItemId,
            CurrentQty = openingQty,
            ReorderLevel = reorderLevel,
            IsActive = true,
            IsDeleted = false
        });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Created" });
    }

    [HttpPost("stock-items-update/{id:int}")]
    public async Task<IActionResult> UpdateStockItem(int id, [FromBody] StockItemInputDto request, CancellationToken cancellationToken)
    {
        var item = await db.Items.FirstOrDefaultAsync(x => x.ItemId == id, cancellationToken);
        if (item is null) return NotFound();

        item.ItemCode = request.Code?.Trim() ?? item.ItemCode;
        item.ItemName = request.Name?.Trim() ?? item.ItemName;
        item.UnitId = request.UnitId;
        item.PurchasePrice = request.PurchasePrice ?? item.PurchasePrice;
        item.SalePrice = item.PurchasePrice;
        item.ReorderLevel = request.ReorderLevel ?? item.ReorderLevel;
        item.UpdatedAtUtc = DateTime.UtcNow;

        var stock = await db.StockItems.FirstOrDefaultAsync(x => x.ItemId == id && x.OutletId == item.OutletId, cancellationToken);
        if (stock is null)
        {
            stock = new StockItem
            {
                OutletId = item.OutletId,
                ItemId = item.ItemId,
                CurrentQty = request.CurrentQty ?? item.ReorderLevel,
                ReorderLevel = item.ReorderLevel,
                IsActive = true,
                IsDeleted = false
            };
            db.StockItems.Add(stock);
        }
        else
        {
            stock.CurrentQty = request.CurrentQty ?? stock.CurrentQty;
            stock.ReorderLevel = item.ReorderLevel;
            stock.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Updated" });
    }

    [HttpPost("stock-items-delete/{id:int}")]
    public async Task<IActionResult> DeleteStockItem(int id, CancellationToken cancellationToken)
    {
        var item = await db.Items.FirstOrDefaultAsync(x => x.ItemId == id, cancellationToken);
        if (item is null) return NotFound();

        item.IsActive = false;
        item.IsDeleted = true;
        item.UpdatedAtUtc = DateTime.UtcNow;

        var stock = await db.StockItems.FirstOrDefaultAsync(x => x.ItemId == id && x.OutletId == item.OutletId, cancellationToken);
        if (stock is not null)
        {
            stock.IsActive = false;
            stock.IsDeleted = true;
            stock.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Deleted" });
    }

    private async Task<int> EnsureStockCategoryAsync(int outletId, CancellationToken cancellationToken)
    {
        var category = await db.Categories
            .FirstOrDefaultAsync(x => x.OutletId == outletId && x.CategoryName == "Stock Items", cancellationToken);
        if (category is not null)
        {
            return category.CategoryId;
        }

        var sortOrder = await db.Categories.Where(x => x.OutletId == outletId).Select(x => x.SortOrder).DefaultIfEmpty(0).MaxAsync(cancellationToken);
        var created = new Category
        {
            OutletId = outletId,
            CategoryName = "Stock Items",
            SortOrder = sortOrder + 1,
            IsActive = false,
            IsDeleted = false
        };
        db.Categories.Add(created);
        await db.SaveChangesAsync(cancellationToken);
        return created.CategoryId;
    }

    private async Task<string> GenerateNextStockCodeAsync(int outletId, CancellationToken cancellationToken)
    {
        var existing = await db.Items
            .Where(x => x.OutletId == outletId && x.ItemCode.StartsWith("STK"))
            .Select(x => x.ItemCode)
            .ToListAsync(cancellationToken);

        var max = existing
            .Select(code => int.TryParse(code[3..], out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"STK{(max + 1):000}";
    }

    public sealed record StockItemInputDto(
        int? OutletId,
        string? Code,
        string? Name,
        int? UnitId,
        decimal? PurchasePrice,
        decimal? ReorderLevel,
        decimal? CurrentQty);

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
