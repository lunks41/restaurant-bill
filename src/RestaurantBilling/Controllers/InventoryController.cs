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
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize switch
        {
            < 1 => 25,
            > 100 => 100,
            _ => pageSize
        };

        var stockCategoryId = await db.Categories
            .Where(x => x.OutletId == outletId && x.CategoryName == "Stock Items")
            .Select(x => x.CategoryId)
            .FirstOrDefaultAsync(cancellationToken);

        var query = BuildStockItemsQuery(outletId, stockCategoryId > 0 ? stockCategoryId : null, search, unitId);
        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        if (totalPages > 0 && page > totalPages) page = totalPages;

        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            Items = rows,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        });
    }

    [HttpGet("stock-items-export")]
    public async Task<IActionResult> StockItemsExport(
        [FromQuery] int outletId,
        [FromQuery] string? search,
        [FromQuery] int? unitId,
        CancellationToken cancellationToken)
    {
        var stockCategoryId = await db.Categories
            .Where(x => x.OutletId == outletId && x.CategoryName == "Stock Items")
            .Select(x => x.CategoryId)
            .FirstOrDefaultAsync(cancellationToken);
        var rows = await BuildStockItemsQuery(outletId, stockCategoryId > 0 ? stockCategoryId : null, search, unitId).ToListAsync(cancellationToken);
        var csv = new StringBuilder("Code,Name,Unit,PurchaseRate,CurrentQty,ReorderLevel,Status\n");
        foreach (var row in rows)
        {
            var status = row.IsActive ? "Active" : "Inactive";
            csv.AppendLine($"{Escape(row.ItemCode ?? string.Empty)},{Escape(row.ItemName)},{Escape(row.UnitName ?? string.Empty)},{row.PurchasePrice},{row.CurrentQty},{row.ReorderLevel},{status}");
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
            GstPercent = 0m,
            IsTaxInclusive = false,
            TaxType = TaxType.GST,
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
        item.SalePrice = request.PurchasePrice ?? item.SalePrice;
        item.UpdatedAtUtc = DateTime.UtcNow;

        var stock = await db.StockItems.FirstOrDefaultAsync(x => x.ItemId == id && x.OutletId == item.OutletId, cancellationToken);
        if (stock is null)
        {
            stock = new StockItem
            {
                OutletId = item.OutletId,
                ItemId = item.ItemId,
                CurrentQty = request.CurrentQty ?? (request.ReorderLevel ?? 0m),
                ReorderLevel = request.ReorderLevel ?? 0m,
                IsActive = true,
                IsDeleted = false
            };
            db.StockItems.Add(stock);
        }
        else
        {
            stock.CurrentQty = request.CurrentQty ?? stock.CurrentQty;
            stock.ReorderLevel = request.ReorderLevel ?? stock.ReorderLevel;
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

    private IQueryable<StockItemListRow> BuildStockItemsQuery(int outletId, int? stockCategoryId, string? search, int? unitId)
    {
        var query =
            from item in db.Items
            where item.OutletId == outletId
                  && !item.IsDeleted
                  && (
                      (stockCategoryId.HasValue && item.CategoryId == stockCategoryId.Value)
                      || item.ItemCode.StartsWith("STK")
                      || db.StockItems.Any(s => s.OutletId == outletId && s.ItemId == item.ItemId && !s.IsDeleted)
                  )
            join stock in db.StockItems.Where(s => s.OutletId == outletId && !s.IsDeleted) on item.ItemId equals stock.ItemId into stockJoin
            from stock in stockJoin.DefaultIfEmpty()
            join unit in db.Units.Where(u => u.OutletId == outletId && !u.IsDeleted) on item.UnitId equals (int?)unit.UnitId into unitJoin
            from unit in unitJoin.DefaultIfEmpty()
            select new StockItemListRow
            {
                ItemId = item.ItemId,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                PurchasePrice = item.SalePrice,
                ReorderLevel = stock == null ? 0m : stock.ReorderLevel,
                IsActive = item.IsActive,
                UnitId = item.UnitId,
                UnitName = unit == null ? string.Empty : unit.UnitName,
                CurrentQty = stock == null ? 0m : stock.CurrentQty
            };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.Like(x.ItemName, term) ||
                EF.Functions.Like(x.ItemCode ?? string.Empty, term));
        }

        if (unitId.HasValue && unitId.Value > 0)
        {
            query = query.Where(x => x.UnitId == unitId.Value);
        }

        return query.OrderBy(x => x.ItemName);
    }

    private sealed class StockItemListRow
    {
        public int ItemId { get; set; }
        public string? ItemCode { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public decimal PurchasePrice { get; set; }
        public decimal ReorderLevel { get; set; }
        public bool IsActive { get; set; }
        public int? UnitId { get; set; }
        public string? UnitName { get; set; }
        public decimal CurrentQty { get; set; }
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
