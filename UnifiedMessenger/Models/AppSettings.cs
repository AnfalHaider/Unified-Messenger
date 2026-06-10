namespace UnifiedMessenger.Models;

public sealed class AppSettings
{
    public const int CurrentVersion = 10;

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

    public bool EnableBranchPulse { get; set; } = true;

    public bool AutoDraftOnlyWhenVisible { get; set; } = true;

    public DraftTonePreference DraftTonePreference { get; set; } = DraftTonePreference.Warm;

    public bool EnableVoiceNoteTranscription { get; set; }

    public int VoiceNoteMaxDurationSeconds { get; set; } = 60;

    public string VoiceNoteLanguageHint { get; set; } = "auto";

    public string WhisperExecutablePath { get; set; } = string.Empty;

    public string WhisperModelPath { get; set; } = string.Empty;

    /// <summary>
    /// When true, closing the window hides to the system tray instead of exiting the process.
    /// </summary>
    public bool RunInBackgroundOnClose { get; set; } = true;

    /// <summary>
    /// When true, professional instances reconcile unread inbox state once after connect.
    /// </summary>
    public bool EnableStartupBackfill { get; set; } = true;

    /// <summary>
    /// Minimum urgency score (0–100) for the professional dashboard urgent triage queue.
    /// </summary>
    public int DashboardUrgencyThreshold { get; set; } = 30;

    /// <summary>
    /// When true, Executive Insights includes heuristic triage cards when Local AI extraction is unavailable.
    /// </summary>
    public bool ShowHeuristicExecutiveInsights { get; set; } = false;

    /// <summary>
    /// Last platform-intelligence alert signal the user dismissed by collapsing the OCC expander.
    /// Auto-expand resumes when the live signal exceeds this value.
    /// </summary>
    public int OccPlatformIntelligenceDismissedSignal { get; set; }

    /// <summary>
    /// Last analytics-trends alert signal the user dismissed by collapsing the OCC expander.
    /// Auto-expand resumes when the live signal exceeds this value.
    /// </summary>
    public int OccAnalyticsTrendsDismissedSignal { get; set; }

    public List<string> OccActionPanelOrder { get; set; } = OccLayoutDefaults.ActionPanelOrder.ToList();

    public List<string> OccContextPanelOrder { get; set; } = OccLayoutDefaults.ContextPanelOrder.ToList();

    public List<string> OccKpiMetricOrder { get; set; } = OccLayoutDefaults.KpiMetricOrder.ToList();

    public List<string> OccHiddenPanels { get; set; } = [];

    public List<OccPanelPlacement> OccPanelPlacements { get; set; } = [];

    public string OccLayoutPresetId { get; set; } = OccLayoutPresets.OperationsFocus;

    public List<string> PersonalOverviewSectionOrder { get; set; } =
        PersonalOverviewLayoutDefaults.SectionOrder.ToList();

    public bool OccBranchPillTeachingDismissed { get; set; }

    public bool OccKanbanColumnTeachingDismissed { get; set; }

    public bool OccLayoutTeachingDismissed { get; set; }

    /// <summary>
    /// When true, the WhatsApp-focus OCC preset was auto-applied for WhatsApp-only module mode.
    /// </summary>
    public bool OccWhatsAppFocusLayoutApplied { get; set; }

    public bool OccThreadClickTeachingDismissed { get; set; }

    /// <summary>
    /// Per-branch service catalog used by WhatsApp operational context and triage prompts.
    /// </summary>
    public List<BranchOperationalProfile> BranchOperationalCatalog { get; set; } =
        BranchOperationalCatalogDefaults.CreateDefaultList();

    /// <summary>
    /// Per-platform adapter module enablement. Missing entries default to enabled.
    /// </summary>
    public List<PlatformModuleSetting> PlatformModules { get; set; } = [];

    /// <summary>
    /// Clamps numeric settings and resets unknown enum values after load or manual edits.
    /// </summary>
    public void Normalize()
    {
        if (Version < 1)
        {
            Version = 1;
        }

        MigrateBranchOperationalCatalog();
        MigratePlatformModules();

        if (Version < CurrentVersion)
        {
            Version = CurrentVersion;
        }

        SlaThresholdMinutes = Math.Clamp(SlaThresholdMinutes, MinSlaThresholdMinutes, MaxSlaThresholdMinutes);
        DashboardUrgencyThreshold = Math.Clamp(DashboardUrgencyThreshold, 15, 50);
        MaxConcurrentWebViews = Math.Clamp(MaxConcurrentWebViews, 0, MaxConcurrentWebViewsCap);
        OccPlatformIntelligenceDismissedSignal = Math.Max(0, OccPlatformIntelligenceDismissedSignal);
        OccAnalyticsTrendsDismissedSignal = Math.Max(0, OccAnalyticsTrendsDismissedSignal);

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

        if (!Enum.IsDefined(DraftTonePreference))
        {
            DraftTonePreference = DraftTonePreference.Warm;
        }

        VoiceNoteMaxDurationSeconds = Math.Clamp(VoiceNoteMaxDurationSeconds, 10, 120);
        VoiceNoteLanguageHint = string.IsNullOrWhiteSpace(VoiceNoteLanguageHint)
            ? "auto"
            : VoiceNoteLanguageHint.Trim();
        WhisperExecutablePath = WhisperExecutablePath?.Trim() ?? string.Empty;
        WhisperModelPath = WhisperModelPath?.Trim() ?? string.Empty;

        LocalAiModelName = string.IsNullOrWhiteSpace(LocalAiModelName)
            ? "phi3:mini"
            : LocalAiModelName.Trim();
    }

    private void MigrateBranchOperationalCatalog()
    {
        if (Version < 6 && (BranchOperationalCatalog is null || BranchOperationalCatalog.Count == 0))
        {
            BranchOperationalCatalog = BranchOperationalCatalogDefaults.CreateDefaultList();
        }

        BranchOperationalCatalog ??= BranchOperationalCatalogDefaults.CreateDefaultList();

        foreach (var profile in BranchOperationalCatalog)
        {
            profile.BranchKey = profile.BranchKey?.Trim() ?? string.Empty;
            profile.Services = NormalizeStringList(profile.Services);
            profile.StandardPackages = NormalizeStringList(profile.StandardPackages);
            profile.BookingRules = profile.BookingRules?.Trim() ?? string.Empty;
        }

        BranchOperationalCatalog = BranchOperationalCatalog
            .Where(profile => !string.IsNullOrWhiteSpace(profile.BranchKey))
            .ToList();
    }

    private static List<string> NormalizeStringList(IEnumerable<string>? values) =>
        values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

    private void MigratePlatformModules()
    {
        PlatformModules ??= [];

        foreach (var item in PlatformModules)
        {
            item.PlatformId = PlatformDefinition.NormalizePlatformId(item.PlatformId);
        }
    }
}
