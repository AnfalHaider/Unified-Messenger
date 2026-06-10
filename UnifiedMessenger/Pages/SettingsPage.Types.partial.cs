using UnifiedMessenger.Models;

namespace UnifiedMessenger.Pages;

public sealed partial class SettingsPage
{
    private sealed record ThemePreferenceOption(AppThemePreference Preference, string Label);

    private sealed record PanelAutoOpenOption(NotificationPanelAutoOpenMode Mode, string Label);

    private sealed record ToastSoundOption(ToastSoundPreference Preference, string Label);

    private sealed record PanelDockOption(NotificationPanelDock Dock, string Label);

    private sealed record StartupWarmModeOption(StartupWarmMode Mode, string Label);
}
