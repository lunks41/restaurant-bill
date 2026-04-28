using Data.Persistence;
using Entities.Enums;
using Entities.Kitchen;
using IServices;
using Microsoft.EntityFrameworkCore;

namespace Services;

public class KitchenService(
    AppDbContext db,
    INumberGeneratorService numberGeneratorService) : IKitchenService
{
    public async Task<long[]> GenerateKotAsync(long billId, CancellationToken cancellationToken)
    {
        var bill = await db.Bills.Include(x => x.Items).FirstOrDefaultAsync(x => x.BillId == billId, cancellationToken)
            ?? throw new InvalidOperationException("Bill not found.");

        var kotNo = await numberGeneratorService.GenerateAsync(NumberSeriesKey.KOT, cancellationToken);
        var header = new KotHeader
        {
            BillId = billId,
            KotNo = kotNo,
            KotDate = DateTime.UtcNow,
            BusinessDate = bill.BusinessDate,
            KitchenStationId = 1,
            KotEventType = "New",
            Status = "Pending"
        };
        db.KotHeaders.Add(header);
        await db.SaveChangesAsync(cancellationToken);

        db.KotItems.AddRange(bill.Items.Select(i => new KotItem
        {
            KotHeaderId = header.KotHeaderId,
            ItemId = i.ItemId,
            ItemNameSnapshot = i.ItemNameSnapshot,
            Qty = i.Qty
        }));
        await db.SaveChangesAsync(cancellationToken);
        return [header.KotHeaderId];
    }

    public async Task UpdateKotStatusAsync(long kotId, string status, CancellationToken cancellationToken)
    {
        var row = await db.KotHeaders.FirstOrDefaultAsync(x => x.KotHeaderId == kotId, cancellationToken)
            ?? throw new InvalidOperationException("KOT not found.");
        row.Status = status;
        await db.SaveChangesAsync(cancellationToken);
    }
}
