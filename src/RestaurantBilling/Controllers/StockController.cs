using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Entities.Enums;
using Entities.Inventory;
using Data.Persistence;
using RestaurantBilling.Models.Stock;

namespace RestaurantBilling.Controllers;

[Authorize]
[Route("stock")]
public class StockController(AppDbContext db) : Controller
{
    [HttpGet("entry")]
    public IActionResult Entry()
    {
        ViewBag.UseDataTables = true;
        return View();
    }

    [HttpGet("balance")]
    public IActionResult Balance()
    {
        ViewBag.UseDataTables = true;
        return View();
    }

    [HttpGet("purchase")]
    public IActionResult Purchase() => View();

    [HttpGet("adjustment")]
    public IActionResult Adjustment()
    {
        ViewBag.UseDataTables = true;
        return View();
    }

    [HttpGet("loss")]
    public IActionResult Loss()
    {
        ViewBag.UseDataTables = true;
        return View();
    }

    [HttpGet("take")]
    public IActionResult Take()
    {
        ViewBag.UseDataTables = true;
        return View();
    }

    [HttpGet("balance-data")]
    public async Task<IActionResult> BalanceData([FromQuery] int outletId, CancellationToken cancellationToken)
    {
        var latestBalances = await db.StockLedger
            .Where(x => x.OutletId == outletId)
            .GroupBy(x => x.ItemId)
            .Select(g => new
            {
                ItemId = g.Key,
                CurrentQty = g.OrderByDescending(x => x.StockLedgerEntryId).Select(x => x.RunningBalance).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var rows = await db.Items
            .Where(x => x.OutletId == outletId && x.IsStockTracked)
            .OrderBy(x => x.ItemName)
            .Select(x => new { x.ItemId, x.ItemName, x.ReorderLevel })
            .ToListAsync(cancellationToken);

        var result = rows
            .Select(x =>
            {
                var current = latestBalances.FirstOrDefault(l => l.ItemId == x.ItemId)?.CurrentQty ?? 0m;
                return new
                {
                    x.ItemId,
                    x.ItemName,
                    currentQty = current,
                    x.ReorderLevel,
                    status = current <= x.ReorderLevel ? "Low" : "Normal"
                };
            })
            .ToList();

        return Ok(result);
    }

    [HttpGet("items")]
    public async Task<IActionResult> Items([FromQuery] int outletId, CancellationToken cancellationToken)
    {
        var rows = await db.Items
            .Where(x => x.OutletId == outletId && x.IsStockTracked)
            .OrderBy(x => x.ItemName)
            .Select(x => new { x.ItemId, x.ItemName })
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    [HttpPost("entry")]
    public async Task<IActionResult> StockEntry([FromBody] StockEntryRequest request, CancellationToken cancellationToken)
    {
        var current = await db.StockLedger
            .Where(x => x.OutletId == request.OutletId && x.ItemId == request.ItemId)
            .OrderByDescending(x => x.StockLedgerEntryId)
            .Select(x => x.RunningBalance)
            .FirstOrDefaultAsync(cancellationToken);

        var lot = new StockLot
        {
            OutletId = request.OutletId,
            ItemId = request.ItemId,
            ReceivedOn = request.BusinessDate,
            ExpiryOn = request.ExpiryOn,
            QtyReceived = request.Qty,
            QtyRemaining = request.Qty,
            CostPerUnit = request.CostPerUnit
        };
        db.StockLots.Add(lot);

        db.StockLedger.Add(StockLedgerEntry.Add(
            request.OutletId,
            request.ItemId,
            request.BusinessDate,
            StockReferenceType.Purchase,
            0,
            request.Qty,
            request.CostPerUnit,
            current,
            "GRN stock entry"));

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "StockAdded" });
    }

    [HttpPost("adjustment")]
    public async Task<IActionResult> StockAdjustment([FromBody] StockAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var current = await db.StockLedger
            .Where(x => x.OutletId == request.OutletId && x.ItemId == request.ItemId)
            .OrderByDescending(x => x.StockLedgerEntryId)
            .Select(x => x.RunningBalance)
            .FirstOrDefaultAsync(cancellationToken);

        var isAdd = request.AdjustmentType.Equals("Add", StringComparison.OrdinalIgnoreCase);
        var entry = isAdd
            ? StockLedgerEntry.Add(request.OutletId, request.ItemId, request.BusinessDate, StockReferenceType.Adjustment, 0, request.Qty, request.Rate, current, request.Reason)
            : StockLedgerEntry.Deduct(request.OutletId, request.ItemId, request.BusinessDate, StockReferenceType.Adjustment, 0, request.Qty, request.Rate, current, request.Reason);

        db.StockLedger.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "StockAdjusted" });
    }

    [HttpPost("loss")]
    public async Task<IActionResult> StockLoss([FromBody] StockLossRequest request, CancellationToken cancellationToken)
    {
        var current = await db.StockLedger
            .Where(x => x.OutletId == request.OutletId && x.ItemId == request.ItemId)
            .OrderByDescending(x => x.StockLedgerEntryId)
            .Select(x => x.RunningBalance)
            .FirstOrDefaultAsync(cancellationToken);

        db.StockLedger.Add(StockLedgerEntry.Deduct(
            request.OutletId,
            request.ItemId,
            request.BusinessDate,
            StockReferenceType.Loss,
            0,
            request.Qty,
            request.Rate,
            current,
            request.Reason));

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "LossRecorded" });
    }

    [HttpPost("take")]
    public async Task<IActionResult> StockTake([FromBody] StockTakeRequest request, CancellationToken cancellationToken)
    {
        var theoretical = await db.StockLedger
            .Where(x => x.OutletId == request.OutletId && x.ItemId == request.ItemId)
            .OrderByDescending(x => x.StockLedgerEntryId)
            .Select(x => x.RunningBalance)
            .FirstOrDefaultAsync(cancellationToken);

        var variance = request.PhysicalQty - theoretical;
        return Ok(new
        {
            itemId = request.ItemId,
            theoreticalQty = theoretical,
            physicalQty = request.PhysicalQty,
            varianceQty = variance
        });
    }
}

