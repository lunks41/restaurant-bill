using Microsoft.EntityFrameworkCore;
using IServices;
using Entities.Enums;
using Data.Persistence;

namespace Services;

public class NumberGeneratorService(AppDbContext db) : INumberGeneratorService
{
    public async Task<string> GenerateAsync(int outletId, NumberSeriesKey key, CancellationToken cancellationToken)
    {
        var series = await db.NumberSeries
            .FirstOrDefaultAsync(x => x.OutletId == outletId && x.SeriesKey == key, cancellationToken);

        if (series is null)
        {
            throw new InvalidOperationException($"Number series not configured for {key}.");
        }

        series.CurrentNumber += 1;
        await db.SaveChangesAsync(cancellationToken);

        var numberPart = series.CurrentNumber.ToString().PadLeft(series.NumberLength, '0');
        return $"{series.Prefix}-{DateTime.UtcNow:yyyy}-{numberPart}{series.Suffix}";
    }
}

