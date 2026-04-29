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

        var allRows = await BuildItemStockRowsAsync(search, unitId, cancellationToken);
        var totalCount = allRows.Count;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        if (totalPages > 0 && page > totalPages) page = totalPages;

        var rows = allRows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

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
        var rows = await BuildItemStockRowsAsync(search, unitId, cancellationToken);
        var csv = new StringBuilder("Code,Name,Unit,Type,StockDate,CurrentQty,ReorderLevel,Status\n");
        foreach (var row in rows)
        {
            var status = row.IsActive ? "Active" : "Inactive";
            csv.AppendLine(
                $"{Escape(row.ItemCode ?? string.Empty)}," +
                $"{Escape(row.ItemName ?? string.Empty)}," +
                $"{Escape(row.UnitName ?? string.Empty)}," +
                $"{Escape(row.Type ?? string.Empty)}," +
                $"{row.StockDate:yyyy-MM-dd}," +
                $"{row.CurrentQty}," +
                $"{row.ReorderLevel}," +
                $"{status}"
            );
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
            .Where(x => !x.IsDeleted && x.IsStock)
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
        if (string.IsNullOrWhiteSpace(request.Type))
        {
            return BadRequest("Type is required (in/out).");
        }

        var type = request.Type.Trim().ToLowerInvariant();
        if (type != "in" && type != "out")
        {
            return BadRequest("Invalid Type. Use 'in' or 'out'.");
        }

        var reorderLevel = request.ReorderLevel ?? 0m;
        var qty = request.CurrentQty ?? reorderLevel;
        var stockDate = request.StockDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);

        db.ItemStocks.Add(new ItemStock
        {
            ItemId = request.ItemId.Value,
            UnitId = request.UnitId,
            CurrentQty = qty,
            ReorderLevel = reorderLevel,
            Type = type,
            StockDate = stockDate,
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
        if (string.IsNullOrWhiteSpace(request.Type))
        {
            return BadRequest("Type is required (in/out).");
        }

        var type = request.Type.Trim().ToLowerInvariant();
        if (type != "in" && type != "out")
        {
            return BadRequest("Invalid Type. Use 'in' or 'out'.");
        }

        row.UnitId = request.UnitId;
        row.CurrentQty = request.CurrentQty ?? row.CurrentQty;
        row.ReorderLevel = request.ReorderLevel ?? row.ReorderLevel;
        row.Type = type;
        row.StockDate = request.StockDate ?? row.StockDate;
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
        row.IsDeleted = true;
        row.DeletedAtUtc = DateTime.UtcNow;
        row.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "Inactivated" });
    }

    private async Task<List<StockItemListRow>> BuildItemStockRowsAsync(
        string? search,
        int? unitId,
        CancellationToken cancellationToken)
    {
        // 100% EF-translation-safe approach:
        // 1) Load only required rows
        // 2) Pick the latest ItemStockId per ItemId in C#
        // 3) Join with Items/Units in C#
        var stocks = await db.ItemStocks
            .Where(x => !x.IsDeleted)
            .Select(x => new
            {
                x.ItemStockId,
                x.ItemId,
                x.UnitId,
                x.CurrentQty,
                x.ReorderLevel,
                x.Type,
                x.StockDate,
                x.IsActive
            })
            .ToListAsync(cancellationToken);

        var items = await db.Items
            .Where(x => !x.IsDeleted)
            .Select(x => new { x.ItemId, x.ItemName, x.ItemCode })
            .ToListAsync(cancellationToken);

        var units = await db.Units
            .Where(x => !x.IsDeleted)
            .Select(x => new { x.UnitId, x.UnitName })
            .ToListAsync(cancellationToken);

        var itemsById = items.ToDictionary(x => x.ItemId, x => x);
        var unitsById = units.ToDictionary(x => x.UnitId, x => x.UnitName);

        var latestByItem = stocks
            .GroupBy(x => x.ItemId)
            .Select(g => g
                .OrderByDescending(s => s.StockDate)
                .ThenByDescending(s => s.ItemStockId)
                .First())
            .ToList();

        IEnumerable<StockItemListRow> rows = latestByItem.Select(stock =>
        {
            itemsById.TryGetValue(stock.ItemId, out var item);
            var unitName = stock.UnitId.HasValue && unitsById.TryGetValue(stock.UnitId.Value, out var u)
                ? u
                : string.Empty;

            return new StockItemListRow
            {
                ItemId = stock.ItemStockId,
                ItemCode = $"STK{stock.ItemStockId:000}",
                ItemIdRef = stock.ItemId,
                ItemName = item != null ? item.ItemName : "Item " + stock.ItemId,
                ReorderLevel = stock.ReorderLevel,
                IsActive = stock.IsActive,
                UnitId = stock.UnitId,
                UnitName = unitName,
                CurrentQty = stock.CurrentQty,
                Type = stock.Type,
                StockDate = stock.StockDate
            };
        });

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var hasInt = int.TryParse(term, out var itemIdTerm);

            rows = rows.Where(x =>
                x.ItemName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (x.ItemCode ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (hasInt && x.ItemIdRef == itemIdTerm));
        }

        if (unitId.HasValue && unitId.Value > 0)
        {
            rows = rows.Where(x => x.UnitId == unitId.Value);
        }

        return rows
            .OrderBy(x => x.ItemName)
            .ToList();
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
        public string? Type { get; set; }
        public DateOnly StockDate { get; set; }
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
        decimal? CurrentQty,
        string? Type,
        DateOnly? StockDate);
}
