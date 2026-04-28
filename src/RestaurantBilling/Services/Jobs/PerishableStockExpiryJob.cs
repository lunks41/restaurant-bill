using Data.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Services.Jobs;

public class PerishableStockExpiryJob(AppDbContext db, ILogger<PerishableStockExpiryJob> logger)
{
    // Simple client mode: maintain one-day perishable item codes here.
    private static readonly HashSet<string> OneDayPerishableCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "STR004", // Samosa
        "STR005", // Kachori
        "STR006"  // Dahi Kachori
    };

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        var rows = await (
            from stock in db.ItemStocks
            join item in db.Items on stock.ItemId equals item.ItemId
            where !stock.IsDeleted
                  && stock.CurrentQty > 0
                  && OneDayPerishableCodes.Contains(item.ItemCode)
            select new { stock, item.ItemCode, item.ItemName }
        ).ToListAsync(cancellationToken);

        if (rows.Count == 0) return;

        var expiredCount = 0;
        decimal expiredQtyTotal = 0m;
        foreach (var row in rows)
        {
            var previousQty = row.stock.CurrentQty;
            row.stock.CurrentQty = 0m;
            row.stock.Type = "Expired";
            row.stock.IsActive = true;
            expiredCount++;
            expiredQtyTotal += previousQty;
            logger.LogInformation(
                "Perishable stock expired: ItemCode={ItemCode}, ItemName={ItemName}, QtyExpired={QtyExpired}",
                row.ItemCode,
                row.ItemName,
                previousQty);
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Perishable expiry job zeroed stock for {Count} rows. Total expired qty: {TotalQty}",
            expiredCount,
            expiredQtyTotal);
    }
}
