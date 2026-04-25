using System.Security.Cryptography;
using System.Text;
using IServices;
using Entities.Audit;
using Microsoft.EntityFrameworkCore;
using Data.Persistence;

namespace Services;

public class AuditService(AppDbContext db) : IAuditService
{
    public async Task LogAsync(
        int outletId,
        int userId,
        string action,
        string entityType,
        string entityId,
        string? oldValuesJson,
        string? newValuesJson,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var previousHash = await db.AuditLogs
            .Where(x => x.OutletId == outletId)
            .OrderByDescending(x => x.AuditLogId)
            .Select(x => x.EntryHash)
            .FirstOrDefaultAsync(cancellationToken);

        var payload = $"{outletId}|{userId}|{action}|{entityType}|{entityId}|{oldValuesJson}|{newValuesJson}|{ipAddress}|{userAgent}|{previousHash}";
        var currentHash = ComputeSha256(payload);

        db.AuditLogs.Add(new AuditLog
        {
            OutletId = outletId,
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValuesJson = oldValuesJson,
            NewValuesJson = newValuesJson,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            PreviousHash = previousHash,
            EntryHash = currentHash
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string ComputeSha256(string input)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
