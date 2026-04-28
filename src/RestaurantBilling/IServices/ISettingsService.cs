using Entities.Configuration;

namespace IServices;

public interface ISettingsService
{
    Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken);
    Task<RestaurantSetting> UpsertSettingAsync(string key, string value, CancellationToken cancellationToken);
}
