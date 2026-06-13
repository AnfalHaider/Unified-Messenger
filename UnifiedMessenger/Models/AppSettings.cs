namespace UnifiedMessenger.Models;

public sealed class AppSettings
{
    /// <summary>v16 adds local Ollama AI settings (EnableLocalAi, model, endpoint).</summary>
    public const int CurrentVersion = 16;

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

    public bool HasCompletedWorkspaceOnboarding { get; set; }

    public int MaxConcurrentWebViews { get; set; }

    public StartupWarmMode StartupWarmMode { get; set; } = StartupWarmMode.VisibleOnly;

    public bool EnableLazyWebViewLoading { get; set; }

    public bool EnablePerInstanceSleepUnload { get; set; }

    public bool EnableEditInstanceMetadata { get; set; }

    public bool EnableImportExportInstances { get; set; }

    public bool EnableInstanceNotesTags { get; set; }

    public bool RunInBackgroundOnClose { get; set; } = true;

    public int DashboardUrgencyThreshold { get; set; } = 30;

    /// <summary>
    /// When true, professional instances reconcile unread inbox state once after connect.
    /// </summary>
    public bool EnableStartupBackfill { get; set; } = true;

    public WhatsAppBackfillMode WhatsAppBackfillMode { get; set; } = WhatsAppBackfillMode.Unread;

    public int WhatsAppBackfillRecentDays { get; set; } = 7;

    public int WhatsAppBackfillMaxChats { get; set; } = 20;

    /// <summary>
    /// Opt-in deep backfill (bounded sidebar walk). Default off — see v3.4.0 release notes.
    /// </summary>
    public bool EnableDeepBackfill { get; set; }

    public List<string> PersonalOverviewSectionOrder { get; set; } =
        PersonalOverviewLayoutDefaults.SectionOrder.ToList();

    /// <summary>Persisted OCC chart date range (local calendar date, yyyy-MM-dd).</summary>
    public string? OccDateRangeFromLocal { get; set; }

    /// <summary>Persisted OCC chart date range (local calendar date, yyyy-MM-dd).</summary>
    public string? OccDateRangeToLocal { get; set; }

    /// <summary>Persisted OCC view mode: Live workload or Historical report.</summary>
    public string? OccViewMode { get; set; }

    /// <summary>Master toggle for on-device Ollama inference. Off by default.</summary>
    public bool EnableLocalAi { get; set; }

    /// <summary>Default local model pulled on first enable.</summary>
    public string LocalAiModelName { get; set; } = "phi3:mini";

    /// <summary>Ollama HTTP endpoint (readonly default in UI).</summary>
    public string OllamaEndpoint { get; set; } = "http://127.0.0.1:11434/";

    /// <summary>When true, bootstrap embedded Ollama or download fallback on enable.</summary>
    public bool OllamaAutoBootstrap { get; set; } = true;

    public void Normalize()
    {
        if (Version < 1)
        {
            Version = 1;
        }

        if (Version < CurrentVersion)
        {
            Version = CurrentVersion;
        }

        SlaThresholdMinutes = Math.Clamp(SlaThresholdMinutes, MinSlaThresholdMinutes, MaxSlaThresholdMinutes);
        DashboardUrgencyThreshold = Math.Clamp(DashboardUrgencyThreshold, 15, 50);
        WhatsAppBackfillRecentDays = Math.Clamp(WhatsAppBackfillRecentDays, 1, 30);
        WhatsAppBackfillMaxChats = Math.Clamp(WhatsAppBackfillMaxChats, 5, 100);
        MaxConcurrentWebViews = Math.Clamp(MaxConcurrentWebViews, 0, MaxConcurrentWebViewsCap);

        if (!Enum.IsDefined(WhatsAppBackfillMode))
        {
            WhatsAppBackfillMode = WhatsAppBackfillMode.Unread;
        }

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
            StartupWarmMode = StartupWarmMode.VisibleOnly;
        }

        if (string.IsNullOrWhiteSpace(LocalAiModelName))
        {
            LocalAiModelName = "phi3:mini";
        }

        if (string.IsNullOrWhiteSpace(OllamaEndpoint))
        {
            OllamaEndpoint = "http://127.0.0.1:11434/";
        }
        else
        {
            OllamaEndpoint = OllamaEndpoint.Trim();
            if (!OllamaEndpoint.EndsWith("/", StringComparison.Ordinal))
            {
                OllamaEndpoint += "/";
            }
        }
    }
}
