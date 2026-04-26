using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using IServices;
using Entities.Audit;
using Entities.Enums;
using Entities.Kitchen;
using Data.Persistence;
using RestaurantBilling.Hubs;
using RestaurantBilling.Models.Kitchen;

namespace RestaurantBilling.Controllers;

[Authorize]
[Route("kot")]
public class KOTController(
    AppDbContext db,
    INumberGeneratorService numberGeneratorService,
    IHubContext<KdsHub> kdsHubContext,
    IHubContext<AlertHub> alertHub) : Controller
{
    [HttpGet("display")]
    public IActionResult Display([FromQuery] int station = 1)
        => RedirectToAction(nameof(KdsScreen), new { station });

    [HttpGet("kots")]
    public IActionResult Kots()
        => RedirectToAction(nameof(KotList));

    [HttpGet("kotlist")]
    [HttpGet("/kot")]
    public IActionResult KotList()
    {
        ViewBag.UseDataTables = true;
        return View("KotList");
    }

    [HttpGet("screen")]
    [HttpGet("kdsscreen")]
    public IActionResult KdsScreen([FromQuery] int station = 1)
    {
        ViewData["Station"] = station;
        ViewBag.UseSignalR = true;
        return View();
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateKotRequest request, CancellationToken cancellationToken)
    {
        var bill = await db.Bills.Include(x => x.Items).FirstOrDefaultAsync(x => x.BillId == request.BillId, cancellationToken);
        if (bill is null)
        {
            return NotFound("Bill not found.");
        }

        var hasPreviousKotForBill = await db.KotHeaders
            .AnyAsync(x => x.OutletId == request.OutletId && x.BillId == request.BillId, cancellationToken);

        var defaultStationId = await db.KitchenStations
            .Where(x => x.OutletId == request.OutletId)
            .OrderBy(x => x.SortOrder)
            .Select(x => x.KitchenStationId)
            .FirstOrDefaultAsync(cancellationToken);
        if (defaultStationId <= 0)
        {
            defaultStationId = 1;
        }

        var kotNo = await numberGeneratorService.GenerateAsync(request.OutletId, NumberSeriesKey.KOT, cancellationToken);
        var header = new KotHeader
        {
            OutletId = request.OutletId,
            BillId = request.BillId,
            KotNo = kotNo,
            KotDate = DateTime.UtcNow,
            BusinessDate = bill.BusinessDate,
            KitchenStationId = defaultStationId,
            KotEventType = hasPreviousKotForBill ? "AddOn" : "New",
            Status = "Pending"
        };

        db.KotHeaders.Add(header);
        await db.SaveChangesAsync(cancellationToken);

        var kotItems = bill.Items.Select(i => new KotItem
        {
            KotHeaderId = header.KotHeaderId,
            ItemId = i.ItemId,
            ItemNameSnapshot = i.ItemNameSnapshot,
            Qty = i.Qty
        });
        db.KotItems.AddRange(kotItems);

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { kotIds = new[] { header.KotHeaderId }, reused = false });
    }

    [Authorize(Roles = "Admin,Kitchen,Captain")]
    [HttpPost("status")]
    public async Task<IActionResult> UpdateStatus([FromBody] KotStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        var allowed = new[] { "Pending", "Preparing", "Ready", "Served", "Cancelled" };
        if (!allowed.Contains(request.Status))
        {
            return BadRequest("Invalid status.");
        }

        var kot = await db.KotHeaders.FirstOrDefaultAsync(x => x.KotHeaderId == request.KotId && x.OutletId == request.OutletId, cancellationToken);
        if (kot is null)
        {
            return NotFound("KOT not found.");
        }

        kot.Status = request.Status;
        if (request.Status == "Served")
        {
            kot.ServedAtUtc ??= DateTime.UtcNow;
            kot.ServedByUserId ??= User?.Identity?.Name;
        }
        await db.SaveChangesAsync(cancellationToken);

        await kdsHubContext.Clients.Group($"station:{kot.KitchenStationId}")
            .SendAsync("KotStatusUpdated", new
            {
                kotId = kot.KotHeaderId,
                status = kot.Status,
                stationId = kot.KitchenStationId
            }, cancellationToken);

        await alertHub.Clients.All.SendAsync("DashboardRefresh", "kot", cancellationToken);
        return Ok(new { status = "Updated" });
    }

    [HttpPost("mark-printed")]
    public async Task<IActionResult> MarkPrinted([FromBody] MarkKotPrintedRequest request, CancellationToken cancellationToken)
    {
        if (request.KotIds is null || request.KotIds.Count == 0) return BadRequest("No KOT ids provided.");

        var kotIds = request.KotIds.Distinct().ToList();
        var headers = await db.KotHeaders
            .Where(x => x.OutletId == request.OutletId && kotIds.Contains(x.KotHeaderId))
            .ToListAsync(cancellationToken);
        if (headers.Count == 0) return NotFound("KOT not found.");

        var now = DateTime.UtcNow;
        foreach (var h in headers)
        {
            h.KotPrintedAtUtc ??= now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { updated = headers.Count });
    }

    [HttpGet("kots-data")]
    public async Task<IActionResult> KotsData([FromQuery] int outletId, [FromQuery] int? stationId, CancellationToken cancellationToken)
    {
        var query = db.KotHeaders.Where(x => x.OutletId == outletId);
        if (stationId.HasValue)
        {
            query = query.Where(x => x.KitchenStationId == stationId.Value);
        }

        var headers = await query
            .OrderByDescending(x => x.KotDate)
            .Take(300)
            .Select(x => new
            {
                x.KotHeaderId,
                x.KotNo,
                x.BillId,
                x.KitchenStationId,
                x.KotEventType,
                x.Status,
                x.KotDate
            })
            .ToListAsync(cancellationToken);

        if (headers.Count == 0)
        {
            return Ok(Array.Empty<object>());
        }

        var headerIds = headers.Select(x => x.KotHeaderId).ToList();
        var itemLookup = await db.KotItems
            .AsNoTracking()
            .Where(x => headerIds.Contains(x.KotHeaderId) && !x.IsVoid)
            .OrderBy(x => x.KotItemId)
            .Select(x => new
            {
                x.KotHeaderId,
                x.ItemNameSnapshot,
                x.Qty,
                x.SpecialInstructions
            })
            .ToListAsync(cancellationToken);

        var rows = headers.Select(h =>
        {
            var items = itemLookup
                .Where(i => i.KotHeaderId == h.KotHeaderId)
                .Select(i => new
                {
                    itemName = i.ItemNameSnapshot,
                    qty = i.Qty,
                    note = i.SpecialInstructions
                })
                .ToList();
            return new
            {
                h.KotHeaderId,
                h.KotNo,
                h.KitchenStationId,
                kotEventType = h.KotEventType,
                h.Status,
                kotDate = h.KotDate.ToString("dd-MMM-yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                kotDateIso = h.KotDate,
                itemCount = items.Count,
                items
            };
        });

        return Ok(rows);
    }
}
