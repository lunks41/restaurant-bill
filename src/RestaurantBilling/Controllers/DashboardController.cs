using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Data.Persistence;
using Entities.Enums;

namespace RestaurantBilling.Controllers;

[Authorize]
public class DashboardController(AppDbContext db) : Controller
{
    private const decimal LowStockThreshold = 10m;
    [HttpGet("/")]
    [HttpGet("/dashboard")]
    [HttpGet("/dashbord")]
    public IActionResult Index()
    {
        ViewBag.UseCharts = true;
        ViewBag.UseDataTables = true;
        ViewBag.UseSignalR = true;
        return View();
    }

    [HttpGet("/dashboard/kpi")]
    public async Task<IActionResult> Kpi(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);

        var todaySales = await db.Bills
            .Where(x => x.BusinessDate == today && x.Status != BillStatus.Cancelled)
            .SumAsync(x => (decimal?)x.GrandTotal, cancellationToken) ?? 0m;

        var yesterdaySales = await db.Bills
            .Where(x => x.BusinessDate == yesterday && x.Status != BillStatus.Cancelled)
            .SumAsync(x => (decimal?)x.GrandTotal, cancellationToken) ?? 0m;

        var billsCount = await db.Bills
            .Where(x => x.BusinessDate == today && x.Status != BillStatus.Cancelled)
            .CountAsync(cancellationToken);

        var pendingKot = await db.KotHeaders
            .Where(x => x.Status != "Served" && x.Status != "Cancelled")
            .CountAsync(cancellationToken);

        var activeTables = await db.Bills
            .Where(x => x.BillType == BillType.DineIn && x.Status == BillStatus.Draft)
            .CountAsync(cancellationToken);

        var pendingBills = await db.Bills
            .Where(x => x.Status == BillStatus.Draft)
            .CountAsync(cancellationToken);

        var lowStock = await (
            from stock in db.ItemStocks
            join item in db.Items on stock.ItemId equals item.ItemId
            where !stock.IsDeleted
                  && stock.IsActive
                  && item.IsStock
                  && stock.ClosingQty > 0
                  && stock.ClosingQty <= LowStockThreshold
            select stock.ItemId
        ).Distinct().CountAsync(cancellationToken);

        var outOfStock = await (
            from stock in db.ItemStocks
            join item in db.Items on stock.ItemId equals item.ItemId
            where !stock.IsDeleted
                  && stock.IsActive
                  && item.IsStock
                  && stock.ClosingQty <= 0
            select stock.ItemId
        ).Distinct().CountAsync(cancellationToken);

        return Ok(new
        {
            todaySales,
            yesterdaySales,
            billsCount,
            pendingKot,
            activeTables,
            pendingBills,
            lowStock,
            outOfStock
        });
    }

    [HttpGet("/dashboard/top-items")]
    public async Task<IActionResult> TopItems(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var rows = await db.BillItems
            .Join(db.Bills, bi => bi.BillId, b => b.BillId, (bi, b) => new { bi, b })
            .Where(x => x.b.BusinessDate == today && x.b.Status != BillStatus.Cancelled)
            .GroupBy(x => new { x.bi.ItemId, x.bi.ItemNameSnapshot })
            .Select(g => new
            {
                itemName = g.Key.ItemNameSnapshot,
                totalQty = g.Sum(x => x.bi.Qty),
                totalSales = g.Sum(x => x.bi.RateSnapshot * x.bi.Qty)
            })
            .OrderByDescending(x => x.totalQty)
            .Take(5)
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    [HttpGet("/dashboard/sales-trend")]
    public async Task<IActionResult> SalesTrend(CancellationToken cancellationToken)
    {
        var to = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = to.AddDays(-6);

        var rows = await db.Bills
            .Where(x => x.BusinessDate >= from && x.BusinessDate <= to && x.Status != BillStatus.Cancelled)
            .GroupBy(x => x.BusinessDate)
            .Select(g => new { Date = g.Key, Sales = g.Sum(x => x.GrandTotal) })
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(x => new { date = x.Date.ToString("dd-MMM"), sales = x.Sales }));
    }

    [HttpGet("/dashboard/payment-breakdown")]
    public async Task<IActionResult> PaymentBreakdown(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var rows = await db.Payments
            .Join(db.Bills, p => p.BillId, b => b.BillId, (p, b) => new { p, b })
            .Where(x => x.b.BusinessDate == today && x.b.Status != BillStatus.Cancelled)
            .GroupBy(x => x.p.PaymentMode)
            .Select(g => new { mode = g.Key.ToString(), amount = g.Sum(x => x.p.Amount) })
            .OrderByDescending(x => x.amount)
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    [HttpGet("/dashboard/recent-bills")]
    public async Task<IActionResult> RecentBills(CancellationToken cancellationToken)
    {
        var rows = await db.Bills
            .OrderByDescending(x => x.BillDate)
            .Take(10)
            .Select(x => new
            {
                x.BillId,
                x.BillNo,
                billDate = x.BillDate.ToString("dd-MMM-yyyy HH:mm"),
                status = x.Status.ToString(),
                billType = x.BillType.ToString(),
                x.GrandTotal
            })
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    [HttpGet("/dashboard/low-stock")]
    public async Task<IActionResult> LowStock(CancellationToken cancellationToken)
    {
        var rows = await (
            from stock in db.ItemStocks
            join item in db.Items on stock.ItemId equals item.ItemId
            join unit in db.Units on stock.UnitId equals unit.UnitId into unitJoin
            from unit in unitJoin.DefaultIfEmpty()
            where !stock.IsDeleted
                  && stock.IsActive
                  && item.IsStock
                  && stock.ClosingQty <= LowStockThreshold
            orderby stock.ClosingQty ascending, item.ItemName
            select new
            {
                stock.ItemId,
                item.ItemName,
                unitName = unit != null ? unit.UnitName : null,
                stock.ClosingQty
            })
            .Take(20)
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }
}

