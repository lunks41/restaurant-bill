using Entities.Common;
using Entities.Enums;

namespace Entities.Configuration;

public class NumberSeries : BaseEntity
{
    public int NumberSeriesId { get; set; }
    public int OutletId { get; set; }
    public NumberSeriesKey SeriesKey { get; set; }
    public string Prefix { get; set; } = string.Empty;
    public int CurrentNumber { get; set; }
    public int NumberLength { get; set; } = 6;
    public string? Suffix { get; set; }
}

