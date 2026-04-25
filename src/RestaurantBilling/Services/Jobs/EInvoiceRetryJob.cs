using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Data.Persistence;

namespace Services.Jobs;

public class EInvoiceRetryJob(AppDbContext db, IConfiguration configuration, ILogger<EInvoiceRetryJob> logger)
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        var maxRetries = int.TryParse(configuration["EInvoice:MaxRetries"], out var configuredMaxRetries)
            ? configuredMaxRetries
            : 5;

        var pending = await db.EInvoiceQueue
            .Where(x => (x.Status == "Pending" || x.Status == "Failed") && x.Attempts < maxRetries)
            .OrderBy(x => x.EInvoiceQueueItemId)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var row in pending)
        {
            try
            {
                row.Attempts += 1;
                var billExists = await db.Bills.AnyAsync(x => x.BillId == row.BillId, cancellationToken);
                if (!billExists)
                {
                    row.Status = "Failed";
                    row.LastError = "Bill not found for e-invoice generation.";
                    continue;
                }

                row.Irn ??= $"IRN-{row.BillId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
                row.Status = "Success";
                row.LastError = null;
            }
            catch (Exception ex)
            {
                row.Status = "Failed";
                row.LastError = ex.Message;
                logger.LogError(ex, "E-invoice retry failed for queue item {QueueItemId}", row.EInvoiceQueueItemId);
            }
        }

        var exhausted = await db.EInvoiceQueue
            .Where(x => (x.Status == "Pending" || x.Status == "Failed") && x.Attempts >= maxRetries)
            .ToListAsync(cancellationToken);

        foreach (var row in exhausted)
        {
            row.Status = "Dead";
            row.LastError ??= $"Retry limit exhausted after {maxRetries} attempts.";
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

