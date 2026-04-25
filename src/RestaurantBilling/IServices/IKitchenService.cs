namespace IServices;

public interface IKitchenService
{
    Task<long[]> GenerateKotAsync(int outletId, long billId, CancellationToken cancellationToken);
    Task UpdateKotStatusAsync(long kotId, string status, CancellationToken cancellationToken);
}
