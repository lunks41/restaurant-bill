using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IServices;
using Helper;
using Entities.Reports;
using Data.Persistence;
using RestaurantBilling.Models.DayClose;

namespace RestaurantBilling.Controllers;

[Authorize]
public class DayCloseController(AppDbContext db, IAuditService auditService) : Controller
{
    [HttpGet("/dayclose")]
    [Authorize(Policy = Permissions.CanCloseDay)]
    public IActionResult Index() => View();

    [HttpPost("/dayclose/preview")]
    [Authorize(Policy = Permissions.CanCloseDay)]
    public async Task<IActionResult> Preview([FromBody] DayClosePreviewRequest request, CancellationToken cancellationToken)
    {
        var unsettledCount = await db.Bills
            .Where(x => x.OutletId == request.OutletId && x.BusinessDate == request.BusinessDate && x.Status == Entities.Enums.BillStatus.Draft)
            .CountAsync(cancellationToken);

        var totals = await db.Bills
            .Where(x => x.OutletId == request.OutletId && x.BusinessDate == request.BusinessDate)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Sales = g.Sum(x => x.GrandTotal),
                Tax = g.Sum(x => x.TaxAmount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new
        {
            businessDate = request.BusinessDate,
            unsettledBills = unsettledCount,
            totalSales = totals?.Sales ?? 0,
            totalTax = totals?.Tax ?? 0
        });
    }

    [HttpPost("/dayclose/finalize")]
    [Authorize(Policy = Permissions.CanCloseDay)]
    public async Task<IActionResult> FinalizeClose([FromBody] DayCloseFinalizeRequest request, CancellationToken cancellationToken)
    {
        var existing = await db.DayCloseReports
            .FirstOrDefaultAsync(x => x.OutletId == request.OutletId && x.BusinessDate == request.BusinessDate && x.IsLocked, cancellationToken);
        if (existing is not null)
        {
            return BadRequest("Business date already closed.");
        }

        var totals = await db.Bills
            .Where(x => x.OutletId == request.OutletId && x.BusinessDate == request.BusinessDate)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Sales = g.Sum(x => x.GrandTotal),
                Tax = g.Sum(x => x.TaxAmount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        db.DayCloseReports.Add(new DayCloseReport
        {
            OutletId = request.OutletId,
            BusinessDate = request.BusinessDate,
            OpenedAtUtc = DateTime.UtcNow,
            ClosedAtUtc = DateTime.UtcNow,
            ClosedBy = request.ClosedBy,
            OpeningCash = request.OpeningCash,
            ClosingCash = request.ClosingCash,
            TotalSales = totals?.Sales ?? 0,
            TotalTax = totals?.Tax ?? 0,
            CashOverShort = request.ClosingCash - request.OpeningCash,
            IsLocked = true
        });

        var setting = await db.RestaurantSettings.FirstOrDefaultAsync(
            x => x.OutletId == request.OutletId && x.SettingKey == "CurrentBusinessDate",
            cancellationToken);
        var nextBizDate = request.BusinessDate.AddDays(1).ToString("yyyy-MM-dd");
        if (setting is null)
        {
            db.RestaurantSettings.Add(new Entities.Configuration.RestaurantSetting
            {
                OutletId = request.OutletId,
                SettingKey = "CurrentBusinessDate",
                SettingValue = nextBizDate
            });
        }
        else
        {
            setting.SettingValue = nextBizDate;
        }

        await db.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            request.OutletId, request.ClosedBy, "DayCloseFinalize", nameof(DayCloseReport), request.BusinessDate.ToString("yyyy-MM-dd"),
            null,
            $"{{\"openingCash\":{request.OpeningCash},\"closingCash\":{request.ClosingCash},\"locked\":true}}",
            HttpContext?.Connection.RemoteIpAddress?.ToString(),
            Request?.Headers.UserAgent.ToString(),
            cancellationToken);
        return Ok(new { status = "DayClosed", nextBusinessDate = nextBizDate });
    }
}

