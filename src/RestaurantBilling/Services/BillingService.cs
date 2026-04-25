using Data.Persistence;
using Entities.Enums;
using Entities.Sales;
using IServices;
using Microsoft.EntityFrameworkCore;

namespace Services;

public class BillingService(
    AppDbContext db,
    INumberGeneratorService numberGeneratorService,
    IStockService stockService) : IBillingService
{
    public async Task<Bill> CreateDraftAsync(int outletId, DateOnly businessDate, IEnumerable<BillItem> items, CancellationToken cancellationToken)
    {
        var billNo = await numberGeneratorService.GenerateAsync(outletId, NumberSeriesKey.Bill, cancellationToken);
        var bill = new Bill(outletId, billNo, businessDate, BillType.DineIn);
        foreach (var item in items)
        {
            bill.AddItem(item);
        }

        db.Bills.Add(bill);
        await db.SaveChangesAsync(cancellationToken);
        return bill;
    }

    public async Task<long> SettleAsync(long billId, IEnumerable<Payment> payments, CancellationToken cancellationToken)
    {
        var bill = await db.Bills.Include(x => x.Items).FirstOrDefaultAsync(x => x.BillId == billId, cancellationToken)
            ?? throw new InvalidOperationException("Bill not found.");

        bill.Settle(payments);
        await stockService.DeductSaleStockAsync(bill.OutletId, bill.BusinessDate, bill.Items.ToList(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return bill.BillId;
    }
}
