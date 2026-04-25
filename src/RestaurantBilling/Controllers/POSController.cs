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

namespace RestaurantBilling.Controllers;

[Authorize]
[Route("pos")]
public class POSController(
    IBillingCalculatorService calculatorService,
    INumberGeneratorService numberGeneratorService,
    IStockService stockService,
    IAuditService auditService,
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
            .Select(x => new { x.OutletId, x.TableName })
            .FirstOrDefaultAsync(cancellationToken);
        if (settledBill is not null)
        {
            await SetTableOccupiedStateAsync(settledBill.OutletId, settledBill.TableName, false, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        await alertHub.Clients.All.SendAsync("DashboardRefresh", "bill", cancellationToken);
        return Ok(new { billId = result.Value });
    }

    [HttpPost("hold")]
    public async Task<IActionResult> Hold([FromBody] HoldBillRequest request, CancellationToken cancellationToken)
    {
        if (request is null) return BadRequest("Invalid request body.");
        if (await IsBusinessDateLocked(request.OutletId, request.BusinessDate, cancellationToken))
        {
            return Conflict("Business date is locked.");
        }
        var billNo = await numberGeneratorService.GenerateAsync(request.OutletId, NumberSeriesKey.Bill, cancellationToken);
        var bill = new Bill(request.OutletId, billNo, request.BusinessDate, request.BillType);
        if (!string.IsNullOrWhiteSpace(request.TableName))
            bill.SetTableName(request.TableName);

        foreach (var item in request.Items)
        {
            var calcLine = calculatorService.Calculate([new BillItemInput(item.ItemId, item.ItemName, item.Qty, item.Rate, item.DiscountAmount, item.TaxPercent, item.IsTaxInclusive, item.TaxType)], 0, 0, false, false).Lines.First();
            bill.AddItem(new BillItem(calcLine.ItemId, calcLine.ItemName, calcLine.Qty, calcLine.Rate, calcLine.DiscountAmount, calcLine.TaxAmount));
        }

        bill.SetServiceCharge(request.ServiceChargeAmount, request.ServiceChargeOptIn);
        await SetTableOccupiedStateAsync(request.OutletId, request.TableName, true, cancellationToken);
        db.Bills.Add(bill);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { billId = bill.BillId, billNo = bill.BillNo, tableName = bill.TableName, status = "Draft" });
    }

    [HttpPost("settle-existing/{billId:long}")]
    public async Task<IActionResult> SettleExisting(long billId, [FromBody] SettleExistingRequest request, CancellationToken cancellationToken)
    {
        var bill = await db.Bills.Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.BillId == billId && x.OutletId == request.OutletId, cancellationToken);
        if (bill is null) return NotFound("Bill not found.");
        if (bill.Status != BillStatus.Draft) return BadRequest("Bill is not in Draft state.");
        if (await IsBusinessDateLocked(request.OutletId, bill.BusinessDate, cancellationToken))
            return Conflict("Business date is locked.");

        var payments = request.Payments
            .Select(p => new Payment(p.Mode, p.Amount, p.ReferenceNo, p.CardLast4, p.UpiTxnId))
            .ToList();
        bill.Settle(payments);
        await SetTableOccupiedStateAsync(request.OutletId, bill.TableName, false, cancellationToken);

        await stockService.DeductSaleStockAsync(request.OutletId, bill.BusinessDate, bill.Items.ToList(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await auditService.LogAsync(request.OutletId, 0, "SettleExisting", nameof(Bill), bill.BillId.ToString(),
            "{\"status\":\"Draft\"}", "{\"status\":\"Paid\"}",
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), cancellationToken);

        await alertHub.Clients.All.SendAsync("DashboardRefresh", "bill", cancellationToken);
        return Ok(new { billId = bill.BillId, billNo = bill.BillNo, grandTotal = bill.GrandTotal });
    }

    [HttpGet("held-bill/{billId:long}")]
    public async Task<IActionResult> HeldBill(long billId, [FromQuery] int outletId, CancellationToken cancellationToken)
    {
        var row = await db.Bills
            .AsNoTracking()
            .Where(x => x.BillId == billId && x.OutletId == outletId && x.Status == BillStatus.Draft)
            .Select(x => new
            {
                x.BillId,
                x.BillNo,
                x.GrandTotal,
                x.TableName,
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
        return Ok(row);
    }

    [HttpGet("held-bills-detail")]
    public async Task<IActionResult> HeldBillsDetail([FromQuery] int outletId, CancellationToken cancellationToken)
    {
        var bills = await db.Bills
            .Where(x => x.OutletId == outletId && x.Status == BillStatus.Draft)
            .OrderByDescending(x => x.BillDate)
            .Take(30)
            .Select(x => new
            {
                x.BillId,
                x.BillNo,
                x.GrandTotal,
                x.TableName,
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
        return Ok(bills);
    }

    [HttpGet("recall")]
    public async Task<IActionResult> Recall([FromQuery] int outletId, CancellationToken cancellationToken)
    {
        var drafts = await db.Bills
            .Where(x => x.OutletId == outletId && x.Status == BillStatus.Draft)
            .OrderByDescending(x => x.BillDate)
            .Select(x => new { x.BillId, x.BillNo, x.BillDate, x.GrandTotal, x.BillType })
            .ToListAsync(cancellationToken);
        return Ok(drafts);
    }

    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog([FromQuery] int outletId, CancellationToken cancellationToken)
    {
        var categories = await db.Categories
            .Where(x => x.OutletId == outletId && x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.SortOrder)
            .Select(x => new { id = x.CategoryId, name = x.CategoryName })
            .ToListAsync(cancellationToken);

        var imageBasePath = await db.RestaurantSettings
            .Where(x => x.OutletId == outletId && x.SettingKey == "MenuImageBasePath")
            .Select(x => x.SettingValue)
            .FirstOrDefaultAsync(cancellationToken) ?? "/images/menu";

        var imageMap = await db.RestaurantSettings
            .Where(x => x.OutletId == outletId && x.SettingKey.StartsWith("MenuImage:"))
            .Select(x => new { x.SettingKey, x.SettingValue })
            .ToDictionaryAsync(
                x => x.SettingKey["MenuImage:".Length..],
                x => x.SettingValue ?? string.Empty,
                cancellationToken);

        var items = await db.Items
            .Where(x => x.OutletId == outletId && x.IsActive && !x.IsDeleted)
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

        var payload = items.Select(i => new
        {
            i.id,
            i.name,
            i.categoryId,
            i.price,
            i.taxPercent,
            imageUrl = !string.IsNullOrWhiteSpace(i.imagePath)
                ? i.imagePath
                : imageMap.TryGetValue(i.name, out var fileName) && !string.IsNullOrWhiteSpace(fileName)
                ? $"{imageBasePath.TrimEnd('/')}/{fileName}"
                : string.Empty
        });

        return Ok(new { categories, items = payload });
    }

    private async Task<bool> IsBusinessDateLocked(int outletId, DateOnly businessDate, CancellationToken cancellationToken)
        => await db.DayCloseReports.AnyAsync(x => x.OutletId == outletId && x.BusinessDate == businessDate && x.IsLocked, cancellationToken);

    private async Task SetTableOccupiedStateAsync(int outletId, string? tableName, bool isOccupied, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tableName)) return;
        var normalized = tableName.Trim();
        var table = await db.TableMasters
            .FirstOrDefaultAsync(
                x => x.OutletId == outletId
                     && x.IsActive
                     && !x.IsDeleted
                     && x.TableName == normalized,
                cancellationToken);
        if (table is null) return;
        table.IsOccupied = isOccupied;
        table.UpdatedAtUtc = DateTime.UtcNow;
    }
}
