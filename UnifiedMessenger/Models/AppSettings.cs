namespace UnifiedMessenger.Models;

public sealed class AppSettings
{
    public const int CurrentVersion = 2;

    public const int MinSlaThresholdMinutes = 5;

    public const int MaxSlaThresholdMinutes = 120;

    public const int MaxConcurrentWebViewsCap = 32;

    public int Version { get; set; } = CurrentVersion;

    public bool EnableBackgroundToasts { get; set; } = true;

    public bool ShowTaskbarBadge { get; set; } = true;

    public AppThemePreference ThemePreference { get; set; } = AppThemePreference.System;

    public NotificationPanelAutoOpenMode PanelAutoOpen { get; set; } =
        NotificationPanelAutoOpenMode.UnfocusedOnly;

    public NotificationPanelDock PanelDock { get; set; } = NotificationPanelDock.Right;

    public bool SidebarPinnedExpanded { get; set; } = true;

    public int SlaThresholdMinutes { get; set; } = 15;

    public bool IncludeMutedChatBadges { get; set; }

    public bool ToastGroupByInstance { get; set; } = true;

    public bool ToastUsePlatformBranding { get; set; } = true;

    public ToastSoundPreference ToastSound { get; set; } = ToastSoundPreference.Default;

    public bool EnableAutoUpdate { get; set; } = true;

    public bool PromptBeforeAutoUpdate { get; set; }

    public bool LaunchAtStartup { get; set; }

    public bool PromptPinToTaskbar { get; set; } = true;

    public bool HasPromptedPinToTaskbar { get; set; }

    public int MaxConcurrentWebViews { get; set; }

    public StartupWarmMode StartupWarmMode { get; set; } = StartupWarmMode.WarmAll;

    // Path C — experimental / future session-management options (settings only unless noted)
    public bool EnableLazyWebViewLoading { get; set; }

    public bool EnablePerInstanceSleepUnload { get; set; }

    public bool EnableEditInstanceMetadata { get; set; }

    public bool EnableImportExportInstances { get; set; }

    public bool EnableInstanceNotesTags { get; set; }

    public bool EnableLocalAi { get; set; }

    public bool OllamaAutoBootstrap { get; set; } = true;

    public string LocalAiModelName { get; set; } = "phi3:mini";

    public bool EnableAutoDraft { get; set; }

    public bool AutoDraftOnlyWhenVisible { get; set; } = true;

    /// <summary>
    /// When true, closing the window hides to the system tray instead of exiting the process.
    /// </summary>
    public bool RunInBackgroundOnClose { get; set; } = true;

    /// <summary>
    /// When true, professional instances reconcile unread inbox state once after connect.
    /// </summary>
    public bool EnableStartupBackfill { get; set; } = true;

    /// <summary>
    /// Clamps numeric settings and resets unknown enum values after load or manual edits.
    /// </summary>
    public void Normalize()
    {
        if (Version < 1)
        {
            Version = 1;
        }

        SlaThresholdMinutes = Math.Clamp(SlaThresholdMinutes, MinSlaThresholdMinutes, MaxSlaThresholdMinutes);
        MaxConcurrentWebViews = Math.Clamp(MaxConcurrentWebViews, 0, MaxConcurrentWebViewsCap);

        if (!Enum.IsDefined(ThemePreference))
        {
            ThemePreference = AppThemePreference.System;
        }

        if (!Enum.IsDefined(PanelAutoOpen))
        {
            PanelAutoOpen = NotificationPanelAutoOpenMode.UnfocusedOnly;
        }

        if (!Enum.IsDefined(PanelDock))
        {
            PanelDock = NotificationPanelDock.Right;
        }

        if (!Enum.IsDefined(ToastSound))
        {
            ToastSound = ToastSoundPreference.Default;
        }

        if (!Enum.IsDefined(StartupWarmMode))
        {
            StartupWarmMode = StartupWarmMode.WarmAll;
        }

        LocalAiModelName = string.IsNullOrWhiteSpace(LocalAiModelName)
            ? "phi3:mini"
            : LocalAiModelName.Trim();
    }
}
