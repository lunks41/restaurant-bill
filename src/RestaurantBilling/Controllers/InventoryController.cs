using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Data.Persistence;
using Entities.Inventory;

namespace RestaurantBilling.Controllers;

[Authorize]
[Route("master")]
[Route("masters")]
public class InventoryController(AppDbContext db) : Controller
{
    private static readonly string[] AllowedGroceries =
    {
        "Cooking Oil",
        "Rice",
        "Millets",
        "Milk",
        "Eggs",
        "Bread",
        "Butter",
        "Apples",
        "Pasta",
        "Chicken",
        "Beans",
        "Salt",
        "Pepper",
        "Coffee",
        "Tea",
        "Toilet Paper",
        "Dish Soap",
        "All-purpose Cleaner"
    };

    [HttpGet("stock")]
    public IActionResult Stock([FromQuery] bool embed)
    {
        ViewBag.UseDataTables = true;
        ViewData["EmbedMode"] = embed;
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

        var query = BuildGroceryStockQuery(outletId, search, unitId);
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
        var rows = await BuildGroceryStockQuery(outletId, search, unitId).ToListAsync(cancellationToken);
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

    [HttpGet("grocery-options")]
    public IActionResult GroceryOptions()
    {
        return Ok(AllowedGroceries);
    }

    [HttpPost("stock-items-create")]
    public async Task<IActionResult> CreateStockItem([FromBody] StockItemInputDto request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return BadRequest("Name is required.");
        }
        if (!AllowedGroceries.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest("Only predefined grocery items are allowed.");
        }

        var outletId = request.OutletId ?? 1;
        var reorderLevel = request.ReorderLevel ?? 0m;
        var openingQty = request.CurrentQty ?? reorderLevel;
        var purchaseRate = request.PurchasePrice ?? 0m;

        var exists = await db.GroceryStockItems
            .AnyAsync(x => x.OutletId == outletId && x.GroceryName == name && !x.IsDeleted, cancellationToken);
        if (exists)
        {
            return BadRequest("This grocery item already exists. Use edit.");
        }

        db.GroceryStockItems.Add(new GroceryStockItem
        {
            OutletId = outletId,
            GroceryName = name,
            UnitId = request.UnitId,
            PurchaseRate = purchaseRate,
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

        var name = request.Name?.Trim() ?? row.GroceryName;
        if (!AllowedGroceries.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest("Only predefined grocery items are allowed.");
        }

        row.GroceryName = name;
        row.UnitId = request.UnitId;
        row.PurchaseRate = request.PurchasePrice ?? row.PurchaseRate;
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
        row.IsDeleted = true;
        row.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Deleted" });
    }

    public sealed record StockItemInputDto(
        int? OutletId,
        string? Code,
        string? Name,
        int? UnitId,
        decimal? PurchasePrice,
        decimal? ReorderLevel,
        decimal? CurrentQty);

    private IQueryable<StockItemListRow> BuildGroceryStockQuery(int outletId, string? search, int? unitId)
    {
        var query =
            from stock in db.GroceryStockItems
            where stock.OutletId == outletId && !stock.IsDeleted
            join unit in db.Units.Where(u => u.OutletId == outletId && !u.IsDeleted) on stock.UnitId equals (int?)unit.UnitId into unitJoin
            from unit in unitJoin.DefaultIfEmpty()
            select new StockItemListRow
            {
                ItemId = stock.GroceryStockItemId,
                ItemCode = $"GRC{stock.GroceryStockItemId:000}",
                ItemName = stock.GroceryName,
                PurchasePrice = stock.PurchaseRate,
                ReorderLevel = stock.ReorderLevel,
                IsActive = stock.IsActive,
                UnitId = stock.UnitId,
                UnitName = unit == null ? string.Empty : unit.UnitName,
                CurrentQty = stock.CurrentQty
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
