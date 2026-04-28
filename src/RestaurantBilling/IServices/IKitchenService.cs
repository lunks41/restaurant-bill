namespace IServices;

public interface IKitchenService
{
    Task<long[]> GenerateKotAsync(long billId, CancellationToken cancellationToken);
    Task UpdateKotStatusAsync(long kotId, string status, CancellationToken cancellationToken);
}
