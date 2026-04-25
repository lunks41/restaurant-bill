using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Services.Billing.Commands.SettleBill;
using Models.Billing;
using IServices;
using Entities.Audit;
using Entities.Enums;
using Entities.Sales;
using Data.Persistence;
using RestaurantBilling.Hubs;
using RestaurantBilling.Models.Billing;
using Microsoft.EntityFrameworkCore;

namespace RestaurantBilling.Controllers;

[Authorize]
[Route("billing")]
public class BillingController(
    IBillingCalculatorService calculatorService,
    INumberGeneratorService numberGeneratorService,
    IStockService stockService,
    IAuditService auditService,
    AppDbContext db,
    IMediator mediator,
    IHubContext<AlertHub> alertHub) : Controller
{
    [HttpGet("dinein")]
    public IActionResult DineIn()
    {
        ViewBag.UseSelect2 = true;
        return View("DineIn");
    }

    private async Task<bool> IsBusinessDateLocked(int outletId, DateOnly businessDate, CancellationToken cancellationToken)
        => await db.DayCloseReports.AnyAsync(x => x.OutletId == outletId && x.BusinessDate == businessDate && x.IsLocked, cancellationToken);

    [HttpGet("pos")]
    public IActionResult Pos() => RedirectToAction(nameof(DineIn));

    [HttpGet("takeaway")]
    public IActionResult Takeaway()
    {
        ViewBag.UseSelect2 = true;
        return View();
    }

    [HttpGet("quote")]
    public IActionResult Quote()
    {
        ViewBag.UseDataTables = true;
        return View();
    }

    [HttpGet("bills")]
    public IActionResult BillList()
    {
        ViewBag.UseDataTables = true;
        return View();
    }

    [HttpGet("bills/{billId:long}")]
    public async Task<IActionResult> BillDetail(long billId, CancellationToken cancellationToken)
    {
        var row = await db.Bills
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.BillId == billId, cancellationToken);
        if (row is null) return NotFound();
        return View(row);
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

        await alertHub.Clients.All.SendAsync("DashboardRefresh", "bill", cancellationToken);
        return Ok(new { billId = result.Value });
    }

    [HttpPost("quote")]
    public async Task<IActionResult> CreateQuote([FromBody] CreateQuoteRequest request, CancellationToken cancellationToken)
    {
        if (await IsBusinessDateLocked(request.OutletId, request.BusinessDate, cancellationToken))
        {
            return Conflict("Business date is locked.");
        }
        var quoteNo = await numberGeneratorService.GenerateAsync(request.OutletId, NumberSeriesKey.Quote, cancellationToken);
        var billInputs = request.Items.Select(i => new BillItemInput(i.ItemId, i.ItemName, i.Qty, i.Rate, i.DiscountAmount, i.TaxPercent, i.IsTaxInclusive, i.TaxType)).ToList();
        var calc = calculatorService.Calculate(billInputs, request.BillLevelDiscount, 0, false, false);

        var quote = new Quotation
        {
            OutletId = request.OutletId,
            QuoteNo = quoteNo,
            BusinessDate = request.BusinessDate,
            SubTotal = calc.SubTotal,
            DiscountAmount = calc.DiscountAmount,
            TaxAmount = calc.TaxAmount,
            GrandTotal = calc.GrandTotal
        };

        quote.Items.AddRange(calc.Lines.Select(x => new QuotationItem
        {
            ItemId = x.ItemId,
            ItemNameSnapshot = x.ItemName,
            Qty = x.Qty,
            Rate = x.Rate,
            DiscountAmount = x.DiscountAmount,
            TaxAmount = x.TaxAmount,
            LineTotal = x.LineTotal
        }));

        db.Quotations.Add(quote);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { quoteId = quote.QuotationId, quoteNo = quote.QuoteNo });
    }

    [HttpPost("quote/{quoteId:long}/convert")]
    public async Task<IActionResult> ConvertQuote(long quoteId, CancellationToken cancellationToken)
    {
        var quote = await db.Quotations.Include(x => x.Items).FirstOrDefaultAsync(x => x.QuotationId == quoteId, cancellationToken);
        if (quote is null)
        {
            return NotFound("Quote not found.");
        }

        if (quote.Status == "Converted")
        {
            return BadRequest("Quote already converted.");
        }
        if (await IsBusinessDateLocked(quote.OutletId, quote.BusinessDate, cancellationToken))
        {
            return Conflict("Business date is locked.");
        }

        var billNo = await numberGeneratorService.GenerateAsync(quote.OutletId, NumberSeriesKey.Bill, cancellationToken);
        var bill = new Bill(quote.OutletId, billNo, quote.BusinessDate, BillType.QuoteConverted);
        foreach (var item in quote.Items)
        {
            bill.AddItem(new BillItem(item.ItemId, item.ItemNameSnapshot, item.Qty, item.Rate, item.DiscountAmount, item.TaxAmount));
        }

        db.Bills.Add(bill);
        quote.Status = "Converted";
        await db.SaveChangesAsync(cancellationToken);
        quote.ConvertedToBillId = bill.BillId;
        await db.SaveChangesAsync(cancellationToken);

        await auditService.LogAsync(
            quote.OutletId, 0, "QuoteConverted", nameof(Quotation), quote.QuotationId.ToString(),
            "{\"status\":\"Draft\"}",
            $"{{\"status\":\"Converted\",\"billId\":\"{bill.BillId}\"}}",
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        return Ok(new { billId = bill.BillId, billNo });
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

    [HttpPost("reprint")]
    public async Task<IActionResult> Reprint([FromBody] ReprintRequest request, CancellationToken cancellationToken)
    {
        var pinSetting = await db.RestaurantSettings
            .FirstOrDefaultAsync(x => x.OutletId == request.OutletId && x.SettingKey == "ManagerPin", cancellationToken);
        if (pinSetting is null || pinSetting.SettingValue != request.ManagerPin)
        {
            return Unauthorized("Invalid manager PIN.");
        }

        db.ReprintLogs.Add(new ReprintLog
        {
            OutletId = request.OutletId,
            DocumentType = request.DocumentType,
            DocumentId = request.DocumentId,
            ReprintedBy = request.UserId,
            Reason = request.Reason
        });
        await db.SaveChangesAsync(cancellationToken);

        await auditService.LogAsync(
            request.OutletId, request.UserId, "Reprint", "Document", request.DocumentId.ToString(),
            null,
            $"{{\"documentType\":\"{request.DocumentType}\",\"reason\":\"{request.Reason}\"}}",
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        return Ok(new { status = "ReprintLogged" });
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

    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog([FromQuery] int outletId, CancellationToken cancellationToken)
    {
        var categories = await db.Categories
            .Where(x => x.OutletId == outletId)
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
            .Where(x => x.OutletId == outletId)
            .OrderBy(x => x.ItemName)
            .Select(x => new
            {
                id = x.ItemId,
                name = x.ItemName,
                categoryId = x.CategoryId,
                price = x.SalePrice,
                taxPercent = x.GstPercent
            })
            .ToListAsync(cancellationToken);

        var payload = items.Select(i => new
        {
            i.id,
            i.name,
            i.categoryId,
            i.price,
            i.taxPercent,
            imageUrl = imageMap.TryGetValue(i.name, out var fileName) && !string.IsNullOrWhiteSpace(fileName)
                ? $"{imageBasePath.TrimEnd('/')}/{fileName}"
                : string.Empty
        });

        return Ok(new { categories, items = payload });
    }

    [HttpGet("bills-data")]
    public async Task<IActionResult> BillsData([FromQuery] int outletId, CancellationToken cancellationToken)
    {
        var rows = await db.Bills
            .AsNoTracking()
            .Where(x => x.OutletId == outletId)
            .OrderByDescending(x => x.BillDate)
            .Take(500)
            .Select(x => new
            {
                x.BillId,
                x.BillNo,
                billDate = x.BillDate.ToString("dd-MMM-yyyy HH:mm"),
                status = x.Status.ToString(),
                billType = x.BillType.ToString(),
                itemCount = x.Items.Count,
                x.GrandTotal
            })
            .ToListAsync(cancellationToken);
        return Ok(rows);
    }

    [HttpGet("quotes-data")]
    public async Task<IActionResult> QuotesData([FromQuery] int outletId, CancellationToken cancellationToken)
    {
        var rows = await db.Quotations
            .Where(x => x.OutletId == outletId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(500)
            .Select(x => new
            {
                x.QuotationId,
                x.QuoteNo,
                businessDate = x.BusinessDate.ToString("dd-MMM-yyyy"),
                x.GrandTotal,
                x.Status
            })
            .ToListAsync(cancellationToken);
        return Ok(rows);
    }
}

