using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IServices;
using Microsoft.EntityFrameworkCore;
using Data.Persistence;
using Entities.Enums;
using System.Text;
using ClosedXML.Excel;

namespace RestaurantBilling.Controllers;

[Authorize]
[Route("reports")]
public class ReportsController(ISalesReportRepository repository, AppDbContext db) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();

    [HttpGet("dailysales")]
    public IActionResult DailySalesView()
    {
        ViewBag.UseDataTables = true;
        return View("DailySales");
    }

    [HttpGet("billwise")]
    public IActionResult BillWise()
    {
        ViewBag.UseDataTables = true;
        return View();
    }

    [HttpGet("itemwise")]
    public IActionResult ItemWise()
    {
        ViewBag.UseDataTables = true;
        return View();
    }

    [HttpGet("paymentmode")]
    public IActionResult PaymentMode()
    {
        ViewBag.UseDataTables = true;
        return View();
    }

    [HttpGet("dayclosereports")]
    public IActionResult DayCloseReports()
    {
        ViewBag.UseDataTables = true;
        return View();
    }


    [HttpGet("daily-sales")]
    public async Task<IActionResult> DailySales([FromQuery] int outletId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        var data = await repository.GetDailySalesAsync(from, to, cancellationToken);
        return Ok(data);
    }

    [HttpGet("payment-summary")]
    public async Task<IActionResult> PaymentSummary([FromQuery] int outletId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        var data = await repository.GetPaymentSummaryAsync(from, to, cancellationToken);
        return Ok(data);
    }

    [HttpGet("item-wise")]
    public async Task<IActionResult> ItemWiseData([FromQuery] int outletId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        var data = await repository.GetStockVarianceAsync(from, to, cancellationToken);
        return Ok(data);
    }

    [HttpGet("voids")]
    public async Task<IActionResult> Voids([FromQuery] int outletId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        var data = await repository.GetVoidReportAsync(from, to, cancellationToken);
        return Ok(data);
    }


    [HttpGet("bill-wise")]
    public async Task<IActionResult> BillWiseData([FromQuery] int outletId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        var rows = await db.Bills
            .Where(x => x.BusinessDate >= from && x.BusinessDate <= to)
            .OrderByDescending(x => x.BillDate)
            .Select(x => new
            {
                x.BillNo,
                billDate = x.BillDate.ToString("dd-MMM-yyyy HH:mm"),
                status = x.Status.ToString(),
                x.GrandTotal
            })
            .Take(500)
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }


    [HttpGet("voids-data")]
    public async Task<IActionResult> VoidsData([FromQuery] int outletId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        var rows = await db.Bills
            .Where(x => x.BusinessDate >= from && x.BusinessDate <= to && x.Status == BillStatus.Cancelled)
            .OrderByDescending(x => x.BillDate)
            .Select(x => new
            {
                x.BillNo,
                date = x.BusinessDate.ToString("dd-MMM-yyyy"),
                x.GrandTotal,
                status = x.Status.ToString()
            })
            .Take(500)
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    [HttpGet("day-close-reports")]
    public async Task<IActionResult> DayCloseReportsData([FromQuery] int outletId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        var rows = await db.DayCloseReports
            .Where(x => x.BusinessDate >= from && x.BusinessDate <= to)
            .OrderByDescending(x => x.BusinessDate)
            .Select(x => new
            {
                x.DayCloseReportId,
                businessDate = x.BusinessDate.ToString("dd-MMM-yyyy"),
                openedAt = x.OpenedAtUtc.ToString("dd-MMM-yyyy HH:mm"),
                closedAt = x.ClosedAtUtc.ToString("dd-MMM-yyyy HH:mm"),
                x.OpeningCash,
                x.ClosingCash,
                x.TotalSales,
                x.TotalTax,
                x.CashOverShort,
                x.IsLocked
            })
            .Take(5000)
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    [HttpGet("daily-sales-export")]
    public async Task<IActionResult> DailySalesExport([FromQuery] int outletId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] string format = "csv", CancellationToken cancellationToken = default)
    {
        var rows = await repository.GetDailySalesAsync(from, to, cancellationToken);
        var fn = $"daily-sales-{from:yyyyMMdd}-{to:yyyyMMdd}";
        if (format == "xlsx")
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Daily Sales");
            AddExportHeaders(ws, ["Date", "Bills", "Gross Sales", "Total Tax", "Net Sales"]);
            for (var i = 0; i < rows.Count; i++)
            {
                var r = rows[i]; var row = i + 2;
                ws.Cell(row, 1).Value = r.BusinessDate.ToString("dd-MMM-yyyy");
                ws.Cell(row, 2).Value = r.TotalBills;
                ws.Cell(row, 3).Value = (double)r.GrossSales;
                ws.Cell(row, 4).Value = (double)r.TotalTax;
                ws.Cell(row, 5).Value = (double)r.NetSales;
            }
            ws.Columns().AdjustToContents();
            return XlsxResult(wb, fn + ".xlsx");
        }
        var csv = new StringBuilder("Date,Bills,GrossSales,TotalTax,NetSales\n");
        foreach (var r in rows)
            csv.AppendLine($"{r.BusinessDate:dd-MMM-yyyy},{r.TotalBills},{r.GrossSales},{r.TotalTax},{r.NetSales}");
        return CsvResult(csv, fn + ".csv");
    }

    [HttpGet("bill-wise-export")]
    public async Task<IActionResult> BillWiseExport([FromQuery] int outletId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] string format = "csv", CancellationToken cancellationToken = default)
    {
        var rows = await db.Bills
            .Where(x => x.BusinessDate >= from && x.BusinessDate <= to)
            .OrderByDescending(x => x.BillDate)
            .Select(x => new { x.BillNo, billDate = x.BillDate.ToString("dd-MMM-yyyy HH:mm"), status = x.Status.ToString(), billType = x.BillType.ToString(), x.GrandTotal, x.TaxAmount })
            .Take(5000)
            .ToListAsync(cancellationToken);
        var fn = $"bill-wise-{from:yyyyMMdd}-{to:yyyyMMdd}";
        if (format == "xlsx")
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Bill Wise");
            AddExportHeaders(ws, ["Bill No", "Date", "Type", "Status", "Tax", "Total"]);
            for (var i = 0; i < rows.Count; i++)
            {
                var r = rows[i]; var row = i + 2;
                ws.Cell(row, 1).Value = r.BillNo;
                ws.Cell(row, 2).Value = r.billDate;
                ws.Cell(row, 3).Value = r.billType;
                ws.Cell(row, 4).Value = r.status;
                ws.Cell(row, 5).Value = (double)r.TaxAmount;
                ws.Cell(row, 6).Value = (double)r.GrandTotal;
            }
            ws.Columns().AdjustToContents();
            return XlsxResult(wb, fn + ".xlsx");
        }
        var csv = new StringBuilder("BillNo,Date,Type,Status,Tax,Total\n");
        foreach (var r in rows)
            csv.AppendLine($"{r.BillNo},{r.billDate},{r.billType},{r.status},{r.TaxAmount},{r.GrandTotal}");
        return CsvResult(csv, fn + ".csv");
    }

    [HttpGet("payment-summary-export")]
    public async Task<IActionResult> PaymentSummaryExport([FromQuery] int outletId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] string format = "csv", CancellationToken cancellationToken = default)
    {
        var rows = await repository.GetPaymentSummaryAsync(from, to, cancellationToken);
        var fn = $"payment-summary-{from:yyyyMMdd}-{to:yyyyMMdd}";
        if (format == "xlsx")
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Payment Summary");
            AddExportHeaders(ws, ["Mode", "Total Amount", "Transactions"]);
            for (var i = 0; i < rows.Count; i++)
            {
                var r = rows[i]; var row = i + 2;
                ws.Cell(row, 1).Value = r.PaymentMode;
                ws.Cell(row, 2).Value = (double)r.TotalAmount;
                ws.Cell(row, 3).Value = r.TransactionCount;
            }
            ws.Columns().AdjustToContents();
            return XlsxResult(wb, fn + ".xlsx");
        }
        var csv = new StringBuilder("Mode,TotalAmount,Transactions\n");
        foreach (var r in rows)
            csv.AppendLine($"{r.PaymentMode},{r.TotalAmount},{r.TransactionCount}");
        return CsvResult(csv, fn + ".csv");
    }

    [HttpGet("item-wise-export")]
    public async Task<IActionResult> ItemWiseExport([FromQuery] int outletId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] string format = "csv", CancellationToken cancellationToken = default)
    {
        var rows = await repository.GetStockVarianceAsync(from, to, cancellationToken);
        var fn = $"item-wise-{from:yyyyMMdd}-{to:yyyyMMdd}";
        if (format == "xlsx")
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Item Wise");
            AddExportHeaders(ws, ["Item", "Theoretical", "Actual", "Variance Qty", "Variance Value"]);
            for (var i = 0; i < rows.Count; i++)
            {
                var r = rows[i]; var row = i + 2;
                ws.Cell(row, 1).Value = r.ItemName;
                ws.Cell(row, 2).Value = (double)r.TheoreticalConsumption;
                ws.Cell(row, 3).Value = (double)r.ActualConsumption;
                ws.Cell(row, 4).Value = (double)r.VarianceQty;
                ws.Cell(row, 5).Value = (double)r.VarianceValue;
            }
            ws.Columns().AdjustToContents();
            return XlsxResult(wb, fn + ".xlsx");
        }
        var csv = new StringBuilder("Item,Theoretical,Actual,VarianceQty,VarianceValue\n");
        foreach (var r in rows)
            csv.AppendLine($"{r.ItemName},{r.TheoreticalConsumption},{r.ActualConsumption},{r.VarianceQty},{r.VarianceValue}");
        return CsvResult(csv, fn + ".csv");
    }

    [HttpGet("day-close-reports-export")]
    public async Task<IActionResult> DayCloseReportsExport([FromQuery] int outletId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] string format = "csv", CancellationToken cancellationToken = default)
    {
        var rows = await db.DayCloseReports
            .Where(x => x.BusinessDate >= from && x.BusinessDate <= to)
            .OrderByDescending(x => x.BusinessDate)
            .Select(x => new
            {
                businessDate = x.BusinessDate.ToString("dd-MMM-yyyy"),
                openedAt = x.OpenedAtUtc.ToString("dd-MMM-yyyy HH:mm"),
                closedAt = x.ClosedAtUtc.ToString("dd-MMM-yyyy HH:mm"),
                x.OpeningCash,
                x.ClosingCash,
                x.TotalSales,
                x.TotalTax,
                x.CashOverShort,
                x.IsLocked
            })
            .Take(5000)
            .ToListAsync(cancellationToken);

        var fn = $"day-close-reports-{from:yyyyMMdd}-{to:yyyyMMdd}";
        if (format == "xlsx")
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Day Close Reports");
            AddExportHeaders(ws, ["Date", "Opened At", "Closed At", "Opening Cash", "Closing Cash", "Sales", "Tax", "Over/Short", "Locked"]);
            for (var i = 0; i < rows.Count; i++)
            {
                var r = rows[i]; var row = i + 2;
                ws.Cell(row, 1).Value = r.businessDate;
                ws.Cell(row, 2).Value = r.openedAt;
                ws.Cell(row, 3).Value = r.closedAt;
                ws.Cell(row, 4).Value = (double)r.OpeningCash;
                ws.Cell(row, 5).Value = (double)r.ClosingCash;
                ws.Cell(row, 6).Value = (double)r.TotalSales;
                ws.Cell(row, 7).Value = (double)r.TotalTax;
                ws.Cell(row, 8).Value = (double)r.CashOverShort;
                ws.Cell(row, 9).Value = r.IsLocked ? "Yes" : "No";
            }
            ws.Columns().AdjustToContents();
            return XlsxResult(wb, fn + ".xlsx");
        }

        var csv = new StringBuilder("Date,OpenedAt,ClosedAt,OpeningCash,ClosingCash,Sales,Tax,CashOverShort,Locked\n");
        foreach (var r in rows)
            csv.AppendLine($"{r.businessDate},{r.openedAt},{r.closedAt},{r.OpeningCash},{r.ClosingCash},{r.TotalSales},{r.TotalTax},{r.CashOverShort},{(r.IsLocked ? "Yes" : "No")}");
        return CsvResult(csv, fn + ".csv");
    }


    [HttpGet("voids-export")]
    public async Task<IActionResult> VoidsExport([FromQuery] int outletId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] string format = "csv", CancellationToken cancellationToken = default)
    {
        var rows = await db.Bills
            .Where(x => x.BusinessDate >= from && x.BusinessDate <= to && x.Status == BillStatus.Cancelled)
            .OrderByDescending(x => x.BillDate)
            .Select(x => new { x.BillNo, date = x.BusinessDate.ToString("dd-MMM-yyyy"), x.GrandTotal, status = x.Status.ToString() })
            .Take(5000)
            .ToListAsync(cancellationToken);
        var fn = $"voids-{from:yyyyMMdd}-{to:yyyyMMdd}";
        if (format == "xlsx")
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Void Bills");
            AddExportHeaders(ws, ["Bill No", "Date", "Total", "Status"]);
            for (var i = 0; i < rows.Count; i++)
            {
                var r = rows[i]; var row = i + 2;
                ws.Cell(row, 1).Value = r.BillNo;
                ws.Cell(row, 2).Value = r.date;
                ws.Cell(row, 3).Value = (double)r.GrandTotal;
                ws.Cell(row, 4).Value = r.status;
            }
            ws.Columns().AdjustToContents();
            return XlsxResult(wb, fn + ".xlsx");
        }
        var csv = new StringBuilder("BillNo,Date,Total,Status\n");
        foreach (var r in rows)
            csv.AppendLine($"{r.BillNo},{r.date},{r.GrandTotal},{r.status}");
        return CsvResult(csv, fn + ".csv");
    }

    private static void AddExportHeaders(IXLWorksheet ws, string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FF6B35");
            cell.Style.Font.FontColor = XLColor.White;
        }
    }

    private IActionResult XlsxResult(XLWorkbook wb, string filename)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
    }

    private IActionResult CsvResult(StringBuilder csv, string filename)
        => File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", filename);

    [HttpGet("daily-sales-drilldown")]
    public async Task<IActionResult> DailySalesDrilldown([FromQuery] int outletId, [FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        var rows = await db.Bills
            .Where(x => x.BusinessDate == date && x.Status != BillStatus.Cancelled)
            .OrderByDescending(x => x.BillDate)
            .Select(x => new { x.BillNo, x.GrandTotal, x.TaxAmount, status = x.Status.ToString(), billDate = x.BillDate.ToString("HH:mm") })
            .ToListAsync(cancellationToken);
        return Ok(rows);
    }
}

