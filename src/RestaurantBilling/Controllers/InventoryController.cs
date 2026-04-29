using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using System.IO;
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
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        var rows = await BuildItemStockRowsAsync(search, unitId, cancellationToken);
        if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Stock");
            var headers = new[]
            {
                "Code", "Name", "Unit", "Stock Date", "Opening Qty", "Purchased Qty",
                "Sold Qty", "Disposed Qty", "Closing Qty", "Status"
            };

            for (var i = 0; i < headers.Length; i++)
            {
                sheet.Cell(1, i + 1).Value = headers[i];
                sheet.Cell(1, i + 1).Style.Font.Bold = true;
            }

            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                var excelRow = rowIndex + 2;
                sheet.Cell(excelRow, 1).Value = row.ItemCode ?? string.Empty;
                sheet.Cell(excelRow, 2).Value = row.ItemName ?? string.Empty;
                sheet.Cell(excelRow, 3).Value = row.UnitName ?? string.Empty;
                sheet.Cell(excelRow, 4).Value = row.StockDate.ToString("yyyy-MM-dd");
                sheet.Cell(excelRow, 5).Value = row.OpeningQty;
                sheet.Cell(excelRow, 6).Value = row.PurchasedQty;
                sheet.Cell(excelRow, 7).Value = row.SoldQty;
                sheet.Cell(excelRow, 8).Value = row.DisposedQty;
                sheet.Cell(excelRow, 9).Value = row.ClosingQty;
                sheet.Cell(excelRow, 10).Value = row.IsActive ? "Active" : "Inactive";
            }

            sheet.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            var fileName = $"stock-items-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx";
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        var csv = new StringBuilder("Code,Name,Unit,StockDate,OpeningQty,PurchasedQty,SoldQty,DisposedQty,ClosingQty,Status\n");
        foreach (var row in rows)
        {
            var status = row.IsActive ? "Active" : "Inactive";
            csv.AppendLine(
                $"{Escape(row.ItemCode ?? string.Empty)}," +
                $"{Escape(row.ItemName ?? string.Empty)}," +
                $"{Escape(row.UnitName ?? string.Empty)}," +
                $"{row.StockDate:yyyy-MM-dd}," +
                $"{row.OpeningQty}," +
                $"{row.PurchasedQty}," +
                $"{row.SoldQty}," +
                $"{row.DisposedQty}," +
                $"{row.ClosingQty}," +
                $"{status}"
            );
        }
        var csvFileName = $"stock-items-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", csvFileName);
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

    [HttpGet("stock-summary")]
    public async Task<IActionResult> StockSummary(
        [FromQuery] DateOnly? stockDate,
        CancellationToken cancellationToken)
    {
        var date = stockDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var rows = await db.ItemStocks
            .Where(x => !x.IsDeleted && x.StockDate == date)
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            stockDate = date.ToString("yyyy-MM-dd"),
            items = rows.Count,
            openingQty = rows.Sum(x => x.OpeningQty),
            purchasedQty = rows.Sum(x => x.PurchasedQty),
            soldQty = rows.Sum(x => x.SoldQty),
            disposedQty = rows.Sum(x => x.DisposedQty),
            closingQty = rows.Sum(x => x.ClosingQty)
        });
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
        var openingQty = request.OpeningQty ?? 0m;
        var purchasedQty = request.PurchasedQty ?? 0m;
        var soldQty = request.SoldQty ?? 0m;
        var disposedQty = request.DisposedQty ?? 0m;
        var closingQty = openingQty + purchasedQty - soldQty - disposedQty;
        var stockDate = request.StockDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);

        db.ItemStocks.Add(new ItemStock
        {
            ItemId = request.ItemId.Value,
            UnitId = request.UnitId,
            OpeningQty = openingQty,
            PurchasedQty = purchasedQty,
            SoldQty = soldQty,
            DisposedQty = disposedQty,
            ClosingQty = closingQty,
            Type = "snapshot",
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
        row.UnitId = request.UnitId;
        row.OpeningQty = request.OpeningQty ?? row.OpeningQty;
        row.PurchasedQty = request.PurchasedQty ?? row.PurchasedQty;
        row.SoldQty = request.SoldQty ?? row.SoldQty;
        row.DisposedQty = request.DisposedQty ?? row.DisposedQty;
        row.ClosingQty = row.OpeningQty + row.PurchasedQty - row.SoldQty - row.DisposedQty;
        row.Type = "snapshot";
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
                x.OpeningQty,
                x.PurchasedQty,
                x.SoldQty,
                x.DisposedQty,
                x.ClosingQty,
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
                IsActive = stock.IsActive,
                UnitId = stock.UnitId,
                UnitName = unitName,
                OpeningQty = stock.OpeningQty,
                PurchasedQty = stock.PurchasedQty,
                SoldQty = stock.SoldQty,
                DisposedQty = stock.DisposedQty,
                ClosingQty = stock.ClosingQty,
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
        public bool IsActive { get; set; }
        public int? UnitId { get; set; }
        public string? UnitName { get; set; }
        public decimal OpeningQty { get; set; }
        public decimal PurchasedQty { get; set; }
        public decimal SoldQty { get; set; }
        public decimal DisposedQty { get; set; }
        public decimal ClosingQty { get; set; }
    }

    public sealed record StockItemInputDto(
        int? OutletId,
        int? ItemId,
        int? UnitId,
        decimal? OpeningQty,
        decimal? PurchasedQty,
        decimal? SoldQty,
        decimal? DisposedQty,
        string? Type,
        DateOnly? StockDate);
}
