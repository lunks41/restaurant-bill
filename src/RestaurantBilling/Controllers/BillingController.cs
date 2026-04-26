using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IServices;
using Entities.Audit;
using Entities.Enums;
using Entities.Sales;
using Data.Persistence;
using RestaurantBilling.Models.Billing;
using Microsoft.EntityFrameworkCore;

namespace RestaurantBilling.Controllers;

[Authorize]
[Route("billing")]
public class BillingController(
    IStockService stockService,
    IAuditService auditService,
    AppDbContext db) : Controller
{
    [HttpGet("quote")]
    public IActionResult Quote() => RedirectToAction(nameof(BillList));

    [HttpGet("bills")]
    [HttpGet("/bills")]
    public IActionResult BillList()
    {
        ViewBag.UseDataTables = true;
        return View();
    }

    [HttpGet("bills/{billId:long}")]
    [HttpGet("/bills/{billId:long}")]
    public async Task<IActionResult> BillDetail(long billId, CancellationToken cancellationToken)
    {
        var row = await db.Bills
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.BillId == billId, cancellationToken);
        if (row is null) return NotFound();
        return View(row);
    }

    [HttpPost("void")]
    public async Task<IActionResult> VoidBill([FromBody] VoidBillRequest request, CancellationToken cancellationToken)
    {
        var pinSetting = await db.RestaurantSettings
            .FirstOrDefaultAsync(x => x.OutletId == request.OutletId && x.SettingKey == "ManagerPin", cancellationToken);
        if (pinSetting is null || pinSetting.SettingValue != request.ManagerPin)
        {
            return Unauthorized("Invalid manager PIN.");
        }

        var bill = await db.Bills.Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.BillId == request.BillId && x.OutletId == request.OutletId, cancellationToken);
        if (bill is null)
        {
            return NotFound("Bill not found.");
        }
        if (bill.Status == BillStatus.Cancelled)
        {
            return BadRequest("Bill already cancelled.");
        }
        if (await IsBusinessDateLocked(request.OutletId, bill.BusinessDate, cancellationToken))
        {
            return Conflict("Business date is locked.");
        }

        await stockService.ReverseSaleStockAsync(request.OutletId, bill.BillId, bill.BusinessDate, bill.Items.ToList(), cancellationToken);
        bill.Cancel();
        await db.SaveChangesAsync(cancellationToken);

        await auditService.LogAsync(
            request.OutletId, request.UserId, "VoidBill", nameof(Bill), bill.BillId.ToString(),
            "{\"status\":\"Paid\"}",
            $"{{\"status\":\"Cancelled\",\"reason\":\"{request.Reason}\"}}",
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        return Ok(new { status = "Voided", billId = bill.BillId });
    }

    [HttpGet("bills-data")]
    [HttpGet("/bills-data")]
    public async Task<IActionResult> BillsData([FromQuery] int outletId, CancellationToken cancellationToken)
    {
        var rows = await db.Bills
            .AsNoTracking()
            .Where(x => x.OutletId == outletId && x.Status == BillStatus.Paid)
            .OrderByDescending(x => x.BillDate)
            .Take(500)
            .Select(x => new
            {
                x.BillId,
                x.BillNo,
                billDate = x.BillDate.ToString("dd-MMM-yyyy HH:mm"),
                status = x.Status.ToString(),
                billType = x.BillType.ToString(),
                tableName = x.TableName,
                itemCount = x.Items.Count,
                x.GrandTotal
            })
            .ToListAsync(cancellationToken);
        return Ok(rows);
    }

    private async Task<bool> IsBusinessDateLocked(int outletId, DateOnly businessDate, CancellationToken cancellationToken)
        => await db.DayCloseReports.AnyAsync(x => x.OutletId == outletId && x.BusinessDate == businessDate && x.IsLocked, cancellationToken);
}

