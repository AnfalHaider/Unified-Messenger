namespace UnifiedMessenger.Models;

public sealed class AppSettings
{
    /// <summary>v19 adds the remembered left-navigation section.</summary>
    public const int CurrentVersion = 19;

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

    // Default to the compact icon rail (56px) on first run; the title-bar pin button expands it.
    // Existing users keep their persisted value (this default only applies to fresh installs).
    public bool SidebarPinnedExpanded { get; set; }

    public int SlaThresholdMinutes { get; set; } = 15;

    /// <summary>
    /// Stop counting a conversation as awaiting a reply when the customer's last message was plainly a
    /// closing one — "ok", "thanks", "ji", "jazakallah", a thumbs-up.
    ///
    /// <para>
    /// On by default because the raw direction flag is not a usable number: measured on a real salon's
    /// data it reported <b>466 customers waiting, oldest 82 days</b>, when only 41 had actually asked
    /// anything and 454 had already been read. The cost was not the size of the number — it was that a
    /// customer reporting bruising and another saying they would go elsewhere were invisible inside it.
    /// </para>
    /// <para>
    /// Turn it off to see the raw direction-based count. Nothing is deleted either way: excluded chats
    /// stay listed with the reason they were excluded.
    /// </para>
    /// </summary>
    public bool FilterClosedConversations { get; set; } = true;

    /// <summary>
    /// How old a waiting conversation has to be before it counts as backlog rather than today's queue.
    /// </summary>
    /// <remarks>
    /// Seven days. On the measured data 341 of 466 were older than that, and 176 were older than a month
    /// — mixing them with the last 24 hours is what made the live queue unreadable. The backlog is still
    /// shown as its own number, so an 82-day-old complaint is separated out, never hidden.
    /// </remarks>
    public int AwaitingBacklogAfterDays { get; set; } = 7;

    /// <summary>
    /// Let the local model judge the conversations the word rules cannot — messages like "Mel to mel" or
    /// "Both signature and senior artist" that are neither plainly a question nor plainly a sign-off.
    /// </summary>
    /// <remarks>
    /// Requires <see cref="EnableLocalAi"/>. When Ollama is off, slow, or answers something unexpected,
    /// the conversation simply stays in the count — the model can only ever remove chats it is sure
    /// about, never add uncertainty to the queue.
    /// </remarks>
    public bool UseAiForReplyNeed { get; set; } = true;

    /// <summary>
    /// Ask where to put each received file, using Windows' own save dialog.
    /// </summary>
    /// <remarks>
    /// On by default. WebView2's own flow drops files into a folder the owner never chose and cannot easily
    /// find — for an unpackaged host that is not the browser's visible Downloads folder. Someone saving a
    /// customer's reference photo wants it somewhere they picked, under a name they will recognise.
    /// </remarks>
    public bool AskWhereToSaveDownloads { get; set; } = true;

    /// <summary>
    /// Where files go when <see cref="AskWhereToSaveDownloads"/> is off, and where the save dialog opens
    /// when it is on. Updated to the last folder used, which is what every browser does.
    /// </summary>
    public string DownloadFolder { get; set; } = string.Empty;

    public bool IncludeMutedChatBadges { get; set; }

    /// <summary>
    /// Read oversight data from WhatsApp Web's in-memory model collections instead of its persisted
    /// IndexedDB store. The in-memory models are already decrypted (so every chat gets a real preview,
    /// not just the ~60 rendered sidebar rows) and never lag behind a reply sent from the phone. Falls
    /// back to the IndexedDB scan automatically whenever the bridge can't reach the collections, so
    /// turning this off is only needed to force the legacy path for diagnosis.
    /// </summary>
    public bool UseStoreBridge { get; set; } = true;

    public bool ToastGroupByInstance { get; set; } = true;

    public bool ToastUsePlatformBranding { get; set; } = true;

    public ToastSoundPreference ToastSound { get; set; } = ToastSoundPreference.Default;

    /// <summary>When on, threshold/awaiting toasts are suppressed during quiet hours (e.g. overnight).</summary>
    public bool QuietHoursEnabled { get; set; }

    /// <summary>Quiet-hours start hour (0–23, local). Wraps past midnight when start &gt; end.</summary>
    public int QuietHoursStartHour { get; set; } = 21;

    /// <summary>Quiet-hours end hour (0–23, local).</summary>
    public int QuietHoursEndHour { get; set; } = 8;

    public bool EnableAutoUpdate { get; set; } = true;

    public bool PromptBeforeAutoUpdate { get; set; }

    public bool LaunchAtStartup { get; set; }

    public bool PromptPinToTaskbar { get; set; } = true;

    public bool HasPromptedPinToTaskbar { get; set; }

    public bool HasCompletedWorkspaceOnboarding { get; set; }

    public int MaxConcurrentWebViews { get; set; }

