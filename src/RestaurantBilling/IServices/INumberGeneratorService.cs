using Entities.Enums;

namespace IServices;

public interface INumberGeneratorService
{
    Task<string> GenerateAsync(NumberSeriesKey key, CancellationToken cancellationToken);
}

