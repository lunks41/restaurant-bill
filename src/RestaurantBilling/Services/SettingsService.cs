using Data.Persistence;
using Entities.Configuration;
using IServices;
using Microsoft.EntityFrameworkCore;

namespace Services;

public class SettingsService(AppDbContext db) : ISettingsService
{
    public async Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken)
    {
        return await db.RestaurantSettings
            .Where(x => x.SettingKey == key)
            .Select(x => x.SettingValue)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<RestaurantSetting> UpsertSettingAsync(string key, string value, CancellationToken cancellationToken)
    {
        var row = await db.RestaurantSettings.FirstOrDefaultAsync(x => x.SettingKey == key, cancellationToken);
        if (row is null)
        {
            row = new RestaurantSetting { SettingKey = key, SettingValue = value };
            db.RestaurantSettings.Add(row);
        }
        else
        {
            row.SettingValue = value;
        }
        await db.SaveChangesAsync(cancellationToken);
        return row;
    }
}
