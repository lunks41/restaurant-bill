using Entities.Configuration;

namespace IServices;

public interface ISettingsService
{
    Task<string?> GetSettingAsync(int outletId, string key, CancellationToken cancellationToken);
    Task<RestaurantSetting> UpsertSettingAsync(int outletId, string key, string value, CancellationToken cancellationToken);
}
