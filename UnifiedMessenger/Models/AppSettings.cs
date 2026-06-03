namespace UnifiedMessenger.Models;

public sealed class AppSettings
{
    public bool EnableBackgroundToasts { get; set; } = true;

    public bool ShowTaskbarBadge { get; set; } = true;

    public AppThemePreference ThemePreference { get; set; } = AppThemePreference.System;

    public NotificationPanelAutoOpenMode PanelAutoOpen { get; set; } =
        NotificationPanelAutoOpenMode.UnfocusedOnly;

    public bool SidebarPinnedExpanded { get; set; } = true;

    public int SlaThresholdMinutes { get; set; } = 15;

    public bool IncludeMutedChatBadges { get; set; }

    public bool ToastGroupByInstance { get; set; } = true;

    public bool ToastUsePlatformBranding { get; set; } = true;
}
