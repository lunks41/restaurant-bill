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
    private const string ItemStatusPrefix = "status:";
    private static readonly string[] StatusPriority = ["Pending", "Preparing", "Served", "Cancelled"];

    private static string NormalizeStatus(string? rawStatus)
    {
        if (string.IsNullOrWhiteSpace(rawStatus)) return "Pending";
        if (rawStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase)) return "Pending";
        if (rawStatus.Equals("Preparing", StringComparison.OrdinalIgnoreCase)) return "Preparing";
        if (rawStatus.Equals("Served", StringComparison.OrdinalIgnoreCase)) return "Served";
        if (rawStatus.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)) return "Cancelled";
        // Backward compatibility for legacy "Ready" state.
        return "Preparing";
    }

    private static string EncodeItemStatus(string status) => $"{ItemStatusPrefix}{NormalizeStatus(status)}";

    private static string DecodeItemStatus(string? specialInstructions, string fallbackStatus)
    {
        if (!string.IsNullOrWhiteSpace(specialInstructions) &&
            specialInstructions.StartsWith(ItemStatusPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeStatus(specialInstructions[ItemStatusPrefix.Length..]);
        }

        return NormalizeStatus(fallbackStatus);
    }

    private static string ResolveHeaderStatus(IEnumerable<string> itemStatuses)
    {
        var statuses = itemStatuses.Select(NormalizeStatus).ToList();
        if (statuses.Count == 0) return "Pending";
        if (statuses.Any(x => x == "Pending")) return "Pending";
        if (statuses.Any(x => x == "Preparing")) return "Preparing";
        if (statuses.All(x => x is "Served" or "Cancelled"))
        {
            return statuses.Any(x => x == "Served") ? "Served" : "Cancelled";
        }
        if (statuses.All(x => x == "Served")) return "Served";
        if (statuses.All(x => x == "Cancelled")) return "Cancelled";
        return "Preparing";
    }

    private static bool CanMoveTo(string currentStatus, string nextStatus) => (currentStatus, nextStatus) switch
    {
        ("Pending", "Preparing") => true,
        ("Pending", "Cancelled") => true,
        ("Preparing", "Served") => true,
        ("Preparing", "Cancelled") => true,
        _ => false
    };

    [HttpGet("display")]
    public IActionResult Display()
        => RedirectToAction(nameof(KdsScreen));

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
    public IActionResult KdsScreen()
    {
        ViewBag.UseSignalR = true;
        return View();
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateKotRequest request, CancellationToken cancellationToken)
    {
        var bill = await db.Bills
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.BillId == request.BillId, cancellationToken);
        if (bill is null)
        {
            return NotFound("Bill not found.");
        }

        var existingHeaders = await db.KotHeaders
            .Where(x => x.BillId == request.BillId)
            .OrderByDescending(x => x.KotDate)
            .ToListAsync(cancellationToken);

        var activeHeaderIds = existingHeaders
            .Where(x => x.Status != "Cancelled")
            .Select(x => x.KotHeaderId)
            .ToList();

        var sentQtyByItem = await db.KotItems
            .Where(x => !x.IsVoid && activeHeaderIds.Contains(x.KotHeaderId))
            .GroupBy(x => x.ItemId)
            .Select(g => new
            {
                ItemId = g.Key,
                Qty = g.Sum(x => x.Qty)
            })
            .ToDictionaryAsync(x => x.ItemId, x => x.Qty, cancellationToken);

        var pendingItems = bill.Items
            .GroupBy(x => x.ItemId)
            .Select(group =>
            {
                var totalQty = group.Sum(x => x.Qty);
                sentQtyByItem.TryGetValue(group.Key, out var sentQty);
                var qtyToSend = totalQty - sentQty;
                var snapshot = group.Last().ItemNameSnapshot;
                return new
                {
                    ItemId = group.Key,
                    ItemNameSnapshot = snapshot,
                    QtyToSend = qtyToSend
                };
            })
            .Where(x => x.QtyToSend > 0)
            .ToList();

        var lastHeader = existingHeaders.FirstOrDefault();
        if (pendingItems.Count == 0)
        {
            return Ok(new
            {
                kotIds = lastHeader is null ? Array.Empty<long>() : new[] { lastHeader.KotHeaderId },
                reused = true
            });
        }

        var hasPreviousKotForBill = existingHeaders.Count > 0;

        var defaultStationId = await db.KitchenStations
            .AsQueryable()
            .OrderBy(x => x.SortOrder)
            .Select(x => x.KitchenStationId)
            .FirstOrDefaultAsync(cancellationToken);
        if (defaultStationId <= 0)
        {
            defaultStationId = 1;
        }

        var header = existingHeaders.FirstOrDefault(x => x.Status != "Cancelled");
        var reused = header is not null;

        if (header is null)
        {
            var kotNo = await numberGeneratorService.GenerateAsync(NumberSeriesKey.KOT, cancellationToken);
            header = new KotHeader
            {
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
        }
        else
        {
            header.KotEventType = "AddOn";
            header.KotDate = DateTime.UtcNow;
            db.KotHeaders.Update(header);
        }

        var kotItems = pendingItems.Select(i => new KotItem
        {
            KotHeaderId = header.KotHeaderId,
            ItemId = i.ItemId,
            ItemNameSnapshot = i.ItemNameSnapshot,
            Qty = i.QtyToSend,
            SpecialInstructions = EncodeItemStatus("Pending")
        });
        db.KotItems.AddRange(kotItems);

        var allItemStatuses = await db.KotItems
            .Where(x => x.KotHeaderId == header.KotHeaderId && !x.IsVoid)
            .Select(x => x.SpecialInstructions)
            .ToListAsync(cancellationToken);
        header.Status = ResolveHeaderStatus(allItemStatuses.Select(x => DecodeItemStatus(x, header.Status)));

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { kotIds = new[] { header.KotHeaderId }, reused });
    }

    [Authorize(Roles = "Admin,Kitchen,Captain")]
    [HttpPost("status")]
    public async Task<IActionResult> UpdateStatus([FromBody] KotStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        var allowed = new[] { "Preparing", "Served", "Cancelled" };
        if (!allowed.Contains(request.Status))
        {
            return BadRequest("Invalid status.");
        }

        var kot = await db.KotHeaders.FirstOrDefaultAsync(x => x.KotHeaderId == request.KotId, cancellationToken);
        if (kot is null)
        {
            return NotFound("KOT not found.");
        }

        var itemRows = await db.KotItems
            .Where(x => x.KotHeaderId == kot.KotHeaderId && !x.IsVoid)
            .OrderBy(x => x.KotItemId)
            .ToListAsync(cancellationToken);

        foreach (var item in itemRows)
        {
            var currentItemStatus = DecodeItemStatus(item.SpecialInstructions, kot.Status);
            if (CanMoveTo(currentItemStatus, request.Status))
            {
                item.SpecialInstructions = EncodeItemStatus(request.Status);
            }
        }

        kot.Status = ResolveHeaderStatus(itemRows.Select(x => DecodeItemStatus(x.SpecialInstructions, kot.Status)));
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
            .Where(x => kotIds.Contains(x.KotHeaderId))
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
    public async Task<IActionResult> KotsData([FromQuery] int outletId, CancellationToken cancellationToken)
    {
        var query = db.KotHeaders.AsQueryable();

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
        var billIds = headers.Where(x => x.BillId.HasValue).Select(x => x.BillId!.Value).Distinct().ToList();
        Dictionary<long, (string BillNo, string? TableName)> billLookup = billIds.Count == 0
            ? new Dictionary<long, (string BillNo, string? TableName)>()
            : await db.Bills
                .AsNoTracking()
                .Where(x => billIds.Contains(x.BillId))
                .Select(x => new { x.BillId, x.BillNo, x.TableName })
                .ToDictionaryAsync(
                    x => x.BillId,
                    x => (BillNo: x.BillNo, TableName: x.TableName),
                    cancellationToken);

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
            billLookup.TryGetValue(h.BillId ?? 0, out var billMeta);
            var items = itemLookup
                .Where(i => i.KotHeaderId == h.KotHeaderId)
                .Select(i => new
                {
                    itemName = i.ItemNameSnapshot,
                    qty = i.Qty,
                    note = i.SpecialInstructions,
                    status = DecodeItemStatus(i.SpecialInstructions, h.Status)
                })
                .ToList();
            return new
            {
                h.KotHeaderId,
                h.KotNo,
                h.BillId,
                billNo = billMeta.BillNo,
                tableName = billMeta.TableName,
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
