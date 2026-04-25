namespace IServices;

public interface IAuditService
{
    Task LogAsync(
        int outletId,
        int userId,
        string action,
        string entityType,
        string entityId,
        string? oldValuesJson,
        string? newValuesJson,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken);
}
