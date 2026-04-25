using IServices;
using Entities.Sales;
using ESCPOS_NET;
using ESCPOS_NET.Emitters;
using ESCPOS_NET.Utilities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Services;

public class PrintService : IPrintService
{
    public Task<byte[]> RenderBillThermalAsync(Bill bill, CancellationToken cancellationToken)
    {
        var e = new EPSON();
        var bytes = ByteSplicer.Combine(
            e.CenterAlign(),
            e.SetStyles(PrintStyle.Bold | PrintStyle.DoubleHeight),
            e.PrintLine("*** BILL ***"),
            e.SetStyles(PrintStyle.None),
            e.PrintLine($"Bill No : {bill.BillNo}"),
            e.PrintLine($"Date    : {bill.BillDate:dd-MMM-yyyy HH:mm}"),
            e.PrintLine($"Type    : {bill.BillType}"),
            e.PrintLine("--------------------------------"),
            e.LeftAlign(),
            RenderItemLines(e, bill),
            e.PrintLine("--------------------------------"),
            e.RightAlign(),
            e.PrintLine($"Sub Total   : {bill.SubTotal,10:0.00}"),
            e.PrintLine($"Discount    : {bill.DiscountAmount,10:0.00}"),
            e.PrintLine($"Tax         : {bill.TaxAmount,10:0.00}"),
            bill.ServiceCharge > 0 ? e.PrintLine($"Service Chg : {bill.ServiceCharge,10:0.00}") : [],
            e.SetStyles(PrintStyle.Bold),
            e.PrintLine($"TOTAL       : {bill.GrandTotal,10:0.00}"),
            e.SetStyles(PrintStyle.None),
            e.CenterAlign(),
            e.PrintLine("--------------------------------"),
            e.PrintLine("Thank you! Visit again."),
            e.FeedLines(3),
            e.PartialCutAfterFeed(1)
        );
        return Task.FromResult(bytes);
    }

    private static byte[] RenderItemLines(EPSON e, Bill bill)
    {
        var chunks = new List<byte[]>();
        foreach (var item in bill.Items)
        {
            var name = item.ItemNameSnapshot.Length > 20 ? item.ItemNameSnapshot[..20] : item.ItemNameSnapshot;
            var lineTotal = item.LineTotal.ToString("0.00").PadLeft(10);
            chunks.Add(e.PrintLine($"{name,-20} {item.Qty,4:0.##} x{item.RateSnapshot,7:0.00} {lineTotal}"));
        }
        return ByteSplicer.Combine(chunks.ToArray());
    }

    public Task<Stream> RenderBillPdfAsync(Bill bill, CancellationToken cancellationToken)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(1, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(header =>
                {
                    header.Item().AlignCenter().Text("TAX INVOICE")
                        .FontSize(16).Bold();
                    header.Item().AlignCenter().Text($"Bill No: {bill.BillNo}")
                        .FontSize(11).Bold();
                    header.Item().AlignCenter().Text($"Date: {bill.BillDate:dd-MMM-yyyy HH:mm}")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                    header.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingTop(8).Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(5);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(3);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Orange.Lighten3).Padding(4).Text("Item").Bold();
                            h.Cell().Background(Colors.Orange.Lighten3).Padding(4).AlignRight().Text("Qty").Bold();
                            h.Cell().Background(Colors.Orange.Lighten3).Padding(4).AlignRight().Text("Rate").Bold();
                            h.Cell().Background(Colors.Orange.Lighten3).Padding(4).AlignRight().Text("Total").Bold();
                        });

                        foreach (var item in bill.Items)
                        {
                            table.Cell().Padding(4).Text(item.ItemNameSnapshot);
                            table.Cell().Padding(4).AlignRight().Text(item.Qty.ToString("0.##"));
                            table.Cell().Padding(4).AlignRight().Text(item.RateSnapshot.ToString("0.00"));
                            table.Cell().Padding(4).AlignRight().Text(item.LineTotal.ToString("0.00"));
                        }
                    });

                    col.Item().PaddingTop(8).Column(totals =>
                    {
                        totals.Item().Row(r =>
                        {
                            r.RelativeItem().AlignRight().Text("Sub Total:");
                            r.ConstantItem(80).AlignRight().Text(bill.SubTotal.ToString("0.00"));
                        });
                        if (bill.DiscountAmount > 0)
                        {
                            totals.Item().Row(r =>
                            {
                                r.RelativeItem().AlignRight().Text("Discount:");
                                r.ConstantItem(80).AlignRight().Text($"-{bill.DiscountAmount:0.00}");
                            });
                        }
                        totals.Item().Row(r =>
                        {
                            r.RelativeItem().AlignRight().Text("Tax (GST):");
                            r.ConstantItem(80).AlignRight().Text(bill.TaxAmount.ToString("0.00"));
                        });
                        if (bill.ServiceCharge > 0)
                        {
                            totals.Item().Row(r =>
                            {
                                r.RelativeItem().AlignRight().Text("Service Charge:");
                                r.ConstantItem(80).AlignRight().Text(bill.ServiceCharge.ToString("0.00"));
                            });
                        }
                        if (bill.RoundOff != 0)
                        {
                            totals.Item().Row(r =>
                            {
                                r.RelativeItem().AlignRight().Text("Round Off:");
                                r.ConstantItem(80).AlignRight().Text(bill.RoundOff.ToString("0.00"));
                            });
                        }
                        totals.Item().Background(Colors.Orange.Lighten4).Padding(4).Row(r =>
                        {
                            r.RelativeItem().AlignRight().Text("GRAND TOTAL:").Bold().FontSize(12);
                            r.ConstantItem(80).AlignRight().Text($"₹{bill.GrandTotal:0.00}").Bold().FontSize(12);
                        });
                    });
                });

                page.Footer().AlignCenter().Text("Thank you for dining with us!")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });

        var ms = new MemoryStream();
        document.GeneratePdf(ms);
        ms.Position = 0;
        return Task.FromResult<Stream>(ms);
    }
}