    public StartupWarmMode StartupWarmMode { get; set; } = StartupWarmMode.VisibleOnly;

    public bool EnableLazyWebViewLoading { get; set; } = true;

    public bool EnablePerInstanceSleepUnload { get; set; }

    /// <summary>
    /// Minutes a non-visible, non-professional WebView may sit idle before it is closed to reclaim RAM
    /// (it reloads — still signed in — on next open). Professional accounts are exempt so background
    /// oversight keeps reading them. 0 disables idle reaping. Clamped to [0, 240].
    /// </summary>
    public int IdleSessionReapMinutes { get; set; } = 20;

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

    /// <summary>
    /// Per-location (workspace) overrides for the Professional scope — display name, SLA threshold,
    /// and business hours. Empty by default; absence means "use global threshold, 24/7 clock".
    /// </summary>
    public List<WorkspaceProfile> WorkspaceProfiles { get; set; } = [];

    /// <summary>Persisted OCC chart date range (local calendar date, yyyy-MM-dd).</summary>
    public string? OccDateRangeFromLocal { get; set; }

    /// <summary>Persisted OCC chart date range (local calendar date, yyyy-MM-dd).</summary>
    public string? OccDateRangeToLocal { get; set; }

    /// <summary>Persisted OCC view mode: Live workload or Historical report.</summary>
    public string? OccViewMode { get; set; }

    /// <summary>When true, kanban board view is expanded below the unified work queue.</summary>
    public bool OccBoardViewExpanded { get; set; }

    /// <summary>Migration hint: v3.7 upgraders default board expanded on first v4 load.</summary>
    public bool? OccDefaultBoardViewExpanded { get; set; }

    /// <summary>When true, user has seen the unified queue TeachingTip.</summary>
    public bool OccQueueTeachingTipSeen { get; set; }

    /// <summary>Compact thread card density in Operations Command Center.</summary>
    public bool OccCompactCardDensity { get; set; }

    /// <summary>
    /// Command-center alert: raise a desktop toast when an account's awaiting-reply count reaches this
    /// many. 0 disables the alert. Default 5.
    /// </summary>
    public int OversightAwaitingAlertThreshold { get; set; } = 5;

    /// <summary>Last time the operator viewed the command center — used for the "since you were here" digest.</summary>
    public DateTimeOffset? OversightLastSeenUtc { get; set; }

    /// <summary>Sidebar scope filter: "All", "Professional", or "Personal".</summary>
    public string SidebarScopeFilter { get; set; } = "All";

    /// <summary>
    /// The section the shell reopens on. Stored as the sidebar selection key (e.g. "analytics") and
    /// parsed defensively via <c>WorkspaceSidebarHelper.ParseSection</c>, so an unknown or hand-edited
    /// value falls back to Dashboard rather than failing to start.
    /// </summary>
    public string LastVisitedSection { get; set; } = "dashboard";

    /// <summary>
    /// The account that was last opened, so the lazy startup warm has one to bring up.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>LastVisitedSection</c> only ever records a *section* — <c>SelectInstanceAsync</c> persisted
    /// nothing — so there was no such thing as "the account we were last on". That is why
    /// <c>ShellController</c> passed <c>null</c> to <c>WarmAllSessionsAsync</c>: it had nothing to pass.
    /// With the default settings (<c>EnableLazyWebViewLoading</c> on) that made the warm a no-op, so no
    /// account reached <c>Connected</c>, and <c>OversightAlertMonitor</c> skips every account that has not
    /// — meaning background scanning never started for anything until the owner clicked into it by hand.
    /// </para>
    /// <para>
    /// Deliberately *not* cleared when the owner navigates to a section. It records the last account
    /// opened, not the last thing on screen: someone who ends a session on the dashboard still wants that
    /// account warming next launch. Validated against the registry on read, because accounts get deleted.
    /// </para>
    /// </remarks>
    public string LastVisitedInstanceId { get; set; } = string.Empty;

    /// <summary>
    /// When on, the command center surfaces a "your weekly report is ready" banner once a week (the app
    /// is a persistent tray app, so this replaces an OS scheduled task — nothing leaves the machine).
    /// </summary>
    public bool WeeklyReportReminderEnabled { get; set; } = true;

    /// <summary>Last time the weekly report was opened (or the reminder baseline was set). Null until first run.</summary>
    public DateTimeOffset? WeeklyReportLastShownUtc { get; set; }

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
        IdleSessionReapMinutes = Math.Clamp(IdleSessionReapMinutes, 0, 240);
        OversightAwaitingAlertThreshold = Math.Clamp(OversightAwaitingAlertThreshold, 0, 1000);

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

        WorkspaceProfiles ??= [];
        foreach (var profile in WorkspaceProfiles)
        {
            profile.Normalize();
        }
    }
}
