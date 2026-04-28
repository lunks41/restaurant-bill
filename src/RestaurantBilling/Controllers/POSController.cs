using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Services.Billing.Commands.SettleBill;
using Models.Billing;
using IServices;
using Entities.Enums;
using Entities.Sales;
using Data.Persistence;
using RestaurantBilling.Hubs;
using RestaurantBilling.Models.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using System.Text.RegularExpressions;

namespace RestaurantBilling.Controllers;

[Authorize]
[Route("pos")]
public class POSController(
    IBillingCalculatorService calculatorService,
    INumberGeneratorService numberGeneratorService,
    IStockService stockService,
    IAuditService auditService,
    IWebHostEnvironment env,
    AppDbContext db,
    IMediator mediator,
    IHubContext<AlertHub> alertHub) : Controller
{
    [HttpGet]
    public IActionResult Pos()
    {
        ViewBag.UseSelect2 = true;
        return View("~/Views/POS/Pos.cshtml");
    }

    [HttpGet("order/{billId:long}")]
    public IActionResult PosOrder(long billId)
    {
        ViewBag.UseSelect2 = true;
        return View("~/Views/POS/Pos.cshtml");
    }

    [HttpPost("preview")]
    public IActionResult Preview([FromBody] List<BillItemInput> items)
    {
        var result = calculatorService.Calculate(items, 0m, 0m, false, false);
        return Ok(result);
    }

    [HttpPost("preview-sample")]
    public IActionResult PreviewSample()
    {
        var result = calculatorService.Calculate(
        [
            new BillItemInput(1, "Sample Food", 1, 100, 0, 5, false, TaxType.GST)
        ], 0, 0, false, false);
        return Ok(result);
    }

    [HttpPost("settle")]
    public async Task<IActionResult> Settle([FromBody] SettleBillCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var settledBill = await db.Bills
            .AsNoTracking()
            .Where(x => x.BillId == result.Value)
            .Select(x => new { x.TableName })
            .FirstOrDefaultAsync(cancellationToken);
        if (settledBill is not null)
        {
            await SetTableOccupiedStateAsync(settledBill.TableName, false, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        await alertHub.Clients.All.SendAsync("DashboardRefresh", "bill", cancellationToken);
        return Ok(new { billId = result.Value });
    }

    [HttpPost("hold")]
    public async Task<IActionResult> Hold([FromBody] HoldBillRequest request, CancellationToken cancellationToken)
    {
        if (request is null) return BadRequest("Invalid request body.");
        if (await IsBusinessDateLocked(request.BusinessDate, cancellationToken))
        {
            return Conflict("Business date is locked.");
        }
        var billNo = await numberGeneratorService.GenerateAsync(NumberSeriesKey.Bill, cancellationToken);
        var bill = new Bill(billNo, request.BusinessDate, request.BillType);
        if (!string.IsNullOrWhiteSpace(request.TableName))
            bill.SetTableName(request.TableName);
        bill.SetCustomerInfo(request.CustomerName, request.Phone);

        var calcInput = request.Items
            .Select(item => new BillItemInput(
                item.ItemId,
                item.ItemName,
                item.Qty,
                item.Rate,
                item.DiscountAmount,
                item.TaxPercent,
                item.IsTaxInclusive,
                item.TaxType))
            .ToList();
        var computed = calculatorService.Calculate(
            calcInput,
            request.BillLevelDiscount,
            request.ServiceChargeAmount,
            request.ServiceChargeOptIn,
            false);

        foreach (var line in computed.Lines)
        {
            bill.AddItem(new BillItem(
                line.ItemId,
                line.ItemName,
                line.Qty,
                line.Rate,
                line.DiscountAmount,
                line.TaxAmount));
        }

        bill.SetServiceCharge(request.ServiceChargeAmount, request.ServiceChargeOptIn);
        await SetTableOccupiedStateAsync(request.TableName, true, cancellationToken);
        db.Bills.Add(bill);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            billId = bill.BillId,
            billNo = bill.BillNo,
            tableName = bill.TableName,
            customerName = bill.CustomerName,
            phone = bill.Phone,
            status = "Draft"
        });
    }

    [HttpPost("settle-existing/{billId:long}")]
    public async Task<IActionResult> SettleExisting(long billId, [FromBody] SettleExistingRequest request, CancellationToken cancellationToken)
    {
        var bill = await db.Bills.Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.BillId == billId, cancellationToken);
        if (bill is null) return NotFound("Bill not found.");
        if (bill.Status != BillStatus.Draft) return BadRequest("Bill is not in Draft state.");
        if (await IsBusinessDateLocked(bill.BusinessDate, cancellationToken))
            return Conflict("Business date is locked.");

        var payments = request.Payments
            .Select(p => new Payment(p.Mode, p.Amount, p.ReferenceNo, p.CardLast4, p.UpiTxnId))
            .ToList();
        bill.SetCustomerInfo(request.CustomerName, request.Phone);
        bill.Settle(payments);
        await SetTableOccupiedStateAsync(bill.TableName, false, cancellationToken);

        await stockService.DeductSaleStockAsync(bill.BusinessDate, bill.Items.ToList(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await auditService.LogAsync(0, "SettleExisting", nameof(Bill), bill.BillId.ToString(),
            "{\"status\":\"Draft\"}", "{\"status\":\"Paid\"}",
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), cancellationToken);

        await alertHub.Clients.All.SendAsync("DashboardRefresh", "bill", cancellationToken);
        return Ok(new { billId = bill.BillId, billNo = bill.BillNo, grandTotal = bill.GrandTotal });
    }

    [HttpPost("update-draft/{billId:long}")]
    public async Task<IActionResult> UpdateDraft(long billId, [FromBody] UpdateDraftBillRequest request, CancellationToken cancellationToken)
    {
        var bill = await db.Bills
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.BillId == billId, cancellationToken);
        if (bill is null) return NotFound("Bill not found.");
        if (bill.Status != BillStatus.Draft) return BadRequest("Only draft bills can be updated.");
        if (request.Items is null || request.Items.Count == 0) return BadRequest("At least one item is required.");

        var calcInput = request.Items
            .Select(item => new BillItemInput(
                item.ItemId,
                item.ItemName,
                item.Qty,
                item.Rate,
                item.DiscountAmount,
                item.TaxPercent,
                item.IsTaxInclusive,
                item.TaxType))
            .ToList();
        var computed = calculatorService.Calculate(
            calcInput,
            request.BillLevelDiscount,
            request.ServiceChargeAmount,
            request.ServiceChargeOptIn,
            false);

        db.BillItems.RemoveRange(bill.Items);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var line in computed.Lines)
        {
            bill.AddItem(new BillItem(
                line.ItemId,
                line.ItemName,
                line.Qty,
                line.Rate,
                line.DiscountAmount,
                line.TaxAmount));
        }
        bill.SetServiceCharge(request.ServiceChargeAmount, request.ServiceChargeOptIn);
        bill.SetTableName(request.TableName);
        bill.SetCustomerInfo(request.CustomerName, request.Phone);

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { billId = bill.BillId, billNo = bill.BillNo, grandTotal = bill.GrandTotal, status = "Updated" });
    }

    [HttpPost("cancel-draft/{billId:long}")]
    public async Task<IActionResult> CancelDraft(long billId, [FromBody] CancelDraftRequest request, CancellationToken cancellationToken)
    {
        var bill = await db.Bills
            .FirstOrDefaultAsync(x => x.BillId == billId, cancellationToken);
        if (bill is null) return NotFound("Bill not found.");
        if (bill.Status != BillStatus.Draft) return BadRequest("Only draft bills can be cancelled.");
        if (await IsBusinessDateLocked(bill.BusinessDate, cancellationToken))
            return Conflict("Business date is locked.");

        bill.Cancel();
        await SetTableOccupiedStateAsync(bill.TableName, false, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await alertHub.Clients.All.SendAsync("DashboardRefresh", "bill", cancellationToken);
        return Ok(new { billId = bill.BillId, billNo = bill.BillNo, status = "Cancelled" });
    }

    [HttpGet("held-bill/{billId:long}")]
    public async Task<IActionResult> HeldBill(long billId, [FromQuery] int outletId, CancellationToken cancellationToken)
    {
        var row = await db.Bills
            .AsNoTracking()
            .Where(x => x.BillId == billId && x.Status == BillStatus.Draft)
            .Select(x => new
            {
                x.BillId,
                x.BillNo,
                x.GrandTotal,
                x.DiscountAmount,
                x.TableName,
                x.CustomerName,
                x.Phone,
                billType = x.BillType.ToString(),
                billTime = x.BillDate.ToString("HH:mm"),
                items = x.Items.Select(i => new
                {
                    i.ItemId,
                    name = i.ItemNameSnapshot,
                    i.Qty,
                    rate = i.RateSnapshot,
                    taxPercent = i.TaxAmount > 0 ? 5 : 0
                })
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null) return NotFound("Draft bill not found.");

        var kotStatuses = await db.KotHeaders
            .AsNoTracking()
            .Where(x => x.BillId == billId && x.Status != "Cancelled")
            .OrderByDescending(x => x.KotDate)
            .Select(x => x.Status)
            .ToListAsync(cancellationToken);

        var latestKotStatus = kotStatuses.FirstOrDefault();
        var hasAnyKot = kotStatuses.Count > 0;
        var hasActiveKot = kotStatuses.Any(s => s != "Served");
        var hasPendingKot = !hasAnyKot;

        return Ok(new
        {
            row.BillId,
            row.BillNo,
            row.GrandTotal,
            row.DiscountAmount,
            row.TableName,
            row.CustomerName,
            row.Phone,
            row.billType,
            row.billTime,
            row.items,
            kotStatus = latestKotStatus,
            hasAnyKot,
            hasActiveKot,
            hasPendingKot
        });
    }

    [HttpGet("held-bills-detail")]
    public async Task<IActionResult> HeldBillsDetail([FromQuery] int outletId, CancellationToken cancellationToken)
    {
        var bills = await db.Bills
            .Where(x => x.Status == BillStatus.Draft)
            .OrderByDescending(x => x.BillDate)
            .Take(30)
            .Select(x => new
            {
                x.BillId,
                x.BillNo,
                x.GrandTotal,
                x.DiscountAmount,
                x.TableName,
                x.CustomerName,
                x.Phone,
                billType = x.BillType.ToString(),
                billTime = x.BillDate.ToString("HH:mm"),
                items = x.Items.Select(i => new
                {
                    i.ItemId,
                    name = i.ItemNameSnapshot,
                    i.Qty,
                    rate = i.RateSnapshot,
                    taxPercent = i.TaxAmount > 0 ? 5 : 0
                })
            })
            .ToListAsync(cancellationToken);

        if (bills.Count == 0) return Ok(bills);

        var billIds = bills.Select(x => x.BillId).Distinct().ToList();
        var kotRows = await db.KotHeaders
            .AsNoTracking()
            .Where(x => x.BillId.HasValue && billIds.Contains(x.BillId.Value) && x.Status != "Cancelled")
            .Select(x => new { billId = x.BillId!.Value, x.Status, x.KotDate })
            .ToListAsync(cancellationToken);

        var kotByBill = kotRows
            .GroupBy(x => x.billId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var ordered = g.OrderByDescending(x => x.KotDate).ToList();
                    var latest = ordered.FirstOrDefault();
                    var hasAny = ordered.Count > 0;
                    var hasActive = ordered.Any(x => x.Status != "Served");
                    return new
                    {
                        latestStatus = latest?.Status,
                        hasAnyKot = hasAny,
                        hasActiveKot = hasActive,
                        hasPendingKot = !hasAny
                    };
                });

        var payload = bills.Select(b =>
        {
            var hasKot = kotByBill.TryGetValue(b.BillId, out var k);
            return new
            {
                b.BillId,
                b.BillNo,
                b.GrandTotal,
                b.DiscountAmount,
                b.TableName,
                b.CustomerName,
                b.Phone,
                b.billType,
                b.billTime,
                b.items,
                kotStatus = hasKot ? k!.latestStatus : null,
                hasAnyKot = hasKot && k!.hasAnyKot,
                hasActiveKot = hasKot && k!.hasActiveKot,
                hasPendingKot = hasKot ? k!.hasPendingKot : true
            };
        });

        return Ok(payload);
    }

    [HttpGet("recall")]
    public async Task<IActionResult> Recall([FromQuery] int outletId, CancellationToken cancellationToken)
    {
        var drafts = await db.Bills
            .Where(x => x.Status == BillStatus.Draft)
            .OrderByDescending(x => x.BillDate)
            .Select(x => new { x.BillId, x.BillNo, x.BillDate, x.GrandTotal, x.BillType })
            .ToListAsync(cancellationToken);
        return Ok(drafts);
    }

    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog([FromQuery] int outletId, CancellationToken cancellationToken)
    {
        var activeItemCategoryIds = await db.Items
            .Where(x => !x.IsDeleted)
            .Select(x => x.CategoryId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var categories = await db.Categories
            .Where(x => !x.IsDeleted && activeItemCategoryIds.Contains(x.CategoryId))
            .OrderBy(x => x.SortOrder)
            .Select(x => new { id = x.CategoryId, name = x.CategoryName })
            .ToListAsync(cancellationToken);

        var rawItems = await db.Items
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.ItemName)
            .Select(x => new
            {
                id = x.ItemId,
                name = x.ItemName,
                categoryId = x.CategoryId,
                price = x.SalePrice,
                taxPercent = x.GstPercent,
                imagePath = x.ImagePath
            })
            .ToListAsync(cancellationToken);

        var availableImageNames = GetAvailableMenuImageNames(env);
        var items = rawItems.Select(x => new
        {
            x.id,
            x.name,
            x.categoryId,
            x.price,
            x.taxPercent,
            imageUrl = ResolveCatalogImageUrl(x.imagePath, x.name, availableImageNames)
        });

        return Ok(new { categories, items });
    }

    private static string? ResolveCatalogImageUrl(string? imagePath, string itemName, HashSet<string> availableImageNames)
    {
        if (!string.IsNullOrWhiteSpace(imagePath))
            return imagePath;

        var slug = Slugify(itemName);
        if (string.IsNullOrWhiteSpace(slug))
            return null;

        return availableImageNames.Contains(slug)
            ? $"/images/menu/items/{slug}.jpg"
            : null;
    }

    private static HashSet<string> GetAvailableMenuImageNames(IWebHostEnvironment env)
    {
        var webRoot = env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var folder = Path.Combine(webRoot, "images", "menu", "items");
        if (!Directory.Exists(folder))
            return [];

        return Directory
            .EnumerateFiles(folder)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var lower = value.Trim().ToLowerInvariant();
        var compact = Regex.Replace(lower, @"[^a-z0-9]+", "-");
        return compact.Trim('-');
    }

    private async Task<bool> IsBusinessDateLocked(DateOnly businessDate, CancellationToken cancellationToken)
        => await db.DayCloseReports.AnyAsync(x => x.BusinessDate == businessDate && x.IsLocked, cancellationToken);

    private async Task SetTableOccupiedStateAsync(string? tableName, bool isOccupied, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tableName)) return;
        var normalized = tableName.Trim();
        var table = await db.DiningTables
            .FirstOrDefaultAsync(
                x => x.IsActive
                     && !x.IsDeleted
                     && x.TableName == normalized,
                cancellationToken);
        if (table is null) return;
        table.IsOccupied = isOccupied;
        table.UpdatedAtUtc = DateTime.UtcNow;
    }
}
