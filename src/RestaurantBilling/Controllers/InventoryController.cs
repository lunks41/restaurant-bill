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

        var query = BuildGroceryStockQuery(search, unitId);
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
        var rows = await BuildGroceryStockQuery(search, unitId).ToListAsync(cancellationToken);
        var csv = new StringBuilder("Code,GroceryId,UnitId,CurrentQty,ReorderLevel,Status\n");
        foreach (var row in rows)
        {
            var status = row.IsActive ? "Active" : "Inactive";
            csv.AppendLine($"{Escape(row.ItemCode ?? string.Empty)},{row.GroceryId},{(row.UnitId.HasValue ? row.UnitId.Value.ToString() : string.Empty)},{row.CurrentQty},{row.ReorderLevel},{status}");
        }
        var fileName = $"stock-items-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
    }

    [HttpGet("stock-units-data")]
    public async Task<IActionResult> StockUnitsData([FromQuery] int outletId, CancellationToken cancellationToken)
    {
        var units = await db.GroceryStockItems
    .Where(x => !x.IsDeleted && x.UnitId.HasValue)
    // Join with the Units table to get actual names/codes
    .Join(db.Units,
          stock => stock.UnitId,
          unit => unit.UnitId,
          (stock, unit) => new { unit.UnitId, unit.UnitName, unit.UnitCode })
    .Distinct()
    .OrderBy(x => x.UnitName)
    .ToListAsync(cancellationToken);

        return Ok(units);
    }

    [HttpGet("grocery-options")]
    public async Task<IActionResult> GroceryOptions([FromQuery] int outletId = 1, CancellationToken cancellationToken = default)
    {
        var groceries = await db.GroceryStockItems
     .Where(x => !x.IsDeleted)
     // Join with the Groceries table to get the name directly
     .Join(db.Groceries,
           stock => stock.GroceryId,
           grocery => grocery.GroceryId,
           (stock, grocery) => new { stock.GroceryId, grocery.GroceryName })
     .Distinct() // Ensure unique ID/Name pairs
     .OrderBy(x => x.GroceryName)
     .ToListAsync(cancellationToken);

        return Ok(groceries);
    }

    [HttpPost("stock-items-create")]
    public async Task<IActionResult> CreateStockItem([FromBody] StockItemInputDto request, CancellationToken cancellationToken)
    {
        if (!request.GroceryId.HasValue || request.GroceryId.Value <= 0)
        {
            return BadRequest("GroceryId is required.");
        }

        var exists = await db.GroceryStockItems
            .AnyAsync(x => x.GroceryId == request.GroceryId.Value && !x.IsDeleted, cancellationToken);
        if (exists)
        {
            return BadRequest("This grocery stock item already exists. Use edit.");
        }

        var reorderLevel = request.ReorderLevel ?? 0m;
        var openingQty = request.CurrentQty ?? reorderLevel;

        db.GroceryStockItems.Add(new GroceryStockItem
        {
            GroceryId = request.GroceryId.Value,
            UnitId = request.UnitId,
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
        var row = await db.GroceryStockItems.FirstOrDefaultAsync(x => x.GroceryStockItemId == id, cancellationToken);
        if (row is null) return NotFound();

        if (request.GroceryId.HasValue && request.GroceryId.Value > 0 && request.GroceryId.Value != row.GroceryId)
        {
            var duplicate = await db.GroceryStockItems.AnyAsync(
                x => x.GroceryStockItemId != id && x.GroceryId == request.GroceryId.Value && !x.IsDeleted,
                cancellationToken);
            if (duplicate)
            {
                return BadRequest("This grocery stock item already exists. Use edit.");
            }

            row.GroceryId = request.GroceryId.Value;
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
        var row = await db.GroceryStockItems.FirstOrDefaultAsync(x => x.GroceryStockItemId == id, cancellationToken);
        if (row is null) return NotFound();

        row.IsActive = false;
        row.IsDeleted = false;
        row.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Inactivated" });
    }

    private IQueryable<StockItemListRow> BuildGroceryStockQuery(string? search, int? unitId)
    {
        var query =
            from stock in db.GroceryStockItems
            where !stock.IsDeleted
            join grocery in db.Groceries.Where(x => !x.IsDeleted)
                on stock.GroceryId equals grocery.GroceryId into groceryJoin
            from grocery in groceryJoin.DefaultIfEmpty()
            join unit in db.Units.Where(x => !x.IsDeleted)
                on stock.UnitId equals unit.UnitId into unitJoin
            from unit in unitJoin.DefaultIfEmpty()
            select new StockItemListRow
            {
                ItemId = stock.GroceryStockItemId,
                ItemCode = $"GRC{stock.GroceryStockItemId:000}",
                GroceryId = stock.GroceryId,
                ItemName = grocery != null ? grocery.GroceryName : "Grocery " + stock.GroceryId,
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
                x.GroceryId.ToString().Contains(term));
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
        public int GroceryId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public decimal ReorderLevel { get; set; }
        public bool IsActive { get; set; }
        public int? UnitId { get; set; }
        public string? UnitName { get; set; }
        public decimal CurrentQty { get; set; }
    }

    public sealed record StockItemInputDto(
        int? OutletId,
        int? GroceryId,
        int? UnitId,
        decimal? ReorderLevel,
        decimal? CurrentQty);
}
