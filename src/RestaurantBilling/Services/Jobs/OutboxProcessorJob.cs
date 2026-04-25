using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Data.Persistence;

namespace Services.Jobs;

public class OutboxProcessorJob(AppDbContext db, ILogger<OutboxProcessorJob> logger)
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        var pending = await db.OutboxEvents
            .Where(x => x.Status == "Pending")
            .OrderBy(x => x.OutboxEventId)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var ev in pending)
        {
            try
            {
                ev.Status = "Processed";
                ev.ProcessedAtUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                ev.Status = "Failed";
                ev.RetryCount += 1;
                ev.Error = ex.Message;
                logger.LogError(ex, "Outbox event processing failed {OutboxEventId}", ev.OutboxEventId);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

