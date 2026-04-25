using Entities.Enums;

namespace IServices;

public interface INumberGeneratorService
{
    Task<string> GenerateAsync(int outletId, NumberSeriesKey key, CancellationToken cancellationToken);
}

