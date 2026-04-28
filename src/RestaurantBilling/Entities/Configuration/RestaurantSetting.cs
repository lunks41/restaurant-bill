using Entities.Common;

namespace Entities.Configuration;

public class RestaurantSetting : BaseEntity
{
    public int RestaurantSettingId { get; set; }
    public string SettingKey { get; set; } = string.Empty;
    public string SettingValue { get; set; } = string.Empty;
    public string SettingType { get; set; } = "String";
}

