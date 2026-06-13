using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class OccViewModeSettingsHelper
{
    public static void ApplyPersistedMode(AppSettings settings, OccViewModeState state)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(state);

        state.Mode = ParseMode(settings.OccViewMode);
    }

    public static void WriteToSettings(AppSettings settings, OccViewModeState state)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(state);

        settings.OccViewMode = state.Mode.ToString();
    }

    public static OccViewMode ParseMode(string? value) =>
        Enum.TryParse<OccViewMode>(value, ignoreCase: true, out var mode)
            ? mode
            : OccViewMode.Live;
}
