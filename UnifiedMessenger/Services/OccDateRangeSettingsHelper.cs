using System.Globalization;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class OccDateRangeSettingsHelper
{
    private const string DateFormat = "yyyy-MM-dd";

    public static void ApplyPersistedRange(AppSettings settings, OccDateRangeFilterState filter)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(filter);

        filter.ApplyPersistedRange(
            ParseLocalDate(settings.OccDateRangeFromLocal),
            ParseLocalDate(settings.OccDateRangeToLocal));
    }

    public static void WriteToSettings(AppSettings settings, OccDateRangeFilterState filter)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(filter);

        settings.OccDateRangeFromLocal = filter.FromUtc?.LocalDateTime.ToString(DateFormat, CultureInfo.InvariantCulture);
        settings.OccDateRangeToLocal = filter.ToUtc?.LocalDateTime.ToString(DateFormat, CultureInfo.InvariantCulture);
    }

    public static DateTimeOffset? ParseLocalDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !DateTime.TryParseExact(
                value,
                DateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return null;
        }

        return new DateTimeOffset(date, DateTimeOffset.Now.Offset);
    }
}
