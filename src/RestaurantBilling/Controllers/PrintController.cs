using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IServices;
using Entities.Audit;
using Data.Persistence;
using RestaurantBilling.Models.Billing;
using RestaurantBilling.Models.Kitchen;
using Microsoft.EntityFrameworkCore;

namespace RestaurantBilling.Controllers;

[Authorize]
public class PrintController(
    AppDbContext db,
    IAuditService auditService) : Controller
{
    [HttpPost("/billing/reprint")]
    public async Task<IActionResult> BillingReprint([FromBody] ReprintRequest request, CancellationToken cancellationToken)
    {
        var pinSetting = await db.RestaurantSettings
            .FirstOrDefaultAsync(x => x.SettingKey == "ManagerPin", cancellationToken);
        if (pinSetting is null || pinSetting.SettingValue != request.ManagerPin)
        {
            return Unauthorized("Invalid manager PIN.");
        }

        db.ReprintLogs.Add(new ReprintLog
        {
            DocumentType = request.DocumentType,
            DocumentId = request.DocumentId,
            ReprintedBy = request.UserId,
            Reason = request.Reason
        });
        await db.SaveChangesAsync(cancellationToken);

        await auditService.LogAsync(
            request.UserId, "Reprint", "Document", request.DocumentId.ToString(),
            null,
            $"{{\"documentType\":\"{request.DocumentType}\",\"reason\":\"{request.Reason}\"}}",
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        return Ok(new { status = "ReprintLogged" });
    }

    [HttpPost("/kot/reprint")]
    public async Task<IActionResult> KotReprint([FromBody] ReprintKotRequest request, CancellationToken cancellationToken)
    {
        var pinSetting = await db.RestaurantSettings
            .FirstOrDefaultAsync(x => x.SettingKey == "ManagerPin", cancellationToken);
        if (pinSetting is null || pinSetting.SettingValue != request.ManagerPin)
        {
            return Unauthorized("Invalid manager PIN.");
        }

        var kot = await db.KotHeaders.FirstOrDefaultAsync(x => x.KotHeaderId == request.KotId, cancellationToken);
        if (kot is null)
        {
            return NotFound("KOT not found.");
        }

        kot.KotEventType = "Addendum";
        db.ReprintLogs.Add(new ReprintLog
        {
            DocumentType = "KOT",
            DocumentId = request.KotId,
            ReprintedBy = request.UserId,
            Reason = request.Reason
        });

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { status = "ReprintLogged", watermark = "REPRINT" });
    }
}
