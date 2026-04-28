using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Data.Persistence;
using Entities.Inventory;

namespace RestaurantBilling.Controllers;

[Authorize]
[Route("inventory")]
public class InventoryController(AppDbContext db) : Controller
{
    [HttpGet()]
    public IActionResult Inventory()
    {
        ViewBag.UseDataTables = true;
        return View("Stock");
    }

    [HttpGet("stock")]
    public IActionResult Stock()
    {
        return RedirectToAction(nameof(Inventory));
    }

    [HttpGet("stock-items-data")]
    public async Task<IActionResult> StockItemsData(
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

        var query = BuildItemStockQuery(search, unitId);
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
        [FromQuery] string? search,
        [FromQuery] int? unitId,
        CancellationToken cancellationToken)
    {
        var rows = await BuildItemStockQuery(search, unitId).ToListAsync(cancellationToken);
        var csv = new StringBuilder("Code,ItemId,UnitId,CurrentQty,ReorderLevel,Status\n");
        foreach (var row in rows)
        {
            var status = row.IsActive ? "Active" : "Inactive";
            csv.AppendLine($"{Escape(row.ItemCode ?? string.Empty)},{row.ItemIdRef},{(row.UnitId.HasValue ? row.UnitId.Value.ToString() : string.Empty)},{row.CurrentQty},{row.ReorderLevel},{status}");
        }
        var fileName = $"stock-items-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
    }

    [HttpGet("stock-units-data")]
    public async Task<IActionResult> StockUnitsData(CancellationToken cancellationToken)
    {
        var units = await db.Units
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.UnitName)
            .Select(x => new { x.UnitId, x.UnitName, x.UnitCode })
            .ToListAsync(cancellationToken);

        return Ok(units);
    }

    [HttpGet("item-options")]
    [HttpGet("grocery-options")]
    public async Task<IActionResult> ItemOptions(CancellationToken cancellationToken = default)
    {
        var items = await db.Items
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.ItemName)
            .Select(x => new { itemId = x.ItemId, itemName = x.ItemName })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost("stock-items-create")]
    public async Task<IActionResult> CreateStockItem([FromBody] StockItemInputDto request, CancellationToken cancellationToken)
    {
        if (!request.ItemId.HasValue || request.ItemId.Value <= 0)
        {
            return BadRequest("ItemId is required.");
        }

        var exists = await db.ItemStocks
            .AnyAsync(x => x.ItemId == request.ItemId.Value && !x.IsDeleted, cancellationToken);
        if (exists)
        {
            return BadRequest("This item stock already exists. Use edit.");
        }

        var reorderLevel = request.ReorderLevel ?? 0m;
        var openingQty = request.CurrentQty ?? reorderLevel;

        db.ItemStocks.Add(new ItemStock
        {
            ItemId = request.ItemId.Value,
            UnitId = request.UnitId,
            CurrentQty = openingQty,
            ReorderLevel = reorderLevel,
            Type = "Opening",
            StockDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IsActive = true,
            IsDeleted = false
        });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Created" });
    }

    [HttpPost("stock-items-update/{id:int}")]
    public async Task<IActionResult> UpdateStockItem(int id, [FromBody] StockItemInputDto request, CancellationToken cancellationToken)
    {
        var row = await db.ItemStocks.FirstOrDefaultAsync(x => x.ItemStockId == id, cancellationToken);
        if (row is null) return NotFound();

        if (request.ItemId.HasValue && request.ItemId.Value > 0 && request.ItemId.Value != row.ItemId)
        {
            var duplicate = await db.ItemStocks.AnyAsync(
                x => x.ItemStockId != id && x.ItemId == request.ItemId.Value && !x.IsDeleted,
                cancellationToken);
            if (duplicate)
            {
                return BadRequest("This item stock already exists. Use edit.");
            }

            row.ItemId = request.ItemId.Value;
        }

        row.UnitId = request.UnitId;
        row.CurrentQty = request.CurrentQty ?? row.CurrentQty;
        row.ReorderLevel = request.ReorderLevel ?? row.ReorderLevel;
        row.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Updated" });
    }

    [HttpPost("stock-items-delete/{id:int}")]
    public async Task<IActionResult> DeleteStockItem(int id, CancellationToken cancellationToken)
    {
        var row = await db.ItemStocks.FirstOrDefaultAsync(x => x.ItemStockId == id, cancellationToken);
        if (row is null) return NotFound();

        row.IsActive = false;
        row.IsDeleted = false;
        row.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Inactivated" });
    }

    private IQueryable<StockItemListRow> BuildItemStockQuery(string? search, int? unitId)
    {
        var query =
            from stock in db.ItemStocks
            where !stock.IsDeleted
            join item in db.Items.Where(x => !x.IsDeleted)
                on stock.ItemId equals item.ItemId into itemJoin
            from item in itemJoin.DefaultIfEmpty()
            join unit in db.Units.Where(x => !x.IsDeleted)
                on stock.UnitId equals unit.UnitId into unitJoin
            from unit in unitJoin.DefaultIfEmpty()
            select new StockItemListRow
            {
                ItemId = stock.ItemStockId,
                ItemCode = $"STK{stock.ItemStockId:000}",
                ItemIdRef = stock.ItemId,
                ItemName = item != null ? item.ItemName : "Item " + stock.ItemId,
                ReorderLevel = stock.ReorderLevel,
                IsActive = stock.IsActive,
                UnitId = stock.UnitId,
                UnitName = unit != null ? unit.UnitName : string.Empty,
                CurrentQty = stock.CurrentQty
            };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.ItemName.Contains(term) ||
                (x.ItemCode ?? string.Empty).Contains(term) ||
                x.ItemIdRef.ToString().Contains(term));
        }

        if (unitId.HasValue && unitId.Value > 0)
        {
            query = query.Where(x => x.UnitId == unitId.Value);
        }

        return query.OrderBy(x => x.ItemName);
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private sealed class StockItemListRow
    {
        public int ItemId { get; set; }
        public string? ItemCode { get; set; }
        public int ItemIdRef { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public decimal ReorderLevel { get; set; }
        public bool IsActive { get; set; }
        public int? UnitId { get; set; }
        public string? UnitName { get; set; }
        public decimal CurrentQty { get; set; }
    }

    public sealed record StockItemInputDto(
        int? OutletId,
        int? ItemId,
        int? UnitId,
        decimal? ReorderLevel,
        decimal? CurrentQty);
}
