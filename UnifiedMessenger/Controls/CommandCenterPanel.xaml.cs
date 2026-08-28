using System.Text;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using UnifiedMessenger.Controls.Shared;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Ai;
using UnifiedMessenger.Services.Backfill;
using UnifiedMessenger.ViewModels;
using Windows.System;

namespace UnifiedMessenger.Controls;

public sealed partial class CommandCenterPanel : UserControl
{
    private const int AutoRefreshSeconds = 20;

    private ApplicationServices? _services;
    private DispatcherTimer? _autoRefreshTimer;
    private string _emptyStateWindowLabel = "today";
    private readonly HashSet<string> _expandedKeys = new(StringComparer.OrdinalIgnoreCase);

    private OversightWindow SelectedWindow() =>
        ((WindowSelector?.SelectedItem as ComboBoxItem)?.Tag as string) switch
        {
            "Week" => OversightWindow.Week,
            "All" => OversightWindow.All,
            "Custom" => OversightWindow.Custom,
            _ => OversightWindow.Today
        };

    private void OnWindowChanged(object sender, SelectionChangedEventArgs e)
    {
        var custom = SelectedWindow() == OversightWindow.Custom;
        if (FromDatePicker is not null)
        {
            FromDatePicker.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        }
        if (ToDatePicker is not null)
        {
            ToDatePicker.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        }

        Render();
    }

    private void OnCustomDateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args) => Render();

    private static string DescribeCustomRange(DateTimeOffset? start, DateTimeOffset? end)
    {
        if (start is null && end is null)
        {
            return "the selected range";
        }

        var from = start is { } s ? s.ToString("MMM d") : "earliest";
        var to = end is { } e ? e.ToString("MMM d") : "now";
        return $"{from} – {to}";
    }

    /// <summary>The selected window's [start, end] in absolute time. Custom uses the From/To pickers
    /// (To is inclusive through end-of-day).</summary>
    private (DateTimeOffset? Start, DateTimeOffset? End) WindowRange()
    {
        // Must agree instant-for-instant with OversightService.BuildSnapshot, which computes the same
        // window for the same selection — the two are compared against each other in the rendered card.
        // LocalDayBoundary rather than `nowLocal.Offset` / `picker.Offset`: those are the offset of a
        // different instant and are an hour wrong on both DST transition days.
        var nowLocal = DateTimeOffset.Now;
        switch (SelectedWindow())
        {
            case OversightWindow.Today:
                return (LocalDayBoundary.StartOfDay(nowLocal.Date), null);
            case OversightWindow.Week:
                return (LocalDayBoundary.StartOfDaysAgo(nowLocal.Date, 6), null);
            case OversightWindow.Custom:
                DateTimeOffset? start = FromDatePicker?.Date is { } f
                    ? LocalDayBoundary.StartOfDay(f.LocalDateTime.Date)
                    : null;
                DateTimeOffset? end = ToDatePicker?.Date is { } t
                    ? LocalDayBoundary.EndOfDay(t.LocalDateTime.Date)
                    : null;
                return (start, end);
            default:
                return (null, null);
        }
    }

    public CommandCenterPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        // Redraw immediately on theme resolve/change so the code-drawn neutral brushes are re-picked for the
        // right theme (otherwise a toggle waits up to one auto-refresh cycle).
        ActualThemeChanged += (_, _) => { _lastRenderSignature = string.Empty; Render(); };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ApplicationServiceProvider.IsInitialized)
        {
            _services = ApplicationServiceProvider.Current;
        }

        Render();

        // Keep the oversight numbers live without a manual Refresh click. Lightweight: rebuilds from the
        // in-memory thread registry, no I/O. Stopped on unload so it never ticks for a detached panel.
        _autoRefreshTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(AutoRefreshSeconds) };
        _autoRefreshTimer.Tick += OnAutoRefreshTick;
        _autoRefreshTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_autoRefreshTimer is not null)
        {
            _autoRefreshTimer.Tick -= OnAutoRefreshTick;
            _autoRefreshTimer.Stop();
            _autoRefreshTimer = null;
        }
    }

    private void OnAutoRefreshTick(object? sender, object e) => Render();

    /// <summary>Forces a full rebuild on the next render — used when something the data signature doesn't
    /// capture changed (e.g. an account's avatar icon), so cards redraw with the new avatar.</summary>
    public void ForceRender()
    {
        _lastRenderSignature = string.Empty;
        Render();
    }

    private bool _digestShown;
    private string _lastRenderSignature = string.Empty;
    private string _searchQuery = string.Empty;
    private bool _compact;

    // When set, the Needs-reply list is scoped to just these accounts (a card's awaiting pill was clicked).
    private List<string>? _needsReplyFilterIds;
    private string _needsReplyFilterLabel = string.Empty;

    // Needs-reply filters. Age defaults to the same window the hero headline uses, so the list and the
    // number above it start out describing the same population — the mismatch between them was the single
    // most confusing thing on this screen.
    private AwaitingAgeFilter _ageFilter = AwaitingAgeFilter.ThisWeek;
    private QueueFacet? _facetFilter;

    // Which row the keyboard is on. -1 = nothing selected. Survives a re-render by index, because the
    // list rebuilds every 20 seconds and holding a reference to a disposed Border would leak it.
    private int _triageIndex = -1;
    private string? _locationFilter;

    /// <summary>Which slice of the waiting queue the Needs-reply list is showing.</summary>
    internal enum AwaitingAgeFilter
    {
        /// <summary>Active within the backlog threshold — matches the hero's "needs a reply" figure.</summary>
        ThisWeek,

        /// <summary>Arrived today.</summary>
        Today,

        /// <summary>Older than the backlog threshold — the accumulated backlog on its own.</summary>
        Backlog,

        /// <summary>Everything still open, any age.</summary>
        All
    }
    private string? _worstEntityFirstInstanceId;

    private void OnSearchChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        _searchQuery = sender.Text?.Trim() ?? string.Empty;
        _lastRenderSignature = string.Empty; // search isn't part of the data signature — force the rebuild
        Render();
    }

    private void OnDensityToggled(object sender, RoutedEventArgs e)
    {
        _compact = DensityToggle.IsChecked == true;
        _lastRenderSignature = string.Empty;
        Render();
    }

    /// <summary>
    /// Re-render after a change the data signature cannot see. The signature is built from the numbers, so
    /// a filter change looks identical to it and the redraw would be skipped.
    /// </summary>
    private void ForceRerender()
    {
        _lastRenderSignature = string.Empty;
        Render();
    }

    private bool _digestDismissed;

    private void OnDismissDigest(object sender, RoutedEventArgs e)
    {
        _digestDismissed = true;
        DigestBanner.Visibility = Visibility.Collapsed;
    }

    private bool _reportReminderDismissed;

    /// <summary>Snooze the weekly-report reminder to next week without opening the report.</summary>
    private void OnDismissReportReminder(object sender, RoutedEventArgs e)
    {
        _reportReminderDismissed = true;
        ReportReminderBanner.Visibility = Visibility.Collapsed;
        _ = AppSettingsService.Instance.UpdateAsync(s => s.WeeklyReportLastShownUtc = DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Non-invasive weekly nudge: the app runs continuously in the tray, so instead of an OS scheduled task
    /// it surfaces a banner once a week. Sets the baseline on first run so the first nudge lands a week in.
    /// </summary>
    private void UpdateReportReminderBanner()
    {
        var settings = AppSettingsService.Instance.Settings;
        if (WeeklyReportReminder.NeedsBaseline(settings))
        {
            _ = AppSettingsService.Instance.UpdateAsync(s => s.WeeklyReportLastShownUtc = DateTimeOffset.UtcNow);
            ReportReminderBanner.Visibility = Visibility.Collapsed;
            return;
        }

        var due = !_reportReminderDismissed && WeeklyReportReminder.IsDue(settings, DateTimeOffset.UtcNow);
        ReportReminderBanner.Visibility = due ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Keeps at most one banner visible at a time, by priority (backlog &gt; weekly report &gt; digest &gt;
    /// define-locations). Suppressed banners are counted onto the surviving one so nothing is silently lost.
    /// </summary>
    private void ConsolidateBanners()
    {
        // Highest priority first. The paired TextBlock (when present) is where we append the "+N more" count.
        var ordered = new (Border Banner, TextBlock? Text)[]
        {
            (AttentionBanner, AttentionText),
            (ReportReminderBanner, null),
            (DigestBanner, DigestText),
            (LocationCtaBanner, null),
        };

        Border? shown = null;
        TextBlock? shownText = null;
        var suppressed = 0;

        foreach (var (banner, text) in ordered)
        {
            if (banner.Visibility != Visibility.Visible)
            {
                continue;
            }

            if (shown is null)
            {
                shown = banner;
                shownText = text;
            }
            else
            {
                banner.Visibility = Visibility.Collapsed;
                suppressed++;
            }
        }

        if (shownText is not null && suppressed > 0)
        {
            shownText.Text += $"  ·  +{suppressed} more";
        }
    }

    private bool MatchesSearch(string? text) =>
        string.IsNullOrWhiteSpace(_searchQuery) ||
        (!string.IsNullOrWhiteSpace(text) && text.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase));

    private static string BuildRenderSignature(
        OversightGrouping grouping,
        OversightWindow window,
        DateTimeOffset? start,
        DateTimeOffset? end,
        OversightCommandCenterSnapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.Append((int)grouping).Append('|').Append((int)window).Append('|')
            .Append(start?.UtcTicks ?? 0).Append('|').Append(end?.UtcTicks ?? 0).Append('|');
        // Coarse 5-minute bucket: the cards carry relative text ("updated 3m ago", "longest wait 2h") that
        // must not freeze when the underlying counts are unchanged — this forces a redraw a few times an hour.
        sb.Append(DateTimeOffset.UtcNow.UtcTicks / TimeSpan.TicksPerMinute / 5).Append('|');
        foreach (var e in snapshot.Entities)
        {
            sb.Append(e.Key).Append(',').Append(e.OnTimePercent).Append(',').Append(e.AwaitingCount)
                .Append(',').Append(e.MeasuredCount).Append(',').Append(e.HasChatData ? 1 : 0)
                .Append(',').Append(e.HistoricalOpenCount).Append(',').Append(e.IsStale ? 1 : 0)
                // ReadFailed changes the card's text and colour without changing any count, so it must be
                // in the signature or the transition into (and out of) "can't read this account" would be
                // suppressed as a no-op redraw.
                .Append(',').Append(e.ReadFailed ? 1 : 0).Append(';');
        }

        return sb.ToString();
    }

    /// <summary>
    /// Once per session, when snapshots have loaded, summarize what's awaiting since the operator was last
    /// here (and stamp "last seen" now). Returns false until there's data or if already shown.
    /// </summary>
    private bool TryBuildDigestBanner(IReadOnlyList<MessengerInstance> instances, out string text)
    {
        text = string.Empty;
        if (_digestShown)
        {
            return false;
        }

        var ids = instances.Select(i => i.Id).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        var lastSeen = AppSettingsService.Instance.Settings.OversightLastSeenUtc;
        var digest = OversightChatSnapshotService.Instance.BuildDigest(ids, lastSeen);
        if (!digest.HasData)
        {
            return false; // snapshots not loaded yet — try again on the next refresh
        }

        _digestShown = true;
        _ = AppSettingsService.Instance.UpdateAsync(s => s.OversightLastSeenUtc = DateTimeOffset.UtcNow);

        if (digest.TotalAwaiting == 0)
        {
            text = "All caught up — no customers are waiting on a reply.";
            return true;
        }

        var since = lastSeen is { } s ? $"Since {s.ToLocalTime():MMM d, h:mm tt}: " : "Waiting now: ";
        var accountWord = digest.AccountsWithAwaiting == 1 ? "account" : "accounts";

        // "365 total" sat about four hundred pixels below a hero reading 78, with nothing saying they
        // measure different things. Both are true — one is the whole open population, the other is what is
        // active this week — but an unlabelled pair of totals on one screen is how a number stops being
        // believed. The word "open" is doing real work here.
        text = $"{since}{digest.NewAwaiting} new since you were last here · " +
               $"{digest.TotalAwaiting} open in total across {digest.AccountsWithAwaiting} {accountWord}";
        if (digest.OldestActivityUtc is { } oldest)
        {
            text += $" · oldest since {oldest.ToLocalTime():MMM d, h:mm tt}";
        }

        return true;
    }

    public void Render()
    {
        if (_services is null)
        {
            return;
        }

        var needsReply = NeedsReplyButton.IsChecked == true;
        var grouping = GroupByLocationButton.IsChecked == true ? OversightGrouping.ByLocation : OversightGrouping.ByInstance;
        var window = SelectedWindow();
        var (rangeStart, rangeEnd) = WindowRange();
        // Oversight cards are only meaningful for channels that contribute conversation metrics. Embed
        // channels (Google Business reviews / Discord / generic URL) have nothing to scan, so including them
        // would strand them at "syncing…" forever — they simply don't appear in the command center.
        //
        // This now asks a CAPABILITY question rather than "is this WhatsApp". Same result today (only the
        // WhatsApp family declares CanReadUnread), but it is the seam a new channel arrives through: a
        // Telegram or Meta adapter earns its card by declaring CanReadUnread, without being dragged into the
        // WhatsApp IndexedDB pipelines that IsPlatformModuleEnabled still gates.
        var instances = _services.Registry.Instances
            .Where(instance => instance.IsProfessional &&
                               PlatformModuleSettingsHelper.ContributesConversationMetrics(instance.Platform))
            .ToList();
        var snapshot = _services.Oversight.BuildSnapshot(grouping, instances, window, rangeStart, rangeEnd);

        // Change-detection: the 20s auto-refresh re-renders constantly; rebuilding the card list when the
        // data is identical makes the accordions flash. Skip the rebuild when nothing changed.
        var signature = BuildRenderSignature(grouping, window, rangeStart, rangeEnd, snapshot);
        if (signature == _lastRenderSignature)
        {
            return;
        }
        _lastRenderSignature = signature;

        var windowLabel = window switch
        {
            OversightWindow.Today => "today",
            OversightWindow.Week => "the last 7 days",
            OversightWindow.Custom => DescribeCustomRange(rangeStart, rangeEnd),
            _ => "all time"
        };
        // "Define locations" CTA: shown only in ByInstance mode when no locations have been set up.
        var hasLocations = AppSettingsService.Instance.Settings.WorkspaceProfiles.Count > 0;
        LocationCtaBanner.Visibility = !needsReply && grouping == OversightGrouping.ByInstance && !hasLocations
            ? Visibility.Visible
            : Visibility.Collapsed;

        SubtitleText.Text = needsReply
            ? $"Every customer awaiting a reply across all accounts, most urgent first · {windowLabel}"
            : grouping == OversightGrouping.ByLocation
                ? $"Rolled up by location · caught up among chats active {windowLabel}"
                : $"Per account · caught up among chats active {windowLabel} · group into locations (Ctrl+K)";
        _emptyStateWindowLabel = windowLabel;

        // "As of" stamp — honest about persisted (pre-scan) data after a fresh launch.
        var capturedAt = OversightChatSnapshotService.Instance.LastCapturedUtc;
        if (capturedAt is { } cap)
        {
            UpdatedText.Text = $"Updated {RelativeAge(cap)}";
            UpdatedText.Visibility = Visibility.Visible;
        }
        else
        {
            UpdatedText.Visibility = Visibility.Collapsed;
        }

        // Resolve the worst entity's first instance id for the Jump button.
        _worstEntityFirstInstanceId = null;
        if (!string.IsNullOrWhiteSpace(snapshot.WorstEntityKey))
        {
            var worst = snapshot.Entities.FirstOrDefault(e =>
                string.Equals(e.Key, snapshot.WorstEntityKey, StringComparison.OrdinalIgnoreCase));
            _worstEntityFirstInstanceId = worst?.MemberInstanceIds.FirstOrDefault();
        }

        // KPI summary band — whole-business glance, computed from per-instance health regardless of grouping.
        var kpiEntities = grouping == OversightGrouping.ByLocation
            ? _services.Oversight.BuildSnapshot(OversightGrouping.ByInstance, instances, window, rangeStart, rangeEnd).Entities
            : snapshot.Entities;
        RenderKpiBand(kpiEntities, instances, rangeStart, rangeEnd);

        // Informational digest ("since you were last here") — neutral info banner, dismissible, shown once.
        if (!_digestDismissed && TryBuildDigestBanner(instances, out var digestText))
        {
            DigestText.Text = digestText;
            DigestBanner.Visibility = Visibility.Visible;
        }

        // Weekly-report reminder — once a week, non-invasive (no OS scheduled task; the app is always-on).
        UpdateReportReminderBanner();

        // Attention banner (caution) — only when there's a real backlog to act on, or during a re-sync.
        if (snapshot.TotalUrgent > 0 || snapshot.TotalDropped > 0)
        {
            AttentionText.Text = snapshot.AttentionSummary;
            AttentionBanner.Visibility = Visibility.Visible;
            AttentionJumpButton.Visibility = _worstEntityFirstInstanceId is not null
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        else if (!_resyncInProgress)
        {
            AttentionBanner.Visibility = Visibility.Collapsed;
            AttentionJumpButton.Visibility = Visibility.Collapsed;
        }

        // Show at most ONE banner at a time (by priority) so notices never stack four-high and push the
        // accounts below the fold; any suppressed ones are counted onto the surviving banner.
        ConsolidateBanners();

        // Rebuilt every render. Holding element references across a render would leak the old tree, and the
        // keyboard tracks position by index precisely so it survives the 20-second refresh.
        _triageRows.Clear();
        CardsHost.Children.Clear();
        CardsHost.Spacing = _compact ? 4 : 8;
        if (snapshot.Entities.Count == 0)
        {
            KpiBand.Visibility = Visibility.Collapsed;
            LegendRow.Visibility = Visibility.Collapsed;
            HeroCard.Visibility = Visibility.Collapsed;

            // Distinguish "no accounts" from "accounts exist but haven't finished their first local-history
            // scan yet" — on startup the WhatsApp IndexedDB read takes a few seconds, and showing "no
            // accounts" during that window is misleading.
            if (instances.Count == 0)
            {
                // First-run / zero-professional-accounts: a proper centred empty state, not a bare line.
                CardsHost.Children.Add(new Shared.EmptyStateView
                {
                    IconGlyph = "", // Add
                    Title = "No accounts connected yet",
                    Hint = "Click + in the sidebar to add your first WhatsApp account, then mark it Professional to see its oversight here.",
                    Margin = new Thickness(0, 28, 0, 12)
                });
                return;
            }

            CardsHost.Children.Add(new TextBlock
            {
                Text = "Reading each account's local chat history — usually a few seconds…",
                Foreground = Brush("TextFillColorSecondaryBrush"),
                TextWrapping = TextWrapping.WrapWholeWords
            });

            // Shimmer placeholder cards (one per pending account, capped) instead of a bare text line.
            for (var i = 0; i < Math.Min(instances.Count, 3); i++)
            {
                CardsHost.Children.Add(BuildSkeletonCard());
            }

            return;
        }

        // "Needs reply" mode: a single flat, cross-account list of every awaiting customer, worst-first —
        // the unified "work through the backlog" view (replaces the standalone Work Queue page).
        if (needsReply)
        {
            LegendRow.Visibility = Visibility.Collapsed;
            BuildNeedsReplyList(instances);
            return;
        }

        var renderedCount = 0;
        if (grouping == OversightGrouping.ByLocation)
        {
            var instanceSnapshot = _services.Oversight.BuildSnapshot(OversightGrouping.ByInstance, instances);
            var byInstanceId = instanceSnapshot.Entities
                .GroupBy(entity => entity.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var location in snapshot.Entities)
            {
                var members = location.MemberInstanceIds
                    .Where(byInstanceId.ContainsKey)
                    .Select(id => byInstanceId[id])
                    .ToList();

                // A location matches if its own name matches (show all members) or any member matches
                // (show just the matching members). Non-matching locations are dropped entirely.
                var locationMatches = MatchesSearch(location.DisplayName);
                var visibleMembers = locationMatches
                    ? members
                    : members.Where(m => MatchesSearch(m.DisplayName)).ToList();
                if (!locationMatches && visibleMembers.Count == 0)
                {
                    continue;
                }

                CardsHost.Children.Add(BuildExpander(location, visibleMembers));
                renderedCount++;
            }
        }
        else
        {
            foreach (var entity in snapshot.Entities)
            {
                if (!MatchesSearch(entity.DisplayName))
                {
                    continue;
                }

                CardsHost.Children.Add(BuildRow(entity));
                renderedCount++;
            }
        }

        if (renderedCount == 0)
        {
            CardsHost.Children.Add(new TextBlock
            {
                Text = $"No accounts or locations match “{_searchQuery}”.",
                Foreground = Brush("TextFillColorSecondaryBrush"),
                TextWrapping = TextWrapping.WrapWholeWords
            });
        }

        // Legend explains the status bands + what the % means — only when health cards are on screen.
        LegendRow.Visibility = renderedCount > 0 && !_compact ? Visibility.Visible : Visibility.Collapsed;
    }

    private Expander BuildExpander(OversightEntityHealth location, IReadOnlyList<OversightEntityHealth> members)
    {
        var content = new StackPanel { Spacing = 6, Padding = new Thickness(8, 4, 4, 4) };
        if (members.Count == 0)
        {
            content.Children.Add(new TextBlock
            {
                Text = "No accounts in this location.",
                FontSize = UmScale.Text.Body,
                Foreground = Brush("TextFillColorSecondaryBrush")
            });
        }

        foreach (var member in members)
        {
            content.Children.Add(BuildRow(member));
        }

        var expander = new Expander
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Header = BuildHeader(location),
            Content = content
        };
        TrackExpansion(expander, location.Key);
        return expander;
    }

    // Auto-refresh rebuilds the rows; without this, every refresh would snap open accordions shut.
    private void TrackExpansion(Expander expander, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        expander.IsExpanded = _expandedKeys.Contains(key);
        expander.Expanding += (_, _) => _expandedKeys.Add(key);
        expander.Collapsed += (_, _) => _expandedKeys.Remove(key);
    }

    private FrameworkElement BuildRow(OversightEntityHealth entity)
    {
        // Each account is an accordion: the header is its health row; expanding reveals the actual
        // customers awaiting a reply (worst-first), each click-through to that chat. No navigation on
        // header click — the user picks the specific waiting customer from the list.
        var expander = new Expander
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = Brush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = Brush("UmHairlineBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = _compact ? new Thickness(14, 4, 14, 6) : new Thickness(16, 10, 16, 12),
            Header = BuildHeader(entity),
            Content = BuildAwaitingPanel(entity),
            IsExpanded = _expandedKeys.Contains(entity.Key)
        };

        // The Expander's header renders as a Button, and with no name on the Expander that button
        // announces only "button" — for the whole account card, on every card. Its children are all named
        // individually, so a screen reader could read the contents, but the control the user actually
        // focuses and activates said nothing about which account it was or what activating it does.
        // "open", not "waiting". The hero says "60 customers are waiting" (this week); a card saying
        // "130 customers waiting" uses identical words for that account's WHOLE open population. Two scopes
        // sharing one phrase in one viewport is how a number stops being believed.
        var awaitingSummary = entity.AwaitingCount == 1
            ? "1 open conversation"
            : $"{entity.AwaitingCount} open conversations";

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
            expander,
            $"{entity.DisplayName}: {awaitingSummary}. Expand to see who is waiting.");

        // Preserve open/closed state across the 20s auto-refresh re-render.
        expander.Expanding += (_, _) => _expandedKeys.Add(entity.Key);
        expander.Collapsed += (_, _) => _expandedKeys.Remove(entity.Key);

        // Full-height status rail to the left of the card — status by position+colour (the % hero glyph
        // carries the non-colour cue for WCAG). Stale accounts read critical.
        // The rail is a severity stripe, not an alarm. It used to be painted in full-saturation status
        // colour, so on a workspace where every account is behind — three of three here — the page showed
        // three tall red bars before the owner had read a single word. It now uses the status WASH, which
        // keeps the positional scan (colour down the left edge, same as before) while letting the verdict
        // chip inside the card carry the emphasis.
        var hasLiveData = entity.MeasuredCount > 0;
        var railBrush = entity.IsStale
            ? Brush("UmStatusDangerWashBrush")
            : !hasLiveData
                ? Brush("UmHairlineBrush")
                : StatusWashBrush(entity.OnTimePercent);
        var rail = new Border
        {
            Width = 4,
            CornerRadius = new CornerRadius(2),
            Background = railBrush,
            Margin = new Thickness(0, 2, 8, 2),
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var wrapper = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        wrapper.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        wrapper.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(rail, 0);
        Grid.SetColumn(expander, 1);
        wrapper.Children.Add(rail);
        wrapper.Children.Add(expander);
        return wrapper;
    }

    /// <summary>
    /// The accordion body for an account/location: the actual customers awaiting a reply (across its
    /// instances), worst-first, each a click-through to that WhatsApp conversation.
    /// </summary>
    /// <summary>
    /// A readable label for a waiting chat: WhatsApp's saved contact name when present, otherwise the
    /// phone number derived from the chat JID (unsaved numbers surface as a generic "New message" title).
    /// </summary>
    private static string FriendlyChatName(string? name, string? conversationKey)
    {
        if (!string.IsNullOrWhiteSpace(name) &&
            !name.Equals("New message", StringComparison.OrdinalIgnoreCase))
        {
            return name;
        }

        var key = conversationKey ?? string.Empty;
        if (key.Contains("@g.us", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(name) ? "Group chat" : name!;
        }

        // Only @c.us / @s.whatsapp.net ids are real phone numbers. @lid is a WhatsApp privacy id, not a
        // dialable number, so don't present it as one.
        var at = key.IndexOf('@');
        var local = at > 0 ? key[..at] : key;
        var isPhoneJid = key.Contains("@c.us", StringComparison.OrdinalIgnoreCase) ||
                         key.Contains("@s.whatsapp.net", StringComparison.OrdinalIgnoreCase);
        if (isPhoneJid && local.Length is >= 6 and <= 15 && local.All(char.IsDigit))
        {
            return "+" + local;
        }

        return "Unsaved contact";
    }

    private FrameworkElement BuildAwaitingPanel(OversightEntityHealth entity)
    {
        var secondary = Brush("TextFillColorSecondaryBrush");
        var danger = Brush("SystemFillColorCriticalBrush");
        var (windowStart, windowEnd) = WindowRange();

        var items = entity.MemberInstanceIds
            .SelectMany(id => OversightChatSnapshotService.Instance.GetAwaiting(id, windowStart, windowEnd)
                .Select(chat => (InstanceId: id, Chat: chat)))
            .OrderByDescending(x => x.Chat.Unread)
            .ThenByDescending(x => x.Chat.LastActivityUtc)
            .Take(100)
            .ToList();

        var panel = new StackPanel { Spacing = 1 };

        if (items.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = entity.HasChatData
                    ? "No chats awaiting a reply."
                    : "Still syncing this account — open it once if its WhatsApp Web is loading.",
                Foreground = secondary,
                Margin = new Thickness(4, 2, 4, 4)
            });
            return panel;
        }

        foreach (var (instanceId, chat) in items)
        {
            var (enrichedName, enrichedPreview) = OversightThreadEnricher.Enrich(instanceId, chat);
            var displayName = FriendlyChatName(enrichedName, chat.ConversationKey);

            var topLine = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            topLine.Children.Add(new TextBlock
            {
                Text = displayName,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 260,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            topLine.Children.Add(new TextBlock
            {
                // Read-but-not-replied chats are awaiting with 0 unread — label them clearly.
                Text = chat.Unread > 0 ? (chat.Unread == 1 ? "1 unread" : $"{chat.Unread} unread") : "needs reply",
                Foreground = danger,
                FontSize = UmScale.Text.Body,
                VerticalAlignment = VerticalAlignment.Center
            });

            var column = new StackPanel { Spacing = 1 };
            column.Children.Add(topLine);
            if (!string.IsNullOrWhiteSpace(enrichedPreview))
            {
                // A glimpse of the last message (from DOM ingress or sidebar preview).
                column.Children.Add(new TextBlock
                {
                    Text = enrichedPreview,
                    Foreground = secondary,
                    FontSize = UmScale.Text.Body,
                    MaxWidth = 360,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
            }

            var item = new Button
            {
                Content = column,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(8, 6, 8, 6),
                CornerRadius = new CornerRadius(6)
            };

            // Each needs-reply row is a Button whose Content is a panel, so it carries no accessible name
            // of its own: a screen-reader user heard "button" for every waiting customer, with no way to
            // tell one row from the next. This is the product's core workflow, so name it with the
            // customer and what activating it does.
            var rowName = string.IsNullOrWhiteSpace(chat.CustomerName)
                ? "Open conversation"
                : $"{chat.CustomerName}, open conversation";
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, rowName);

            var capturedInstanceId = instanceId;
            var capturedChat = chat;
            item.Click += (_, _) =>
                _services?.Navigation.OpenInstance(capturedInstanceId, capturedChat.ConversationKey, capturedChat.CustomerName, capturedChat.ContactPhone);
            panel.Children.Add(item);
        }

        return panel;
    }

    /// <summary>
    /// "Needs reply" mode: one flat, cross-account list of every chat awaiting a reply, worst-first (most
    /// unread, then longest-waiting). Each row click-throughs to the live chat. Reuses the same per-instance
    /// awaiting snapshot that powers the per-card accordion — no manual status bookkeeping, no drift.
    /// </summary>
    private void BuildNeedsReplyList(IReadOnlyList<MessengerInstance> instances)
    {
        var secondary = Brush("TextFillColorSecondaryBrush");
        var danger = Brush("SystemFillColorCriticalBrush");

        // Scope to one account/location when a card's awaiting pill was clicked.
        var scoped = instances;
        if (_needsReplyFilterIds is { Count: > 0 } filter)
        {
            scoped = instances.Where(i => filter.Contains(i.Id, StringComparer.OrdinalIgnoreCase)).ToList();
            CardsHost.Children.Add(BuildScopeChip(_needsReplyFilterLabel));
        }

        if (_locationFilter is { Length: > 0 } location)
        {
            scoped = scoped
                .Where(i => string.Equals(
                    BranchWorkspaceHelper.ResolveBranchKey(i),
                    location,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var everything = scoped
            .SelectMany(inst => OversightChatSnapshotService.Instance
                .GetAwaiting(inst.Id)
                .Select(chat => (Instance: inst, Chat: chat)))
            .ToList();

        // The chip row is built from the UNFILTERED set so each chip can show its own count. A filter whose
        // label does not say how much it will show is a guess the owner has to make by clicking.
        CardsHost.Children.Add(BuildQueueFilters(everything, instances));

        var rows = everything
            .Where(r => MatchesAgeFilter(r.Chat.LastActivityUtc))
            .Where(r => _facetFilter is null || QueueFacets.Resolve(r.Chat) == _facetFilter)
            .Take(400)
            .ToList();

        if (rows.Count == 0)
        {
            CardsHost.Children.Add(new TextBlock
            {
                Text = _needsReplyFilterIds is { Count: > 0 }
                    ? $"{_needsReplyFilterLabel} is all caught up — no customers waiting."
                    : "All caught up — no customers are waiting on a reply.",
                Foreground = secondary,
                TextWrapping = TextWrapping.WrapWholeWords,
                Margin = new Thickness(2, 6, 2, 0)
            });
            return;
        }

        // Aging-band summary so triage order is obvious at a glance.
        CardsHost.Children.Add(BuildAgingBands(rows.Select(r => r.Chat.LastActivityUtc)));

        // Grouped by branch (account): the account furthest behind first; oldest-waiting first within each,
        // so you work one branch's backlog at a time instead of a time-interleaved mix across accounts.
        var groups = rows
            .GroupBy(x => x.Instance.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => (
                g.First().Instance,
                Items: g.OrderBy(i => i.Chat.LastActivityUtc).ThenByDescending(i => i.Chat.Unread).ToList()))
            .OrderByDescending(g => g.Items.Count)
            .ThenBy(g => g.Instance.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var (inst, items) in groups)
        {
            CardsHost.Children.Add(BuildBranchHeader(inst, items.Count));
            foreach (var (rowInst, chat) in items)
            {
                CardsHost.Children.Add(BuildNeedsReplyRow(rowInst, chat, secondary, danger));
            }
        }
    }

    private bool MatchesAgeFilter(DateTimeOffset lastActivityUtc)
    {
        var days = Math.Max(1, AppSettingsService.Instance.Settings.AwaitingBacklogAfterDays);
        var age = DateTimeOffset.UtcNow - lastActivityUtc;

        return _ageFilter switch
        {
            AwaitingAgeFilter.Today => age < TimeSpan.FromDays(1),
            AwaitingAgeFilter.ThisWeek => age < TimeSpan.FromDays(days),
            AwaitingAgeFilter.Backlog => age >= TimeSpan.FromDays(days),
            _ => true
        };
    }

    /// <summary>
    /// The filter row above the Needs-reply queue: how old, which branch, and what the customer wants.
    ///
    /// <para>
    /// Every chip carries its own count, taken from the unfiltered set. That is the difference between a
    /// filter and a guess — an owner should be able to see that "At risk" holds three conversations before
    /// deciding whether to click it, and see that "Job &amp; training" holds nine before deciding to hide
    /// them. Chips with nothing in them are not rendered at all rather than offered as dead ends.
    /// </para>
    /// </summary>
    private FrameworkElement BuildQueueFilters(
        IReadOnlyList<(MessengerInstance Instance, OversightChatSnapshotService.ChatEntry Chat)> all,
        IReadOnlyList<MessengerInstance> allInstances)
    {
        var days = Math.Max(1, AppSettingsService.Instance.Settings.AwaitingBacklogAfterDays);
        var now = DateTimeOffset.UtcNow;
        var host = new StackPanel { Spacing = 6, Margin = new Thickness(2, 2, 2, 10) };

        // ---- Age ---------------------------------------------------------------------------------------
        var ageRow = NewChipRow("Waiting");
        void AddAge(AwaitingAgeFilter value, string label, Func<TimeSpan, bool> predicate, string tip)
        {
            var count = all.Count(r => predicate(now - r.Chat.LastActivityUtc));
            ageRow.Children.Add(BuildFilterChip(
                $"{label} · {count}",
                _ageFilter == value,
                tip,
                () =>
                {
                    _ageFilter = value;
                    ForceRerender();
                }));
        }

        AddAge(AwaitingAgeFilter.Today, "Today", a => a < TimeSpan.FromDays(1),
            "Arrived in the last 24 hours.");
        AddAge(AwaitingAgeFilter.ThisWeek, $"Last {days} days", a => a < TimeSpan.FromDays(days),
            $"Active in the last {days} days — this is the figure the headline above shows.");
        AddAge(AwaitingAgeFilter.Backlog, "Backlog", a => a >= TimeSpan.FromDays(days),
            $"Waiting longer than {days} days. Work through these separately from today's queue.");
        AddAge(AwaitingAgeFilter.All, "All", _ => true,
            "Every open conversation, whatever its age.");
        host.Children.Add(ageRow);

        // ---- Branch ------------------------------------------------------------------------------------
        // Grouping BY location already existed; filtering TO one did not, so an owner who wanted a single
        // branch had to type its name into a free-text box and hope they matched it.
        var byLocation = allInstances
            .Select(BranchWorkspaceHelper.ResolveBranchKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (byLocation.Count > 1)
        {
            var locationRow = NewChipRow("Branch");
            locationRow.Children.Add(BuildFilterChip("All branches", _locationFilter is null,
                "Every branch.", () => { _locationFilter = null; ForceRerender(); }));

            foreach (var key in byLocation)
            {
                var label = key;
                locationRow.Children.Add(BuildFilterChip(
                    label,
                    string.Equals(_locationFilter, key, StringComparison.OrdinalIgnoreCase),
                    $"Only accounts at {label}.",
                    () => { _locationFilter = key; ForceRerender(); }));
            }

            host.Children.Add(locationRow);
        }

        // ---- Kind of row -------------------------------------------------------------------------------
        // Facets, not topics. A missed call and an uncaptioned photo are not topics — they have no text to
        // classify — but they ARE different kinds of row with different actions, and the owner filters by
        // exactly that: do I type, do I call, or is this not even a customer?
        var facetCounts = new Dictionary<QueueFacet, int>();
        foreach (var row in all)
        {
            var facet = QueueFacets.Resolve(row.Chat);
            facetCounts[facet] = facetCounts.GetValueOrDefault(facet) + 1;
        }

        var facetRow = NewChipRow("Kind");
        facetRow.Children.Add(BuildFilterChip("Anything", _facetFilter is null,
            "No filter on the kind of row.", () => { _facetFilter = null; ForceRerender(); }));

        // Uncategorised is offered last and never hidden — most of the queue lands there until a model
        // replaces the lexicon, and pretending otherwise would misrepresent how much the app knows.
        foreach (var facet in QueueFacets.DisplayOrder)
        {
            if (!facetCounts.TryGetValue(facet, out var count) || count == 0)
            {
                continue;
            }

            // Missed calls are the one facet whose count depends on which reader an account is using.
            // On the IndexedDB fallback there is no callOutcome to read, so answered calls stay counted;
            // the number is over-stated and the owner has no way to know that from the chip alone.
            var describe = facet == QueueFacet.MissedCall && StoreBridgeHealth.AnyAccountOnFallback
                ? QueueFacets.Describe(facet)
                  + " Some accounts are on the fallback reader, which cannot tell an answered call from a "
                  + "missed one — so this count may be higher than the real number. See Settings → Data."
                : QueueFacets.Describe(facet);

            facetRow.Children.Add(BuildFilterChip(
                $"{QueueFacets.Label(facet)} · {count}",
                _facetFilter == facet,
                describe,
                () => { _facetFilter = facet; ForceRerender(); }));
        }

        host.Children.Add(facetRow);

        // A shortcut nobody knows about is worth nothing. One quiet line, and the "?" is a button so the
        // full list is reachable by mouse and by keyboard rather than only by knowing the key.
        var hint = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(56, 2, 0, 0)
        };
        hint.Children.Add(new TextBlock
        {
            Text = "J / K to move · Enter to open · D done · S snooze · R copy a reply",
            FontSize = UmScale.Text.Caption,
            Foreground = Brush("TextFillColorTertiaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });

        var helpButton = new Button
        {
            Content = new TextBlock { Text = "?", FontSize = UmScale.Text.Caption },
            Padding = new Thickness(6, 0, 6, 0),
            MinWidth = 0,
            MinHeight = 0,
            VerticalAlignment = VerticalAlignment.Center
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(helpButton, "Show keyboard shortcuts");
        helpButton.Click += (_, _) => ShowTriageHelp();
        hint.Children.Add(helpButton);
        host.Children.Add(hint);

        return host;
    }

    private StackPanel NewChipRow(string caption)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        row.Children.Add(new TextBlock
        {
            Text = caption,
            FontSize = UmScale.Text.Body,
            Width = 56,
            Foreground = Brush("TextFillColorSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });
        return row;
    }

    private FrameworkElement BuildFilterChip(string label, bool selected, string tooltip, Action onClick)
    {
        var button = new ToggleButton
        {
            Content = new TextBlock { Text = label, FontSize = UmScale.Text.Body },
            IsChecked = selected,
            Padding = new Thickness(10, 3, 10, 3),
            CornerRadius = new CornerRadius(12),
            MinWidth = 0,
            MinHeight = 0
        };

        // Selection is carried by the toggle's own checked visual, which is a colour change. The accessible
        // name repeats it in words so the state is not conveyed by colour alone (WCAG 1.4.1) and a screen
        // reader does not have to infer it from styling.
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
            button, selected ? $"{label}, selected" : label);

        ToolTipService.SetToolTip(button, tooltip);
        button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>A branch/account section header above that account's waiting customers in the Needs-reply list.</summary>
    private FrameworkElement BuildBranchHeader(MessengerInstance instance, int count)
    {
        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(2, 12, 2, 2)
        };

        var avatar = ProfileAvatarService.CreateAvatar(instance, 22);
        avatar.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(avatar);

        header.Children.Add(new TextBlock
        {
            Text = instance.DisplayName,
            FontWeight = FontWeights.SemiBold,
            FontSize = UmScale.Text.BodyStrong,
            VerticalAlignment = VerticalAlignment.Center
        });
        header.Children.Add(new TextBlock
        {
            Text = count == 1 ? "· 1 waiting" : $"· {count} waiting",
            FontSize = UmScale.Text.Caption,
            Opacity = 0.7,
            VerticalAlignment = VerticalAlignment.Center
        });
        return header;
    }

    /// <summary>
    /// A compact "how long have they been waiting" band summary above the Needs-reply list:
    /// &lt;15m · 15m–1h · 1–4h · &gt;4h, coloured by severity. Only non-empty bands render.
    /// </summary>
    private FrameworkElement BuildAgingBands(IEnumerable<DateTimeOffset> waitingSinceUtc)
    {
        var now = DateTimeOffset.UtcNow;
        var ages = waitingSinceUtc.Select(since => now - since).ToList();

        // The bands used to be fixed at 15m / 1h / 4h / >4h, which is right for a live inbox and useless
        // for a backlog: on real data that read "355 waiting >4h · 5 · 5" — 97% of the list in one bucket,
        // occupying prime space above the queue and telling the owner nothing they did not already know.
        //
        // So the scale follows the data. If most of the list has been waiting more than a day, hours stop
        // being the useful unit and the bands switch to days and weeks.
        var mostlyOld = ages.Count > 0 && ages.Count(a => a.TotalHours >= 24) * 2 > ages.Count;
        var bands = mostlyOld ? DayScaleBands : HourScaleBands;

        var counts = new int[bands.Length];
        foreach (var age in ages)
        {
            for (var i = 0; i < bands.Length; i++)
            {
                if (bands[i].UpperBound is null || age < bands[i].UpperBound)
                {
                    counts[i]++;
                    break;
                }
            }
        }

        var strip = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(2, 0, 0, 8)
        };

        void AddBand(int count, string label, string brushKey, string tip)
        {
            if (count == 0)
            {
                return;
            }

            var fg = Brush(brushKey);
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            content.Children.Add(new TextBlock { Text = count.ToString(), FontSize = UmScale.Text.BodyStrong, FontWeight = FontWeights.SemiBold, Foreground = fg, VerticalAlignment = VerticalAlignment.Center });
            content.Children.Add(new TextBlock { Text = label, FontSize = UmScale.Text.Body, Foreground = fg, VerticalAlignment = VerticalAlignment.Center });
            var chip = new Border
            {
                Background = Brush("CardBackgroundFillColorSecondaryBrush"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(9, 4, 9, 4),
                Child = content
            };
            ToolTipService.SetToolTip(chip, tip);
            strip.Children.Add(chip);
        }

        // Most-urgent first, so the eye lands on the worst band.
        for (var i = bands.Length - 1; i >= 0; i--)
        {
            AddBand(counts[i], bands[i].Label, bands[i].BrushKey, bands[i].Tooltip);
        }

        return strip;
    }

    private readonly record struct AgingBand(TimeSpan? UpperBound, string Label, string BrushKey, string Tooltip);

    /// <summary>Bands for a live queue, where minutes matter.</summary>
    private static readonly AgingBand[] HourScaleBands =
    [
        new(TimeSpan.FromMinutes(15), "<15m", "SystemFillColorSuccessBrush", "Just arrived — under 15 minutes."),
        new(TimeSpan.FromHours(1), "15m–1h", "SystemFillColorAttentionBrush", "Waiting 15 minutes to 1 hour."),
        new(TimeSpan.FromHours(4), "1–4h", "SystemFillColorCautionBrush", "Waiting 1 to 4 hours."),
        new(null, ">4h", "SystemFillColorCriticalBrush", "Waiting more than 4 hours — reply to these first.")
    ];

    /// <summary>
    /// Bands for a backlog, where hours are noise. Used once more than half the list has been waiting over
    /// a day — at which point ">4h" describes almost everything and distinguishes nothing.
    /// </summary>
    private static readonly AgingBand[] DayScaleBands =
    [
        new(TimeSpan.FromHours(24), "today", "SystemFillColorSuccessBrush", "Arrived in the last 24 hours."),
        new(TimeSpan.FromDays(7), "1–7d", "SystemFillColorAttentionBrush", "Waiting between a day and a week."),
        new(TimeSpan.FromDays(30), "1–4w", "SystemFillColorCautionBrush", "Waiting between a week and a month."),
        new(null, ">1 month", "SystemFillColorCriticalBrush", "Waiting more than a month — these are the ones costing you customers.")
    ];

    /// <summary>A "Showing: &lt;account&gt; ✕" chip above the scoped Needs-reply list; click clears the scope.</summary>
    private FrameworkElement BuildScopeChip(string label)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new TextBlock
        {
            Text = $"Showing: {label}",
            FontSize = UmScale.Text.Body,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        // "" (Cancel), not a literal character. Eight FontIcons in this app were written with the
        // glyph inline and reached the repo EMPTY — a FontIcon with a zero-length Glyph draws nothing, so
        // the control is invisible while remaining present, focusable and clickable. Use the escape form.
        content.Children.Add(new FontIcon { Glyph = "\uE711", FontSize = UmScale.Icon.Sm, VerticalAlignment = VerticalAlignment.Center });

        var chip = new Button
        {
            // Theme-correct (Button + card background can otherwise resolve the wrong theme — see needs-reply rows).
            Background = Services.ThemeBrushResolver.CardBackgroundSecondary(this),
            BorderBrush = Services.ThemeBrushResolver.CardStroke(this),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 0, 6),
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = content
        };
        ToolTipService.SetToolTip(chip, "Show every account's waiting customers");
        // Panel content again — a tooltip is not an accessible name, and screen readers do not announce it
        // in place of one.
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
            chip, $"Showing {label}. Activate to show every account's waiting customers.");
        chip.Click += (_, _) =>
        {
            _needsReplyFilterIds = null;
            _lastRenderSignature = string.Empty;
            Render();
        };
        return chip;
    }

    private FrameworkElement BuildNeedsReplyRow(
        MessengerInstance inst,
        OversightChatSnapshotService.ChatEntry chat,
        Brush secondary,
        Brush danger)
    {
        var accent = new SolidColorBrush(
            PlatformBrandingHelper.ParseAccentColor(inst.AccentColor ?? PlatformBrandingHelper.DefaultAccentHex));

        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new Border
        {
            Width = 3,
            CornerRadius = new CornerRadius(2),
            Background = accent,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0, 0, 10, 0)
        });

        var (enrichedName, enrichedPreview) = OversightThreadEnricher.Enrich(inst.Id, chat);
        var displayName = FriendlyChatName(enrichedName, chat.ConversationKey);

        var left = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(left, 1);
        left.Children.Add(new TextBlock
        {
            Text = displayName,
            FontWeight = FontWeights.SemiBold,
            FontSize = UmScale.Text.BodyStrong,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        if (!string.IsNullOrWhiteSpace(enrichedPreview))
        {
            left.Children.Add(new TextBlock
            {
                Text = enrichedPreview,
                Foreground = secondary,
                FontSize = UmScale.Text.Body,
                MaxWidth = 460,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }
        grid.Children.Add(left);

        var right = new StackPanel
        {
            Spacing = 1,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(right, 2);

        // Shape cue (WCAG 1.4.1): a warning glyph next to the count so "awaiting" isn't conveyed by the red
        // colour alone (the text "N unread" is also a non-colour cue; the glyph makes it shape-distinct too).
        var unreadLine = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        unreadLine.Children.Add(new FontIcon
        {
            Glyph = "\uE7BA", // Warning (ErrorBadge family) — Segoe Fluent
            FontSize = UmScale.Icon.Sm,
            Foreground = danger,
            VerticalAlignment = VerticalAlignment.Center
        });
        unreadLine.Children.Add(new TextBlock
        {
            Text = chat.Unread > 0 ? (chat.Unread == 1 ? "1 unread" : $"{chat.Unread} unread") : "needs reply",
            Foreground = danger,
            FontSize = UmScale.Text.Body,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        right.Children.Add(unreadLine);
        right.Children.Add(new TextBlock
        {
            Text = $"{inst.DisplayName} · {RelativeAge(chat.LastActivityUtc)}",
            Foreground = secondary,
            FontSize = UmScale.Text.Caption,
            HorizontalAlignment = HorizontalAlignment.Right
        });
        grid.Children.Add(right);

        var button = new Button
        {
            Content = grid,
            // Theme-correct surfaces (Application.Resources' Card* brushes can resolve the wrong theme here,
            // which painted these rows light-grey in dark mode). See ThemeBrushResolver.
            Background = Services.ThemeBrushResolver.CardBackground(this),
            BorderBrush = Services.ThemeBrushResolver.CardStroke(this),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = _compact ? new Thickness(12, 6, 12, 6) : new Thickness(14, 10, 14, 10)
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button,
            $"Open chat with {displayName} in {inst.DisplayName}");

        var capturedId = inst.Id;
        var capturedChat = chat;
        button.Click += (_, _) =>
            _services?.Navigation.OpenInstance(capturedId, capturedChat.ConversationKey, capturedChat.CustomerName, capturedChat.ContactPhone);

        // Row = the click-through button + an overflow menu (mark handled elsewhere / snooze).
        var rowGrid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(button, 0);
        rowGrid.Children.Add(button);

        // Actions sit beside the row in the order the owner decides between them: the one this KIND of row
        // calls for first, then the reply library, then done/snooze.
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0)
        };

        var facet = QueueFacets.Resolve(chat);

        if (QueueFacets.IsCallBack(facet) && BuildCallBackButton(chat, displayName) is { } callButton)
        {
            // A missed call cannot be answered by typing. 81 of these sat in the queue with no action that
            // made any sense for them.
            actions.Children.Add(callButton);
        }

        actions.Children.Add(BuildSavedReplyButton(inst, chat, displayName, facet));
        actions.Children.Add(BuildAwaitingActionButton(inst.Id, chat, displayName));

        Grid.SetColumn(actions, 1);
        rowGrid.Children.Add(actions);

        // Registered so a keypress can act on this row without holding a reference across a re-render.
        _triageRows.Add(new TriageRow(inst, chat, displayName, facet, rowGrid, button));
        return rowGrid;
    }

    /// <summary>One rendered queue row, so the keyboard can act on it. Rebuilt on every render.</summary>
    private sealed record TriageRow(
        MessengerInstance Instance,
        OversightChatSnapshotService.ChatEntry Chat,
        string DisplayName,
        QueueFacet Facet,
        FrameworkElement Container,
        Control Focusable);

    private readonly List<TriageRow> _triageRows = [];

    /// <summary>
    /// Dials a missed call through the system handler. Returns null when no number is known — a button that
    /// looks available but cannot work is worse than no button.
    /// </summary>
    private FrameworkElement? BuildCallBackButton(
        OversightChatSnapshotService.ChatEntry chat,
        string displayName)
    {
        var number = TelUriFor(chat);
        if (number is null)
        {
            return null;
        }

        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        content.Children.Add(new FontIcon { Glyph = "", FontSize = UmScale.Icon.Sm }); // Phone
        content.Children.Add(new TextBlock { Text = "Call back", FontSize = UmScale.Icon.Sm });

        var button = new Button
        {
            Content = content,
            Padding = _compact ? new Thickness(8, 4, 8, 4) : new Thickness(10, 6, 10, 6),
            VerticalAlignment = VerticalAlignment.Center
        };

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, $"Call {displayName} back");
        ToolTipService.SetToolTip(button, $"Dial {number[4..]} with your phone app.");
        button.Click += (_, _) => CallBack(chat, displayName);
        return button;
    }

    /// <summary>The dialable <c>tel:</c> URI for a chat, or null when no usable number is known.</summary>
    internal static string? TelUriFor(OversightChatSnapshotService.ChatEntry chat)
    {
        var digits = DigitsOf(chat.ContactPhone);
        if (digits.Length < 7)
        {
            // Fall back to the conversation key, which is a phone-number JID for a saved contact. An @lid
            // privacy JID is not a phone number, so its digits must not be dialled.
            var key = chat.ConversationKey ?? string.Empty;
            if (!key.Contains("@lid", StringComparison.OrdinalIgnoreCase))
            {
                digits = DigitsOf(key.Split('@')[0]);
            }
        }

        return digits.Length >= 7 ? $"tel:+{digits}" : null;
    }

    private static string DigitsOf(string? value) =>
        new((value ?? string.Empty).Where(char.IsDigit).ToArray());

    private void CallBack(OversightChatSnapshotService.ChatEntry chat, string displayName)
    {
        if (TelUriFor(chat) is not { } uri)
        {
            ShowTriageToast($"No phone number stored for {displayName}.");
            return;
        }

        // Goes through the same guarded launcher as a customer link, so the scheme allow-list applies here
        // too rather than this becoming a second, looser way to shell out.
        ShowTriageToast(WebViewNavigationGuard.TryOpenExternally(uri, userInitiated: true)
            ? $"Dialling {displayName}…"
            : "Windows has no app registered for placing calls.");
    }

    /// <summary>
    /// The saved-reply menu for a row: the library narrowed to this kind of conversation, most-used first.
    /// It copies and never sends, which is the app's standing rule and not a limitation to work around.
    /// </summary>
    private FrameworkElement BuildSavedReplyButton(
        MessengerInstance inst,
        OversightChatSnapshotService.ChatEntry chat,
        string displayName,
        QueueFacet facet)
    {
        var flyout = new MenuFlyout();
        var replies = SavedReplyStore.Instance.ForFacet(facet);

        if (replies.Count == 0)
        {
            flyout.Items.Add(new MenuFlyoutItem { Text = "No saved replies yet", IsEnabled = false });
        }

        foreach (var reply in replies.Take(8))
        {
            var item = new MenuFlyoutItem { Text = reply.Title };
            var captured = reply;
            item.Click += (_, _) => CopySavedReply(captured, inst, chat, displayName);
            flyout.Items.Add(item);
        }

        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        content.Children.Add(new FontIcon { Glyph = "", FontSize = UmScale.Icon.Sm }); // Copy
        content.Children.Add(new TextBlock { Text = "Reply", FontSize = UmScale.Icon.Sm });

        var button = new Button
        {
            Content = content,
            Padding = _compact ? new Thickness(8, 4, 8, 4) : new Thickness(10, 6, 10, 6),
            VerticalAlignment = VerticalAlignment.Center,
            Flyout = flyout
        };

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
            button, $"Copy a saved reply for {displayName}");
        ToolTipService.SetToolTip(button,
            "Copies a ready-made answer with this customer's details filled in. You paste and send it "
            + "yourself — the app never sends anything.");
        return button;
    }

    private void CopySavedReply(
        SavedReply reply,
        MessengerInstance inst,
        OversightChatSnapshotService.ChatEntry chat,
        string displayName)
    {
        var text = SavedReplyText.Fill(
            reply.Body, displayName, BranchWorkspaceHelper.ResolveBranchKey(inst), inst.DisplayName);

        var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
        package.SetText(text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);

        SavedReplyStore.Instance.RecordUse(reply.Id);
        ShowTriageToast($"“{reply.Title}” copied — paste it into the chat.");
    }

    /// <summary>
    /// A short confirmation for an action that has no other visible result. Copying to the clipboard and
    /// handing a number to the dialler both succeed silently otherwise, and an action with no feedback is
    /// one the owner repeats because they cannot tell whether it worked.
    /// </summary>
    private void ShowTriageToast(string message)
    {
        TriageToastText.Text = message;
        TriageToast.Visibility = Visibility.Visible;

        // Announced as well as shown: the owner may be driving the queue from the keyboard with their eyes
        // on the chat window rather than on this strip.
        var peer = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.FromElement(TriageToast)
            ?? Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(TriageToast);
        peer?.RaiseNotificationEvent(
            Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationKind.ActionCompleted,
            Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationProcessing.MostRecent,
            message,
            "triage-toast");

        _toastCts?.Cancel();
        _toastCts = new CancellationTokenSource();
        var token = _toastCts.Token;
        _ = HideToastAfterDelayAsync(token);
    }

    private CancellationTokenSource? _toastCts;

    private async Task HideToastAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(4), token).ConfigureAwait(true);
            TriageToast.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer message, which is already on screen.
        }
    }

    // ---- Keyboard triage --------------------------------------------------------------------------------

    /// <summary>
    /// Drives the reply queue from the keyboard.
    /// </summary>
    /// <remarks>
    /// Attached at the panel level rather than per row, because a row that has just been marked done is gone
    /// from the tree — a per-row handler would lose the keyboard the moment it was used, which is precisely
    /// when the owner is about to press the same key again.
    /// </remarks>
    private void OnTriageKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!IsNeedsReplyMode || _triageRows.Count == 0)
        {
            return;
        }

        var modifiers = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
        var ctrl = modifiers.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var alt = Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Menu)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        var typing = FocusManager.GetFocusedElement(XamlRoot) is TextBox or AutoSuggestBox or PasswordBox
                     or RichEditBox;

        var command = TriageKeyboard.Resolve(e.Key, ctrl || alt, typing);
        if (command == TriageCommand.None)
        {
            return;
        }

        e.Handled = true;

        switch (command)
        {
            case TriageCommand.Next:
            case TriageCommand.Previous:
            case TriageCommand.First:
            case TriageCommand.Last:
                _triageIndex = TriageKeyboard.Move(command, _triageIndex, _triageRows.Count);
                FocusTriageRow();
                return;
        }

        // Every remaining command acts on a row, so make sure one is selected first.
        if (_triageIndex < 0 || _triageIndex >= _triageRows.Count)
        {
            _triageIndex = 0;
            FocusTriageRow();
            if (command != TriageCommand.ShowHelp)
            {
                return; // the first press selects; the second acts
            }
        }

        var row = _triageRows[Math.Clamp(_triageIndex, 0, _triageRows.Count - 1)];

        switch (command)
        {
            case TriageCommand.Open:
                _services?.Navigation.OpenInstance(
                    row.Instance.Id, row.Chat.ConversationKey, row.Chat.CustomerName, row.Chat.ContactPhone);
                break;

            case TriageCommand.MarkDone:
                AwaitingOverrideStore.Instance.MarkHandled(
                    row.Instance.Id, row.Chat.ConversationKey, row.Chat.LastActivityUtc);
                ShowTriageToast($"{row.DisplayName} marked done.");
                AdvanceAfterRemoval();
                break;

            case TriageCommand.Snooze:
                AwaitingOverrideStore.Instance.Snooze(
                    row.Instance.Id, row.Chat.ConversationKey, DateTimeOffset.UtcNow.AddDays(1));
                ShowTriageToast($"{row.DisplayName} snoozed until tomorrow.");
                AdvanceAfterRemoval();
                break;

            case TriageCommand.CallBack:
                CallBack(row.Chat, row.DisplayName);
                break;

            case TriageCommand.CopyReply:
                var replies = SavedReplyStore.Instance.ForFacet(row.Facet);
                if (replies.Count > 0)
                {
                    CopySavedReply(replies[0], row.Instance, row.Chat, row.DisplayName);
                }
                else
                {
                    ShowTriageToast("No saved replies yet — add some in Settings.");
                }

                break;

            case TriageCommand.ShowHelp:
                ShowTriageHelp();
                break;
        }
    }

    /// <summary>True while the flat cross-account queue is the visible view.</summary>
    private bool IsNeedsReplyMode => NeedsReplyButton.IsChecked == true;

    /// <summary>
    /// Re-renders after a row leaves the queue, keeping the selection where the owner was rather than
    /// sending them back to the top of a list they were working down.
    /// </summary>
    private void AdvanceAfterRemoval()
    {
        var removed = _triageIndex;
        _lastRenderSignature = string.Empty;
        Render();
        _triageIndex = TriageKeyboard.IndexAfterRemoval(removed, _triageRows.Count);
        FocusTriageRow();
    }

    private void FocusTriageRow()
    {
        if (_triageIndex < 0 || _triageIndex >= _triageRows.Count)
        {
            return;
        }

        var row = _triageRows[_triageIndex];
        row.Focusable.Focus(FocusState.Keyboard);
        row.Container.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
    }

    private void ShowTriageHelp()
    {
        var body = new StackPanel { Spacing = 6 };
        foreach (var (keys, does) in TriageKeyboard.Shortcuts)
        {
            var line = new Grid();
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var k = new TextBlock { Text = keys, FontFamily = new FontFamily("Consolas"), FontSize = UmScale.Text.BodyStrong };
            var d = new TextBlock { Text = does, FontSize = UmScale.Text.BodyStrong, TextWrapping = TextWrapping.WrapWholeWords };
            Grid.SetColumn(d, 1);
            line.Children.Add(k);
            line.Children.Add(d);
            body.Children.Add(line);
        }

        _ = new ContentDialog
        {
            Title = "Keyboard shortcuts",
            Content = body,
            CloseButtonText = "Close",
            XamlRoot = XamlRoot
        }.ShowManagedAsync();
    }

    /// <summary>
    /// The per-chat action on an awaiting row — see <see cref="AwaitingChatActions"/>, which both this panel
    /// and the per-account drill-down share so the capability can't go missing from one of them again.
    /// </summary>
    private FrameworkElement BuildAwaitingActionButton(string instanceId, OversightChatSnapshotService.ChatEntry chat, string displayName) =>
        AwaitingChatActions.Build(instanceId, chat, displayName, () =>
        {
            _lastRenderSignature = string.Empty;
            Render();
        },
        compact: _compact);

    /// <summary>
    /// The accordion header: the health row, plus (when the account needs attention) an insight strip —
    /// a plain-language, on-device summary of what's waiting. Heuristic and instant: no cloud, no API, no
    /// AI runtime required, so it's always available at zero cost.
    /// </summary>
    private FrameworkElement BuildHeader(OversightEntityHealth entity)
    {
        var strip = BuildInsightStrip(entity);
        if (strip is null)
        {
            return BuildRowContent(entity);
        }

        var stack = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Stretch };
        stack.Children.Add(BuildRowContent(entity));
        stack.Children.Add(strip);
        return stack;
    }

    /// <summary>
    /// A chip naming this entity's session state — but only when there is something worth saying.
    /// A healthy (Working) session gets no chip: putting one on every card would turn the signal into
    /// wallpaper, and the whole point is that a stalled account stands out.
    /// </summary>
    /// <remarks>
    /// A location card rolls up to its <b>worst</b> member, so a branch with one signed-out account can't
    /// hide behind its healthy siblings — the same reasoning as the least-fresh freshness stamp above.
    /// </remarks>
    private FrameworkElement? BuildSessionStateChip(OversightEntityHealth entity)
    {
        var state = ResolveWorstSessionState(entity);
        if (state == SessionState.Working)
        {
            return null;
        }

        var (background, foreground) = state switch
        {
            SessionState.Failed => ("SystemFillColorCriticalBackgroundBrush", "SystemFillColorCriticalBrush"),
            SessionState.ScanQr => ("SystemFillColorCautionBackgroundBrush", "SystemFillColorCautionBrush"),
            SessionState.Degraded => ("SystemFillColorCautionBackgroundBrush", "SystemFillColorCautionBrush"),
            _ => ("SystemFillColorNeutralBackgroundBrush", "TextFillColorSecondaryBrush")
        };

        var label = SessionStateProjection.ToLabel(state);
        var chip = new Border
        {
            Background = Brush(background),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(6, 1, 6, 1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = label,
                FontSize = UmScale.Text.Caption,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush(foreground)
            }
        };

        var description = SessionStateProjection.ToDescription(state);
        ToolTipService.SetToolTip(chip, description);
        // Colour alone can't carry this (WCAG 1.4.1) — the text label does, and screen readers get the
        // full explanation rather than just the one-word chip.
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(chip, $"{label}. {description}");

        return chip;
    }

    private static SessionState ResolveWorstSessionState(OversightEntityHealth entity)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var worst = SessionState.Working;
        var sawAny = false;

        foreach (var instanceId in entity.MemberInstanceIds)
        {
            var state = SessionStateProjection.Resolve(instanceId, nowUtc);
            sawAny = true;
            if (SessionSeverity(state) > SessionSeverity(worst))
            {
                worst = state;
            }
        }

        return sawAny ? worst : SessionState.Working;
    }

    private static int SessionSeverity(SessionState state) => state switch
    {
        SessionState.Working => 0,
        SessionState.Starting => 1,
        SessionState.Degraded => 2,
        SessionState.ScanQr => 3,
        SessionState.Failed => 4,
        _ => 0
    };

    private static string RelativeAge(DateTimeOffset whenUtc)
    {
        var span = DateTimeOffset.UtcNow - whenUtc;
        if (span < TimeSpan.Zero)
        {
            span = TimeSpan.Zero;
        }

        if (span.TotalMinutes < 1)
        {
            return "just now";
        }
        if (span.TotalMinutes < 60)
        {
            var m = (int)Math.Round(span.TotalMinutes);
            return m == 1 ? "1 min ago" : $"{m} min ago";
        }
        if (span.TotalHours < 24)
        {
            var h = (int)Math.Round(span.TotalHours);
            return h == 1 ? "1 hr ago" : $"{h} hrs ago";
        }

        var d = (int)Math.Round(span.TotalDays);
        return d == 1 ? "1 day ago" : $"{d} days ago";
    }

    /// <summary>Compact age for tight KPI tiles: "now", "12m", "5h", "9d".</summary>
    private static string ShortAge(DateTimeOffset whenUtc)
    {
        var span = DateTimeOffset.UtcNow - whenUtc;
        if (span < TimeSpan.Zero)
        {
            span = TimeSpan.Zero;
        }

        if (span.TotalMinutes < 1)
        {
            return "now";
        }
        if (span.TotalMinutes < 60)
        {
            return $"{(int)Math.Round(span.TotalMinutes)}m";
        }
        if (span.TotalHours < 24)
        {
            return $"{(int)Math.Round(span.TotalHours)}h";
        }

        return $"{(int)Math.Round(span.TotalDays)}d";
    }

    /// <summary>Status colour bands shared by the % hero, KPI tiles, and card accent stripe.</summary>
    private Brush StatusBrushForPercent(int onTimePercent) => onTimePercent switch
    {
        >= 90 => Brush("SystemFillColorSuccessBrush"),
        >= 70 => Brush("SystemFillColorCautionBrush"),
        _ => Brush("SystemFillColorCriticalBrush"),
    };

    /// <summary>
    /// Populates the at-a-glance KPI band from per-instance health: overall caught-up %, total awaiting
    /// (+ accounts behind), total past-SLA, and the single oldest waiting conversation. Computed from the
    /// by-instance entities so the headline numbers are stable across grouping modes.
    /// </summary>
    /// <summary>
    /// Renders a delta for a KPI tile. Colour follows the delta's SENTIMENT, not its arrow: response time
    /// falling is a down-arrow in green, while a neutral-polarity metric moving either way stays muted.
    /// </summary>
    private static (string Text, Brush? Brush) FormatKpiDelta(MetricDelta delta, Brush neutral)
    {
        if (!delta.HasData || delta.Direction == DeltaDirection.None)
        {
            return (string.Empty, null);
        }

        var arrow = delta.Direction == DeltaDirection.Up ? "▲" : "▼";
        var brush = delta.Sentiment switch
        {
            DeltaSentiment.Favourable => UmSemanticBrushes.Get("UmStatusSuccessBrush"),
            DeltaSentiment.Adverse => UmSemanticBrushes.Get("UmStatusDangerBrush"),
            _ => neutral
        };

        return ($"{arrow} {delta.Percent}%", brush);
    }

    /// <summary>
    /// The line under the "Needs a reply" figure. Ordered by what the owner can do something about:
    /// chats the app could not read first (those are the ones where the number itself is uncertain),
    /// then the backlog, then how many accounts are behind.
    /// </summary>
    /// <summary>
    /// The line under the Backlog figure. Says what the hero cannot: how far behind the older queue is,
    /// and whether any of today's queue could not be read.
    /// </summary>
    internal static string BuildBacklogHint(
        OversightChatSnapshotService.AwaitingSplit split,
        int accountsBehind)
    {
        if (split.Backlog <= 0)
        {
            return BuildAwaitingHint(split, accountsBehind);
        }

        var parts = new List<string>(2) { $"{split.NeedsReply} need a reply now" };
        if (split.Unreadable > 0)
        {
            parts.Add($"{split.Unreadable} unreadable");
        }

        return string.Join(" · ", parts);
    }

    internal static string BuildAwaitingHint(
        OversightChatSnapshotService.AwaitingSplit split,
        int accountsBehind)
    {
        var parts = new List<string>(3);

        // Said first and said plainly. A scrape that failed to read message bodies otherwise looks
        // exactly like a quiet morning, and the owner would have no way to tell the difference.
        if (split.Unreadable > 0)
        {
            parts.Add($"{split.Unreadable} unreadable");
        }

        if (split.Backlog > 0)
        {
            parts.Add($"{split.Backlog} older");
        }

        if (parts.Count == 0)
        {
            return accountsBehind switch
            {
                0 => "all accounts clear",
                1 => "1 account behind",
                _ => $"{accountsBehind} accounts behind"
            };
        }

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// The tooltip has to account for the whole population, because the headline is now a subset of it.
    /// An owner who remembers a bigger number is owed an explanation of where the rest went.
    /// </summary>
    internal static string BuildAwaitingTooltip(OversightChatSnapshotService.AwaitingSplit split)
    {
        var text = "Customers waiting on a reply, active in the last "
            + $"{Math.Max(1, AppSettingsService.Instance.Settings.AwaitingBacklogAfterDays)} days. "
            + "Click to see them, most urgent first.";

        if (split.Backlog > 0)
        {
            text += $"\n{split.Backlog} more have been waiting longer than that.";
        }

        if (split.ClosedAutomatically > 0)
        {
            text += $"\n{split.ClosedAutomatically} were not counted because the customer's last message "
                + "only said something like \"ok\" or \"thanks\". Settings → Data lists them.";
        }

        if (split.Unreadable > 0)
        {
            text += $"\n{split.Unreadable} are counted but their message could not be read.";
        }

        return text;
    }

    private void RenderKpiBand(
        IReadOnlyList<OversightEntityHealth> entities,
        IReadOnlyList<MessengerInstance> instances,
        DateTimeOffset? rangeStart,
        DateTimeOffset? rangeEnd)
    {
        var secondary = Brush("TextFillColorSecondaryBrush");
        var success = Brush("SystemFillColorSuccessBrush");
        var primary = Brush("TextFillColorPrimaryBrush");
        var caution = Brush("SystemFillColorCautionBrush");
        var tiles = new List<KpiTileViewModel>(6);

        // Caught-up %: measured-count-weighted average across accounts that actually have live data.
        var live = entities.Where(e => e.MeasuredCount > 0).ToList();
        var measured = live.Sum(e => e.MeasuredCount);
        int? overallPct = null;
        if (measured > 0)
        {
            var weighted = (int)Math.Round(live.Sum(e => (long)e.OnTimePercent * e.MeasuredCount) / (double)measured);

            // Same honesty rule the per-entity percentage uses, applied again here because a weighted
            // average re-introduces the problem: a large fully-caught-up account beside a small one at 90%
            // averages to 99.9, which rounds back up to 100 — so the headline tile would read "100% caught
            // up" while the hero beside it reads "1 customer is waiting". The whole business is only 100%
            // caught up when every measured account is.
            overallPct = weighted >= 100 && live.Any(e => e.OnTimePercent < 100) ? 99 : weighted;
        }

        var totalAwaiting = entities.Sum(e => e.AwaitingCount);
        var behind = entities.Count(e => e.AwaitingCount > 0);

        // Split the waiting customers into today's work and the backlog behind it. One number could not
        // carry both: measured on real data the tile read 466 with the oldest at 82 days, which is true,
        // unreadable, and hid a complaint and a customer threatening to leave. 341 of those were over a
        // week old — a backlog, not a queue, and mixing the two is what made the number unusable.
        var awaitingSplit = OversightChatSnapshotService.Instance.BuildAwaitingSplit(
            instances.Select(i => i.Id));

        // Record the number the tile SHOWS, so the sparkline underneath it cannot tell a different story
        // than the figure above it. This does step down on the release that introduces the split — the
        // definition changed, and drawing the trend against the old definition would be the fiction.
        // ...but only while the tile is showing TODAY. The store is a daily history keyed by today's date,
        // and overallPct is scoped to the selected range while awaitingSplit is always current-state — so
        // opening "last 30 days" wrote a 30-day average into today's slot and permanently replaced the real
        // reading. The sparkline then told a story about a day that never happened.
        if (overallPct is { } recPct && rangeStart is null && rangeEnd is null)
        {
            KpiTrendStore.Instance.Record(recPct, awaitingSplit.NeedsReply);
        }

        tiles.Add(new KpiTileViewModel
        {
            // The one tile allowed to be loud. The hero states the queue; this states the health, and it
            // is the figure the owner is judged on. Everything else in the band is context and is drawn a
            // step quieter so the eye has somewhere to land.
            IsPrimary = true,
            Label = "Caught up",
            Value = overallPct is { } p ? $"{p}%" : "—",
            ValueBrush = overallPct is { } pp ? StatusBrushForPercent(pp) : secondary,
            Hint = "unread cleared, across accounts",
            ActionKey = overallPct is null ? string.Empty : "caughtup",
            Trend = KpiTrendStore.Instance.GetCaughtUpTrend(),
            Tooltip = "Share of active chats with no unread messages. This measures unread cleared — not reply speed (see Response time)."
        });

        // The hero already renders this figure at 56px. Repeating it at 32px a hundred pixels below read as
        // two facts rather than one emphasised fact, so this tile now carries the half the hero CANNOT
        // show: the backlog, its trend, and how much of today's queue is unreadable. Nothing is lost and
        // the duplication is gone.
        tiles.Add(new KpiTileViewModel
        {
            Label = awaitingSplit.Backlog > 0 ? "Backlog" : "Needs a reply",
            Value = awaitingSplit.Backlog > 0
                ? awaitingSplit.Backlog.ToString()
                : awaitingSplit.NeedsReply.ToString(),
            // Neutral ink, like every tile except the health figure. A band where six numbers each pick
            // their own semantic colour has no hierarchy — the eye has nowhere to land, and a genuinely
            // critical figure looks exactly like a routine one. The status lives in the hint and in the
            // account rows' verdict chips, which state it in words rather than in hue alone.
            ValueBrush = primary,
            Hint = BuildBacklogHint(awaitingSplit, behind),
            ActionKey = awaitingSplit.TotalOpen > 0 ? "awaiting" : string.Empty,
            Trend = KpiTrendStore.Instance.GetAwaitingTrend(),
            Tooltip = BuildAwaitingTooltip(awaitingSplit)
        });

        // Response time (FRT) + SLA compliance — forward-tracked from real message timestamps.
        var slaThreshold = AppSettingsService.Instance.Settings.SlaThresholdMinutes;
        var response = ResponseTimeTracker.Instance.GetStats(instances, rangeStart, rangeEnd, slaThreshold);

        // Change vs the prior equal-length period. Only meaningful when the range is bounded — an
        // all-time view has no "previous period" to compare against, so no delta is shown rather than a
        // fabricated one.
        var responseDelta = MetricDelta.None;
        var slaDelta = MetricDelta.None;
        if (rangeStart is { } periodStart && response.HasData)
        {
            var periodEnd = rangeEnd ?? DateTimeOffset.Now;
            var prior = ResponseTimeTracker.Instance.GetStats(
                instances, periodStart - (periodEnd - periodStart), periodStart, slaThreshold);
            if (prior.HasData)
            {
                responseDelta = ChartSeriesBuilder.ComputeDelta(
                    response.MedianMinutes, prior.MedianMinutes, MetricPolarity.LowerIsBetter);
                slaDelta = ChartSeriesBuilder.ComputeDelta(
                    response.SlaCompliancePercent, prior.SlaCompliancePercent, MetricPolarity.HigherIsBetter);
            }
        }

        var (responseDeltaText, responseDeltaBrush) = FormatKpiDelta(responseDelta, secondary);
        var (slaDeltaText, slaDeltaBrush) = FormatKpiDelta(slaDelta, secondary);

        tiles.Add(new KpiTileViewModel
        {
            Label = "Response time",
            Value = response.HasData ? FormatMinutes(response.MedianMinutes) : "—",
            ValueBrush = response.HasData ? primary : secondary,
            Delta = responseDeltaText,
            DeltaBrush = responseDeltaBrush,
            Hint = response.HasData ? $"median · {response.SampleCount} {(response.SampleCount == 1 ? "reply" : "replies")}" : "builds as you reply",
            Tooltip = $"Median time from a customer's message to your first reply (measured live since tracking began). Target: under {slaThreshold} min."
        });

        tiles.Add(new KpiTileViewModel
        {
            Label = "SLA met",
            Value = response.HasData ? $"{response.SlaCompliancePercent}%" : "—",
            ValueBrush = response.HasData ? primary : secondary,
            Delta = slaDeltaText,
            DeltaBrush = slaDeltaBrush,
            // The DENOMINATOR, not just the threshold. "SLA met 0%" computed from a single reply looked
            // pixel-for-pixel identical to 0% computed from two hundred, and the dashboard defaults its
            // window to Today — so early in the day this tile headlines a percentage built from one or two
            // samples. The response-time tile beside it already says "median · N replies"; this one said
            // only what the target was. Measured 2026-08-29: Today showed 0% from one reply while the same
            // week read 83%, and both were correct.
            Hint = response.HasData
                ? $"{response.SampleCount} {(response.SampleCount == 1 ? "reply" : "replies")} · target {slaThreshold} min"
                : $"target {slaThreshold} min",
            Tooltip = $"Share of replies sent within your {slaThreshold}-minute SLA target. Adjust the target in Settings → Session & performance."
        });

        // Messages/day — 7-day inbound average + change vs the prior week (from the activity history log).
        var perDay = MessageAnalyticsService.Instance.GetMessagesPerDay(instances);
        var perDayDelta = string.Empty;
        Brush? perDayDeltaBrush = null;
        if (perDay is { HasData: true, DeltaCount: not 0 })
        {
            var up = perDay.DeltaCount > 0;
            perDayDelta = $"{(up ? "▲" : "▼")} {Math.Abs(perDay.DeltaCount)}";
            perDayDeltaBrush = up ? success : secondary;
        }

        tiles.Add(new KpiTileViewModel
        {
            Label = "Messages / day",
            Value = perDay.HasData ? perDay.AveragePerDay.ToString() : "—",
            ValueBrush = perDay.HasData ? primary : secondary,
            Delta = perDayDelta,
            DeltaBrush = perDayDeltaBrush,
            Hint = "7-day average",
            ActionKey = perDay.HasData ? "busiest" : string.Empty,
            Tooltip = "Average inbound customer messages per day over the last 7 days, vs the prior week. Click to open the activity graph."
        });

        // Busiest window — peak inbound hour + day (from the same history log feeding the graph).
        var (busyHour, busyDay) = MessageAnalyticsService.Instance.GetBusiestWindow(instances);
        tiles.Add(new KpiTileViewModel
        {
            Label = "Busiest window",
            Value = busyHour,
            ValueBrush = busyHour == "—" ? secondary : primary,
            Hint = busyDay == "—" ? "peak hour" : $"peak hour · {busyDay}",
            ActionKey = busyHour == "—" ? string.Empty : "busiest",
            Tooltip = "Your peak inbound hour and weekday — plan coverage around it. Click to open the activity graph."
        });

        KpiBand.ItemsSource = tiles;
        KpiBand.Visibility = Visibility.Visible;

        // The hero shows the live queue but judges "all caught up" on the WHOLE open population. Those
        // have to be different numbers or the split reintroduces the exact defect F-STATE-01 closed: an
        // owner told they are caught up while a three-month backlog sits behind the claim.
        RenderHero(overallPct, awaitingSplit.TotalOpen, behind, entities, instances, awaitingSplit.NeedsReply);
        RenderBriefing(
            entities, instances, overallPct, awaitingSplit.TotalOpen, behind, busyHour, awaitingSplit.NeedsReply);
    }

    /// <summary>
    /// The hero answer at the top of the command center: the one thing the owner needs at a glance —
    /// "You're all caught up" or "N waiting" — with the oldest wait, the account furthest behind, and a jump
    /// to the backlog. Sized far larger than the supporting KPI tiles so the 5-second scan lands here first.
    /// </summary>
    private void RenderHero(
        int? overallPct,
        int totalAwaiting,
        int accountsBehind,
        IReadOnlyList<OversightEntityHealth> entities,
        IReadOnlyList<MessengerInstance> instances,
        int? liveAwaiting = null)
    {
        if (overallPct is null)
        {
            // No live data yet (first launch, still syncing) — the skeleton/empty state carries this.
            HeroCard.Visibility = Visibility.Collapsed;
            return;
        }

        // `totalAwaiting == 0` alone was not enough to claim caught-up. An account whose read failed
        // contributes zero awaiting because there is nothing to count, so a branch dropping out of the
        // rollup pushed this number DOWN — and the hero showed a green tick while the card underneath
        // said "couldn't read". See CaughtUpClaim / F-STATE-01.
        var claim = CaughtUpClaim.Resolve(entities, totalAwaiting);
        var caughtUp = claim.CanClaim;

        // Semantic red/green pops the same in both themes, so the app-level Brush() lookup is fine here.
        // "Nothing waiting but incomplete" gets caution rather than success or danger: there is no known
        // backlog, but the tick would be a lie.
        var accent = caughtUp
            ? Brush("SystemFillColorSuccessBrush")
            : claim.NothingWaitingButIncomplete
                ? Brush("SystemFillColorCautionBrush")
                : Brush("SystemFillColorCriticalBrush");
        // NOTE: neutral text (primary/secondary) must NOT be fetched via Brush() — that resolves the app's
        // default (dark) theme, so it renders near-white and vanishes on the light hero. Let the primary text
        // INHERIT the element-themed default foreground, and dim the secondary line with Opacity instead.

        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var rail = new Border
        {
            Width = 4,
            CornerRadius = new CornerRadius(2),
            Background = accent,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Grid.SetColumn(rail, 0);
        grid.Children.Add(rail);

        var text = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(text, 1);

        var headline = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (caughtUp)
        {
            headline.Children.Add(new FontIcon
            {
                Glyph = "", // checkmark
                FontSize = UmScale.Icon.Lg,
                Foreground = accent,
                VerticalAlignment = VerticalAlignment.Center
            });
            headline.Children.Add(new TextBlock
            {
                Text = CaughtUpClaim.Headline(claim),
                FontSize = UmScale.Text.Metric,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        else if (claim.NothingWaitingButIncomplete)
        {
            // Nothing is waiting in what could be read, but something could not be. Rendering the big "0"
            // here would be the same overclaim in a different font — it states a count the app does not
            // actually have. A warning glyph and an explicit headline instead.
            headline.Children.Add(new FontIcon
            {
                Glyph = "\uE7BA", // warning
                FontSize = UmScale.Icon.Lg,
                Foreground = accent,
                VerticalAlignment = VerticalAlignment.Center
            });
            headline.Children.Add(new TextBlock
            {
                Text = CaughtUpClaim.Headline(claim),
                FontSize = UmScale.Text.Title,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.WrapWholeWords,
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        else
        {
            // The figure is the live queue; the caught-up claim above still needed the full open count.
            var shown = liveAwaiting ?? totalAwaiting;
            headline.Children.Add(new TextBlock
            {
                Text = shown.ToString(),
                FontSize = UmScale.Text.Hero,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = accent,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -2, 0, 0)
            });
            headline.Children.Add(new TextBlock
            {
                Text = shown == 1 ? "customer is waiting\nfor a reply" : "customers are waiting\nfor a reply",
                FontSize = UmScale.Text.Subtitle,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                LineHeight = 19,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        text.Children.Add(headline);

        text.Children.Add(new TextBlock
        {
            Text = BuildHeroSubtext(claim, overallPct.Value, accountsBehind, entities, instances),
            FontSize = UmScale.Text.Body,
            Opacity = 0.75, // dims the inherited (theme-correct) foreground instead of forcing a brush
            TextWrapping = TextWrapping.WrapWholeWords
        });

        grid.Children.Add(text);

        if (!caughtUp)
        {
            var cta = new Button
            {
                Content = "Review now  →",
                Style = (Style)Application.Current.Resources["AccentButtonStyle"],
                VerticalAlignment = VerticalAlignment.Center
            };
            cta.Click += (_, _) => SelectMode(NeedsReplyButton);
            Grid.SetColumn(cta, 2);
            grid.Children.Add(cta);
        }

        HeroCard.Child = grid;
        HeroCard.BorderBrush = Brush("UmHairlineBrush");
        HeroCard.Visibility = Visibility.Visible;
        // (Depth via ThemeShadow was removed — the accent rail + large number already give the hero enough
        //  visual weight, and the imperative Shadow+Translation was an unverified, finicky variable.)
    }

    /// <summary>The hero's supporting line — oldest wait + the account furthest behind + overall caught-up %.</summary>
    private string BuildHeroSubtext(
        CaughtUpClaim.Verdict claim,
        int overallPct,
        int accountsBehind,
        IReadOnlyList<OversightEntityHealth> entities,
        IReadOnlyList<MessengerInstance> instances)
    {
        if (claim.CanClaim)
        {
            // "No customers are waiting" is only true when nothing predates the window either. With
            // "Today" selected and a week-old thread still unanswered, the unqualified line was false in
            // exactly the way that costs a customer.
            return claim.CaughtUpButCarryingBacklog
                ? $"{CaughtUpClaim.CarriedBacklogClause(claim)} · {overallPct}% caught up overall."
                : $"No customers are waiting on a reply · {overallPct}% caught up overall.";
        }

        if (claim.NothingWaitingButIncomplete)
        {
            // Say which accounts are missing rather than a bare reassurance. Without this the line read
            // "No customers are waiting on a reply", which is only true of the accounts that answered.
            return $"Nothing waiting in what could be read, but {CaughtUpClaim.IncompleteClause(claim)} " +
                   $"· {overallPct}% caught up across the rest.";
        }

        var parts = new List<string>(3);

        // The oldest wait must be attributed to the account it actually belongs to, and must be measured
        // over the SAME window-bounded awaiting snapshot the per-account cards use.
        //
        // This line previously read "oldest 75d · <name of the account with the most awaiting>", which the
        // " · " join renders as one sentence — so it claimed the 75-day-old customer was at that account.
        // It usually was not. Observed live: the hero read "oldest 75d · Depilex DHA-2 WhatsApp" while that
        // account's own card read "Longest wait: 50d"; the 75d belonged to a different account entirely.
        // The two figures also came from different windows (an unbounded digest here vs. the card's
        // WindowRange()), so they could disagree even for the same account.
        var (windowStart, windowEnd) = WindowRange();
        var nowUtc = DateTimeOffset.UtcNow;

        (string Name, double Minutes)? oldestWait = null;
        foreach (var entity in entities)
        {
            foreach (var chat in entity.MemberInstanceIds.SelectMany(id =>
                         OversightChatSnapshotService.Instance.GetAwaiting(id, windowStart, windowEnd)))
            {
                var minutes = (nowUtc - chat.LastActivityUtc).TotalMinutes;
                if (oldestWait is null || minutes > oldestWait.Value.Minutes)
                {
                    oldestWait = (entity.DisplayName, minutes);
                }
            }
        }

        var worst = entities.Where(e => e.AwaitingCount > 0)
            .OrderByDescending(e => e.AwaitingCount)
            .ThenBy(e => e.OnTimePercent)
            .FirstOrDefault();

        var worstName = worst is not null && !string.IsNullOrWhiteSpace(worst.DisplayName)
            ? worst.DisplayName
            : null;

        return ComposeHeroSubtext(
            oldestWait?.Name,
            oldestWait?.Minutes,
            worstName);
    }

    /// <summary>
    /// Formats the hero's supporting line from already-resolved facts.
    /// </summary>
    /// <remarks>
    /// Split out from <see cref="BuildHeroSubtext"/> so the attribution contract is testable without a
    /// live snapshot service. The contract that matters: whatever name appears beside "oldest" must be the
    /// account that oldest wait actually belongs to. Joining the parts with " · " makes them read as one
    /// sentence, so an unlabelled name next to a duration is read as owning that duration — which is how
    /// this line came to claim a 75-day-old customer was at an account whose own longest wait was 50 days.
    /// </remarks>
    internal static string ComposeHeroSubtext(
        string? oldestAccountName,
        double? oldestMinutes,
        string? worstAccountName)
    {
        var parts = new List<string>(3);

        if (oldestMinutes is { } minutes && minutes >= 1)
        {
            // Name the owning account only when it is not the one already named as furthest behind,
            // so the line does not repeat itself in the common case.
            var attributed = !string.IsNullOrWhiteSpace(oldestAccountName)
                             && !string.Equals(oldestAccountName, worstAccountName, StringComparison.Ordinal);

            parts.Add(attributed
                ? $"oldest {FormatMinutes(minutes)} ({oldestAccountName})"
                : $"oldest {FormatMinutes(minutes)}");
        }

        if (!string.IsNullOrWhiteSpace(worstAccountName))
        {
            // Labelled, so it reads as its own fact rather than as a qualifier on the oldest wait.
            parts.Add($"furthest behind: {worstAccountName}");
        }

        // "N% caught up overall" used to be appended here as well. It is the Caught up tile's entire job,
        // rendered 55px below in larger type — saying it twice made the subtext longer without making it
        // more informative. The subtext keeps only what no tile carries: which account is oldest and which
        // is furthest behind.
        //
        // The percentage PARAMETER went with it. Leaving an unused argument in place is what let this drift
        // go unnoticed: two tests kept asserting the old sentence, kept failing in CI, and read as though
        // they were still exercising something real. A signature that no longer takes the number cannot be
        // mistaken for one that still uses it.
        return string.Join(" · ", parts);
    }

    private static string FormatMinutes(double minutes)
    {
        if (minutes < 1)
        {
            return "<1m";
        }

        if (minutes < 60)
        {
            return $"{Math.Round(minutes)}m";
        }

        var hours = minutes / 60.0;
        return hours < 24 ? $"{hours:0.#}h" : $"{Math.Round(hours / 24.0)}d";
    }

    private Brush ResponseBrush(double medianMinutes, int slaThreshold) =>
        medianMinutes <= slaThreshold ? Brush("SystemFillColorSuccessBrush")
        : medianMinutes <= slaThreshold * 2 ? Brush("SystemFillColorCautionBrush")
        : Brush("SystemFillColorCriticalBrush");

    /// <summary>Routes a KPI tile tap to the matching drill-down (mode switch, account jump, or activity graph).</summary>
    // Click, not Tapped: the tiles are Buttons now so keyboard activation reaches the same handler. Tapped
    // is a pointer-only event, which is why these drill-downs were unreachable without a mouse.
    private void OnKpiTileTapped(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string action || string.IsNullOrEmpty(action))
        {
            return;
        }

        switch (action)
        {
            case "awaiting":
                SelectMode(NeedsReplyButton);
                break;
            case "caughtup":
                OnAttentionJump(sender, e);
                break;
            case "busiest":
            case "messages":
                DashboardActivityRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    /// <summary>Raised when a KPI tile asks to open the activity graph (the dashboard scrolls it into view).</summary>
    public event EventHandler? DashboardActivityRequested;

    private const string BriefingSystemPrompt =
        "You are an operations assistant for a multi-location business owner monitoring WhatsApp customer " +
        "chats. You are given only aggregate counts across the owner's OWN business accounts (account names " +
        "are the owner's labels — fine to mention). Reply with EXACTLY ONE short start-of-shift line (max 24 " +
        "words) telling the owner where to focus first. Plain sentence, no greeting, no markdown, no quotes. " +
        "Never invent customer names or message text.";

    /// <summary>
    /// #25 AI shift briefing: a one-line, whole-business "where to focus first" summary under the KPI band.
    /// Always shows a deterministic heuristic; when local AI is on it swaps in a model-phrased line (cached,
    /// degrades to the heuristic). Hidden when there's nothing to brief.
    /// </summary>
    private void RenderBriefing(
        IReadOnlyList<OversightEntityHealth> entities,
        IReadOnlyList<MessengerInstance> instances,
        int? overallPct,
        int totalAwaiting,
        int accountsBehind,
        string busyHour,
        int? liveAwaiting = null)
    {
        if (overallPct is null)
        {
            BriefingStrip.Visibility = Visibility.Collapsed;
            return;
        }

        // #34 ranking rationale: the account furthest behind (most awaiting, then lowest caught-up %).
        var worst = entities.Where(e => e.AwaitingCount > 0)
            .OrderByDescending(e => e.AwaitingCount)
            .ThenBy(e => e.OnTimePercent)
            .FirstOrDefault();

        // #36 end-of-day projection + #33 anomaly (today's pace vs the recent daily average).
        var eod = MessageAnalyticsService.Instance.GetEndOfDayProjection(instances);
        var perDay = MessageAnalyticsService.Instance.GetMessagesPerDay(instances);
        var busierThanUsual = eod.HasData && perDay is { HasData: true, AveragePerDay: > 0 }
            && eod.Projected >= (int)Math.Round(perDay.AveragePerDay * 1.4);
        var projectionNote = eod.HasData && eod.Projected > eod.SoFar
            ? $" On pace for ~{eod.Projected} messages today{(busierThanUsual ? " — busier than usual" : string.Empty)}."
            : busierThanUsual ? " Busier than usual today." : string.Empty;

        var busy = busyHour is "—" or "" ? string.Empty : $" Busiest around {busyHour}.";

        // Same honesty gate as the hero — the briefing sits directly beneath it and must not contradict
        // it, and it had the identical `totalAwaiting == 0` blind spot.
        var claim = CaughtUpClaim.Resolve(entities, totalAwaiting);

        string heuristic;
        if (claim.CanClaim)
        {
            heuristic = claim.CaughtUpButCarryingBacklog
                ? $"Caught up on this range — but {CaughtUpClaim.CarriedBacklogClause(claim)}.{projectionNote}{busy}"
                : $"All caught up — nothing waiting on a reply.{projectionNote}{busy}";
        }
        else if (claim.NothingWaitingButIncomplete)
        {
            heuristic =
                $"Nothing waiting in what could be read, but {CaughtUpClaim.IncompleteClause(claim)} — " +
                $"check those before calling it a day.{projectionNote}{busy}";
        }
        else
        {
            // Quote the same figure the tile above shows. Two different totals on one screen is how the
            // old number lost the owner's trust — and the backlog is named rather than dropped, so the
            // smaller headline never reads as the whole story.
            var shown = liveAwaiting ?? totalAwaiting;
            var customers = shown == 1 ? "1 customer is" : $"{shown} customers are";
            var accountWord = accountsBehind == 1 ? "account" : "accounts";
            var older = totalAwaiting > shown
                ? $" {totalAwaiting - shown} more have been waiting over a week."
                : string.Empty;
            // "open", not "waiting". The sentence already opened with the live figure, and the per-account
            // number is that account's whole open population — saying "76 customers are waiting … start
            // with the one that has 145 waiting" puts two different scales in one breath and reads as a
            // contradiction.
            var start = worst is not null
                ? $" Start with {worst.DisplayName} ({worst.AwaitingCount} open, {worst.OnTimePercent}% caught up)."
                : string.Empty;
            heuristic = $"{customers} waiting across {accountsBehind} {accountWord}.{older}{start}{projectionNote}";
        }

        var displayText = heuristic;
        var isAi = false;
        if (AppSettingsService.Instance.Settings.EnableLocalAi)
        {
            // The unmeasured counts belong in the cache key. An account going from readable to unreadable
            // changes none of the other terms — awaiting stays 0, the percentage stays the same — so
            // without them the cached briefing, written before anything broke, would be served unchanged
            // and the new warning would never reach the owner.
            var signature =
                $"{overallPct}|{totalAwaiting}|{accountsBehind}|{worst?.Key}|{worst?.AwaitingCount}|" +
                $"{busyHour}|{eod.Projected}|{busierThanUsual}|{claim.UnreadableCount}|{claim.NotLoadedCount}";
            var cached = OversightInsightService.Instance.TryGet(BriefingCacheKey, signature);
            if (cached is not null)
            {
                displayText = cached;
                isAi = true;
            }
            else
            {
                // The model is told about the unmeasured accounts too. Feeding it only the counts would
                // have it write the same falsely-reassuring briefing the heuristic used to, in better
                // prose — and the AI line replaces the heuristic rather than sitting beside it.
                var incomplete = CaughtUpClaim.IncompleteClause(claim);
                var prompt =
                    $"Across {entities.Count} accounts: {totalAwaiting} customer(s) waiting, {accountsBehind} account(s) " +
                    $"behind, {overallPct}% caught up overall." +
                    (string.IsNullOrEmpty(incomplete)
                        ? string.Empty
                        : $" Important: {incomplete}, so these figures do not cover the whole business.") +
                    (worst is not null ? $" Furthest behind: {worst.DisplayName} ({worst.AwaitingCount} waiting, {worst.OnTimePercent}% caught up)." : string.Empty) +
                    (eod.HasData ? $" {eod.SoFar} messages so far today, projected ~{eod.Projected} by end of day." : string.Empty) +
                    (busierThanUsual ? " That is busier than the usual daily average." : string.Empty) +
                    (busyHour is "—" or "" ? string.Empty : $" Busiest hour: {busyHour}.") +
                    " Write the one-line start-of-shift briefing telling the owner where to focus and flag anything unusual.";
                OversightInsightService.Instance.Request(BriefingCacheKey, signature, prompt, BriefingSystemPrompt, OnInsightReady);
            }
        }

        BriefingBadge.Text = isAi ? "✦ AI" : "✦";
        BriefingText.Text = displayText;
        BriefingStrip.Visibility = Visibility.Visible;
    }

    private const string BriefingCacheKey = "__shift_briefing__";

    /// <summary>
    /// A one-line attention summary for an account/location, styled like an info strip. Returns null when
    /// there's nothing to flag (still syncing, no activity, or fully caught up) so quiet accounts stay quiet.
    /// </summary>
    private FrameworkElement? BuildInsightStrip(OversightEntityHealth entity)
    {
        var hasLiveData = entity.MeasuredCount > 0;
        if (!entity.HasChatData || !hasLiveData || entity.AwaitingCount == 0)
        {
            return null;
        }

        // Light scan of the awaiting list (in-memory) to make the insight specific: how many are unread
        // vs read-but-unanswered, and how long the longest-waiting customer has been waiting.
        var (windowStart, windowEnd) = WindowRange();
        var awaiting = entity.MemberInstanceIds
            .SelectMany(id => OversightChatSnapshotService.Instance.GetAwaiting(id, windowStart, windowEnd))
            .ToList();
        var unreadCount = awaiting.Count(c => c.Unread > 0);
        DateTimeOffset? oldest = awaiting.Count > 0 ? awaiting.Min(c => c.LastActivityUtc) : null;

        var oldestText = oldest is { } o ? RelativeAge(o) : "unknown";

        // "open", matching the card and the digest. This was the third site of the same scope mismatch and
        // it survived the first fix: the hero counts this week, an account counts its whole history, and
        // both were saying "customers are waiting on a reply". Caught by reading the installed build.
        var conversationWord = entity.AwaitingCount == 1 ? "open conversation" : "open conversations";
        var sb = new StringBuilder();
        sb.Append("Needs attention — ").Append(entity.AwaitingCount).Append(' ').Append(conversationWord);
        if (unreadCount > 0)
        {
            sb.Append(" · ").Append(unreadCount).Append(" unread");
        }
        if (oldest is { } ot)
        {
            sb.Append(" · oldest ").Append(RelativeAge(ot));
        }
        sb.Append('.');
        var heuristicText = sb.ToString();

        // Optional local-AI enhancement: when EnableLocalAi is on and the Ollama runtime is reachable, swap the
        // heuristic line for a model-phrased one. It's cached per account by a state signature; until it lands
        // (or if AI is off/unreachable) we show the heuristic, so this never blocks or regresses the strip.
        var displayText = heuristicText;
        var isAi = false;
        if (AppSettingsService.Instance.Settings.EnableLocalAi)
        {
            var signature = $"{entity.AwaitingCount}|{unreadCount}|{entity.OnTimePercent}|{oldest?.UtcTicks ?? 0}";
            var cached = OversightInsightService.Instance.TryGet(entity.Key, signature);
            if (cached is not null)
            {
                displayText = cached;
                isAi = true;
            }
            else
            {
                var facts = new OversightInsightFacts(
                    entity.DisplayName, entity.AwaitingCount, unreadCount, entity.OnTimePercent, oldestText);
                OversightInsightService.Instance.Request(entity.Key, signature, facts, OnInsightReady);
            }
        }

        // Dark neutral surface — severity is already communicated via the % color in the card header.
        // A consistent dark strip looks more premium than alternating amber/red backgrounds.
        var bg = Brush("ControlSolidFillColorDefaultBrush");
        var fg = Brush("TextFillColorPrimaryBrush");
        var badge = Brush("SystemFillColorCautionBrush");

        // A GRID, not a horizontal StackPanel. A horizontal StackPanel measures its children with INFINITE
        // available width, so a TextBlock inside one can never wrap however its TextWrapping is set — it
        // overflows and is clipped by the parent Border. The insight line was being cut mid-word with no
        // ellipsis ("…at end of shif"), on the dashboard's most prominent sentence. Auto + star gives the
        // text a real width to wrap into.
        var content = new Grid { ColumnSpacing = 6, HorizontalAlignment = HorizontalAlignment.Stretch };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        content.Children.Add(new TextBlock
        {
            Text = isAi ? "✦ AI" : "✦",
            FontSize = UmScale.Text.Caption,
            FontWeight = FontWeights.SemiBold,
            Foreground = badge,
            Opacity = 0.9,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 0, 0)
        });
        var insightText = new TextBlock
        {
            Text = displayText,
            Foreground = fg,
            FontSize = UmScale.Text.Body,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        Grid.SetColumn(insightText, 1);
        content.Children.Add(insightText);

        return new Border
        {
            Background = bg,
            BorderBrush = Brush("UmHairlineBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 7, 10, 7),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = content
        };
    }

    // The local-AI insight landed for some account; force a one-shot re-render so it swaps in for the heuristic.
    private void OnInsightReady()
    {
        DispatcherQueue?.TryEnqueue(() =>
        {
            _lastRenderSignature = string.Empty;
            Render();
        });
    }

    private StackPanel BuildRowContent(OversightEntityHealth entity)
    {
        var secondary = Brush("TextFillColorSecondaryBrush");
        var danger = Brush("SystemFillColorCriticalBrush");
        var hasLiveData = entity.MeasuredCount > 0;
        var statusBrush = !hasLiveData ? secondary : StatusBrushForPercent(entity.OnTimePercent);

        // Live awaiting detail from the same snapshot the awaiting count comes from, so the "past target"
        // chip and oldest-wait hint always agree with the pill (unlike the old registry-based "late" count,
        // which could show dozens late on a 100% caught-up account).
        var (cardWindowStart, cardWindowEnd) = WindowRange();
        var slaMinutes = AppSettingsService.Instance.Settings.SlaThresholdMinutes;
        var nowUtc = DateTimeOffset.UtcNow;
        var awaitingChats = entity.MemberInstanceIds
            .SelectMany(id => OversightChatSnapshotService.Instance.GetAwaiting(id, cardWindowStart, cardWindowEnd))
            .ToList();
        var pastSlaCount = awaitingChats.Count(c => (nowUtc - c.LastActivityUtc).TotalMinutes > slaMinutes);
        TimeSpan? oldestWait = awaitingChats.Count > 0
            ? nowUtc - awaitingChats.Min(c => c.LastActivityUtc)
            : null;

        var card = new StackPanel
        {
            Spacing = _compact ? 4 : 8,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // ── Top row: avatar circle + name/freshness + awaiting pill ──────────────────────────
        // (The status accent is a full-height stripe on the card wrapper, see BuildRow.)
        var topRow = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Avatar: imported photo / built-in icon / initials — per-account via ProfileAvatarService so a
        // chosen icon shows on the dashboard card and the sidebar alike. Locations fall back to initials.
        var avatar = BuildEntityAvatar(entity, 30);
        avatar.Margin = new Thickness(0, 0, 10, 0);
        avatar.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(avatar, 0);

        // Account name (with location count when grouped) + freshness subline
        var nameText = entity.Kind == OversightEntityKind.Location
            ? $"{entity.DisplayName}  ({entity.AccountCount})"
            : entity.DisplayName;
        var nameColumn = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
        Grid.SetColumn(nameColumn, 1);

        // Name line, plus a session-state chip when (and only when) the session isn't healthy. A chip on
        // every card would be decoration; a chip that appears only for Starting / Scan QR / Stale / Failed
        // is signal, and matches the panel's worst-first philosophy.
        var nameLine = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center
        };
        nameLine.Children.Add(new TextBlock
        {
            Text = nameText,
            FontWeight = FontWeights.SemiBold,
            FontSize = UmScale.Text.BodyStrong,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        if (BuildSessionStateChip(entity) is { } sessionChip)
        {
            nameLine.Children.Add(sessionChip);
        }

        nameColumn.Children.Add(nameLine);
        if (!_compact)
        {
            // Per-card data freshness: when this account's chats were last read. Locations show their
            // least-fresh member so a silently-stale branch account can't hide behind a fresh sibling.
            var capturedAt = entity.MemberInstanceIds
                .Select(OversightChatSnapshotService.Instance.TryGetCapturedAtUtc)
                .Where(t => t is not null)
                .DefaultIfEmpty(null)
                .Min();
            // Keep this line SHORT. It is character-ellipsis trimmed inside a card, so a long string gets
            // cut mid-sentence — which is how the stale state came to read "stale — right-click the accou…".
            // The recovery steps belong in the tooltip, in the owner's vocabulary: "WebView" is an
            // implementation detail and means nothing to the person paying for this.
            // F-OFFLINE-08: "click Re-sync" is the wrong thing to say to an owner whose machine is
            // offline. Re-sync reloads the account's page, which cannot succeed without a connection, so
            // the one instruction given was the one that could not work — and it read as though the
            // staleness were something they had neglected. The connection join was already being made in
            // the log by ScanBlockedMessage; it just never reached the screen.
            var offline = OfflineState.AnyOffline(entity.MemberInstanceIds);
            var freshness = entity.IsStale
                ? offline ? "out of date — no connection" : "out of date — click Re-sync"
                : capturedAt is { } cap
                    ? $"updated {RelativeAge(cap)}{(entity.HistoricalOpenCount > 0 ? $" · {entity.HistoricalOpenCount} chats tracked" : string.Empty)}"
                    : offline ? "no connection — waiting" : "waiting for first sync…";
            var freshnessBlock = new TextBlock
            {
                Text = freshness,
                FontSize = UmScale.Text.Caption,
                Foreground = entity.IsStale ? danger : Brush("TextFillColorTertiaryBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            ToolTipService.SetToolTip(freshnessBlock, offline
                ? "This PC cannot reach the internet, so this account's page has not been able to load and "
                  + "its numbers have stopped updating. Nothing is wrong with the account. It will catch up "
                  + "on its own once the connection is back."
                : entity.IsStale
                    ? "This account has stopped reporting, so the numbers on this card are out of date. "
                      + "Click Re-sync at the top of the command centre. If that doesn't help, right-click "
                      + "the account in the sidebar and choose Refresh, then Re-sync again."
                    : "When this account's chat data was last read. Numbers on this card are only as fresh "
                      + "as this stamp — click Re-sync to update.");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(freshnessBlock, entity.IsStale && !offline
                ? $"{entity.DisplayName}: data out of date, click Re-sync"
                : $"{entity.DisplayName}: {freshness}");
            nameColumn.Children.Add(freshnessBlock);

            // F-SNAP-02: which reader this account is on. A bridge failure falls soft to the IndexedDB
            // scan, so metrics keep flowing and the degradation is invisible — but that reader cannot read
            // WhatsApp's callOutcome, so answered calls stay counted as missed. Settings has said so since
            // v4.99.47; the card the owner actually looks at did not.
            if (entity.MemberInstanceIds.Any(id => StoreBridgeHealth.TryGet(id) is { Succeeded: false }))
            {
                var reducedBlock = new TextBlock
                {
                    Text = "reduced detail — fallback reader",
                    FontSize = UmScale.Text.Caption,
                    Foreground = Brush("TextFillColorTertiaryBrush"),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                ToolTipService.SetToolTip(
                    reducedBlock,
                    "This account is being read by the backup reader. Waiting counts and reply times are "
                    + "still right, but message previews are sparser and an answered call cannot be told "
                    + "apart from a missed one — so any missed-call figure for it may be too high. "
                    + "See Settings → Data.");
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
                    reducedBlock,
                    $"{entity.DisplayName}: on the fallback reader, missed-call count may be over-stated");
                nameColumn.Children.Add(reducedBlock);
            }
        }

            // Awaiting pill (right-aligned): a soft danger chip when behind, quiet text when caught up.
        FrameworkElement awaitingVisual;
        if (!entity.HasChatData || !hasLiveData)
        {
            awaitingVisual = new TextBlock
            {
                Text = "—", Foreground = secondary, FontSize = UmScale.Text.Body, VerticalAlignment = VerticalAlignment.Center
            };
        }
        else if (entity.AwaitingCount > 0)
        {
            // Clickable: opens the flat Needs-reply list scoped to just this account/location.
            var pill = new Button
            {
                Background = Brush("SystemFillColorCriticalBackgroundBrush"),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(9, 3, 9, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Content = new TextBlock
                {
                    Text = entity.AwaitingCount == 1 ? "1 open" : $"{entity.AwaitingCount} open",
                    Foreground = danger,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = UmScale.Text.Body
                }
            };
            // "3 awaiting" alone does not say WHICH account, and every card renders one of these — so a
            // screen-reader user heard the same phrase repeatedly with no way to tell them apart.
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
                pill,
                $"{entity.DisplayName}: {entity.AwaitingCount} " +
                (entity.AwaitingCount == 1 ? "open conversation" : "open conversations") +
                ". Activate to work through this account's replies.");

            var filterIds = entity.MemberInstanceIds.ToList();
            var filterLabel = entity.DisplayName;
            pill.Click += (_, _) => ShowNeedsReplyFor(filterIds, filterLabel);
            ToolTipService.SetToolTip(pill, oldestWait is { } ow
                ? $"{entity.AwaitingCount} waiting — longest {FormatMinutes(ow.TotalMinutes)}. Click to work through just this account's replies."
                : "Click to work through just this account's waiting customers.");
            awaitingVisual = pill;
        }
        else
        {
            awaitingVisual = new TextBlock
            {
                Text = "caught up", Foreground = secondary, FontSize = UmScale.Text.Body, VerticalAlignment = VerticalAlignment.Center
            };
            ToolTipService.SetToolTip(awaitingVisual, "No customers are waiting on a reply in this date range.");
        }

        // In compact density the % hero (which carries the status glyph) is hidden, so the status would be
        // colour-only — add the shape-distinct glyph here so compact stays WCAG 1.4.1 clean.
        FrameworkElement trailingCell = awaitingVisual;
        if (_compact && hasLiveData)
        {
            var (compactGlyph, compactLabel) = StatusGlyph(entity.OnTimePercent);
            var glyphIcon = new FontIcon
            {
                Glyph = compactGlyph,
                FontSize = UmScale.Icon.Md,
                Foreground = statusBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            ToolTipService.SetToolTip(glyphIcon, compactLabel);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(glyphIcon, compactLabel);

            var trailing = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };
            trailing.Children.Add(glyphIcon);
            trailing.Children.Add(awaitingVisual);
            trailingCell = trailing;
        }

        // A "details" button (per-account only) opens the account's L1 insight view before the raw WebView.
        if (entity.Kind == OversightEntityKind.Instance && _services is not null)
        {
            var detailsButton = new Button
            {
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6),
                VerticalAlignment = VerticalAlignment.Center,
                Content = new FontIcon { Glyph = "\uE9D2", FontSize = UmScale.Icon.Md } // BarChart
            };
            ToolTipService.SetToolTip(detailsButton, "Account details — reply speed, backlog, and who's waiting");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(detailsButton, $"{entity.DisplayName} details");
            var instanceKey = entity.Key;
            detailsButton.Click += (_, _) => ShowAccountDetail(instanceKey);

            var wrapped = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            wrapped.Children.Add(detailsButton);
            wrapped.Children.Add(trailingCell);
            trailingCell = wrapped;
        }

        Grid.SetColumn(trailingCell, 2);
        topRow.Children.Add(avatar);
        topRow.Children.Add(nameColumn);
        topRow.Children.Add(trailingCell);
        card.Children.Add(topRow);

        if (_compact)
        {
            return card;
        }

        // ── Metric row: large % hero + sparkline ──────────────────────────────────────────────
        var metricRow = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        metricRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metricRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        if (!entity.HasChatData || !hasLiveData)
        {
            // Three distinct states, not two. "Can't read" used to render as "no activity", which reads as
            // reassuring — the owner concludes the branch is quiet when in fact oversight of it has stopped
            // and customers may be waiting unseen. It is shown in the danger colour because it is the only
            // one of the three that needs action.
            var couldNotRead = entity.ReadFailed;
            var readBlockedByNetwork = couldNotRead && OfflineState.AnyOffline(entity.MemberInstanceIds);
            var stateBlock = new TextBlock
            {
                Text = couldNotRead
                    ? readBlockedByNetwork
                        ? "can't read this account — no connection"
                        : "can't read this account — click Re-sync"
                    : !entity.HasChatData
                        ? "syncing…"
                        : $"no activity {_emptyStateWindowLabel}",
                Foreground = couldNotRead ? danger : secondary,
                FontSize = UmScale.Text.BodyStrong,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.WrapWholeWords
            };

            ToolTipService.SetToolTip(stateBlock, readBlockedByNetwork
                ? "This PC cannot reach the internet, so this account's page has not loaded and there was "
                  + "nothing to read. Its numbers are missing rather than zero, and it is left out of your "
                  + "caught-up percentage instead of counting as perfect. It will pick up on its own once "
                  + "the connection is back."
                : couldNotRead
                    ? "The last attempt to read this account returned no usable data, so its numbers are "
                      + "missing rather than zero. This account is left out of your caught-up percentage "
                      + "instead of counting as perfect. Click Re-sync; if it persists, right-click the "
                      + "account in the sidebar and choose Refresh."
                    : "No customer activity was recorded for this account in the selected period.");

            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(stateBlock, couldNotRead
                ? readBlockedByNetwork
                    ? $"{entity.DisplayName}: cannot read this account, no internet connection"
                    : $"{entity.DisplayName}: cannot read this account, click Re-sync"
                : $"{entity.DisplayName}: no activity {_emptyStateWindowLabel}");

            Grid.SetColumn(stateBlock, 0);
            metricRow.Children.Add(stateBlock);
        }
        else
        {
            var pctCell = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };

            // One verdict chip, in words, instead of a glyph + a 24px coloured percentage + the word
            // "caught up". The old arrangement put an error badge and a big red number on every account
            // that was behind — three of three, here — so the card shouted before it informed, and the
            // percentage competed with the hero figure a few pixels above it.
            //
            // WCAG 1.4.1 is satisfied more strongly than before, not less: the status is now literally
            // spelled ("Behind", "Needs attention", "On track") rather than encoded in a glyph shape.
            var (_, statusLabel) = StatusGlyph(entity.OnTimePercent);
            var verdictChip = new Border
            {
                Background = StatusWashBrush(entity.OnTimePercent),
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(10, 3, 10, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = $"{statusLabel} · {entity.OnTimePercent}% caught up",
                    FontSize = UmScale.Text.Body,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = statusBrush
                }
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
                verdictChip, $"{entity.DisplayName}: {statusLabel}, {entity.OnTimePercent}% caught up.");
            pctCell.Children.Add(verdictChip);

            ToolTipService.SetToolTip(pctCell,
                $"{entity.OnTimePercent}% of this account's {entity.MeasuredCount} active chats have no customer message waiting. " +
                "This measures unread cleared — reply speed is the \"reply ~\" chip below.");
            Grid.SetColumn(pctCell, 0);
            metricRow.Children.Add(pctCell);
        }

        var sparklineHost = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Spacing = 3 };
        var sparkline = BuildSparkline(entity.TrendCounts, statusBrush);
        ToolTipService.SetToolTip(sparkline, "Chat activity per day over the last 7 days (today rightmost) — taller bar = busier day");
        sparklineHost.Children.Add(sparkline);
        sparklineHost.Children.Add(new TextBlock
        {
            Text = "last 7 days",
            FontSize = UmScale.Text.Caption,
            Foreground = Brush("TextFillColorTertiaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Right
        });
        Grid.SetColumn(sparklineHost, 1);
        metricRow.Children.Add(sparklineHost);
        card.Children.Add(metricRow);

        // ── Detail chips: reply speed · answered today · past-target · urgent · dropped ───────
        // All live-data derived. The old "N late" figure came from the triage registry and could
        // contradict the pill (e.g. "45 late" on a 100% caught-up account) — replaced by "past target",
        // counted from the same awaiting snapshot as the pill, so the numbers always agree.
        if (hasLiveData)
        {
            var caution = Brush("SystemFillColorCautionBrush");
            var success = Brush("SystemFillColorSuccessBrush");
            var memberInstances = _services?.Registry.Instances
                .Where(i => entity.MemberInstanceIds.Contains(i.Id, StringComparer.OrdinalIgnoreCase))
                .ToList() ?? [];
            var resp = ResponseTimeTracker.Instance.GetStats(memberInstances, cardWindowStart, cardWindowEnd, slaMinutes);

            var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 22 };

            // Show reply speed only once there's live data; a perpetual "measuring…" on every card just
            // reads as stuck (the KPI band's "Response time — · builds as you reply" sets that once).
            if (resp.HasData)
            {
                var replies = resp.SampleCount == 1 ? "1 reply measured" : $"{resp.SampleCount} replies measured";
                chips.Children.Add(BuildStat(
                    "Reply time",
                    FormatMinutes(resp.MedianMinutes),
                    ResponseBrush(resp.MedianMinutes, slaMinutes),
                    $"Median time from a customer's message to this account's first reply ({replies}). Target: under {slaMinutes} min."));
            }

            if (resp.AnsweredToday > 0)
            {
                chips.Children.Add(BuildStat(
                    "Answered today",
                    resp.AnsweredToday.ToString(),
                    success,
                    "Waiting customers this account replied to today — work done, not just work pending."));
            }

            if (pastSlaCount > 0)
            {
                chips.Children.Add(BuildStat(
                    $"Past {slaMinutes}m target",
                    pastSlaCount.ToString(),
                    caution,
                    $"Of the {entity.AwaitingCount} awaiting, {pastSlaCount} have already waited longer than your {slaMinutes}-minute reply target — reply to these first."));
            }

            if (entity.UrgentCount > 0)
            {
                chips.Children.Add(BuildStat(
                    "Urgent",
                    entity.UrgentCount.ToString(),
                    danger,
                    "Messages whose wording looks urgent (triage keywords / local AI)."));
            }

            if (entity.DroppedCount > 0)
            {
                chips.Children.Add(BuildStat(
                    "Dropped",
                    entity.DroppedCount.ToString(),
                    danger,
                    "Conversations that look abandoned — the customer never got a reply and the chat went quiet."));
            }

            card.Children.Add(chips);

            // Plain-language nudge — the single most useful next action for this account.
            if (oldestWait is { } worst && entity.AwaitingCount > 0)
            {
                card.Children.Add(new TextBlock
                {
                    Text = $"Longest wait: {FormatMinutes(worst.TotalMinutes)} — expand to see who's waiting.",
                    FontSize = UmScale.Text.Caption,
                    Foreground = Brush("TextFillColorTertiaryBrush"),
                    TextWrapping = TextWrapping.WrapWholeWords
                });
            }
        }

        return card;
    }

    /// <summary>
    /// One cell of the card's stat strip: a quiet uppercase key over the figure.
    /// </summary>
    /// <remarks>
    /// This was a row of icon+text pills, each in its own semantic colour — a red "reply ~15.7h", a green
    /// "4 answered today", an amber "134 past 15m", all outlined, all shouting at once. Three colours and
    /// three glyphs to carry three numbers meant the row had no reading order and no relative importance.
    ///
    /// Now the key names the measure and the value carries it, which is how an instrument panel reads.
    /// Semantic ink is kept only where the figure is genuinely a problem (past target, urgent, dropped);
    /// the rest sit in primary ink. The glyphs are gone: they were decoration, and the tooltip already
    /// carried the plain-language explanation.
    /// </remarks>
    private FrameworkElement BuildStat(string label, string value, Brush valueBrush, string tooltip)
    {
        var cell = new StackPanel { Spacing = 1 };
        cell.Children.Add(new TextBlock
        {
            Text = label.ToUpperInvariant(),
            FontSize = UmScale.Text.Caption,
            FontWeight = FontWeights.SemiBold,
            CharacterSpacing = 60,
            Foreground = Brush("TextFillColorTertiaryBrush")
        });
        cell.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = UmScale.Text.BodyStrong,
            FontWeight = FontWeights.SemiBold,
            Foreground = valueBrush
        });

        ToolTipService.SetToolTip(cell, tooltip);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(cell, $"{label}: {value}");
        return cell;
    }

    /// <summary>
    /// A shimmering placeholder card shown while the first per-account history scan runs — communicates
    /// "loading" with shape instead of a bare text line. Pure opacity pulse; no dependencies.
    /// </summary>
    private FrameworkElement BuildSkeletonCard()
    {
        Border Bar(double width, double height) => new()
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(6),
            Background = Brush("ControlFillColorSecondaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var lines = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        lines.Children.Add(Bar(170, 12));
        lines.Children.Add(Bar(110, 9));

        var top = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        top.Children.Add(new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(12),
            Background = Brush("ControlFillColorSecondaryBrush")
        });
        top.Children.Add(lines);

        var inner = new StackPanel { Spacing = 12 };
        inner.Children.Add(top);
        inner.Children.Add(Bar(230, 18));

        var cardBorder = new Border
        {
            Background = Brush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = Brush("UmHairlineBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 12, 16, 14),
            Child = inner
        };

        var pulse = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            From = 1.0,
            To = 0.45,
            Duration = new Duration(TimeSpan.FromMilliseconds(900)),
            AutoReverse = true,
            RepeatBehavior = Microsoft.UI.Xaml.Media.Animation.RepeatBehavior.Forever
        };
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(pulse, cardBorder);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(pulse, "Opacity");
        var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        storyboard.Children.Add(pulse);
        cardBorder.Loaded += (_, _) => storyboard.Begin();
        cardBorder.Unloaded += (_, _) => storyboard.Stop();
        return cardBorder;
    }

    /// <summary>
    /// Builds the card avatar: for an account, the per-instance avatar (imported photo, built-in icon, or
    /// initials) from <see cref="ProfileAvatarService"/>; for a location (or an unresolved account), colored
    /// initials of the entity name.
    /// </summary>
    private FrameworkElement BuildEntityAvatar(OversightEntityHealth entity, double size)
    {
        if (entity.Kind == OversightEntityKind.Instance && _services is not null)
        {
            var instance = _services.Registry.Instances.FirstOrDefault(i =>
                string.Equals(i.Id, entity.Key, StringComparison.OrdinalIgnoreCase));
            if (instance is not null)
            {
                return ProfileAvatarService.CreateAvatar(instance, size);
            }
        }

        var brush = new SolidColorBrush(PlatformBrandingHelper.ParseAccentColor(ResolveEntityAccentColor(entity)));
        var host = new Grid { Width = size, Height = size };
        host.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse { Width = size, Height = size, Fill = brush });
        host.Children.Add(new TextBlock
        {
            Text = PlatformBrandingHelper.GetInitials(entity.DisplayName),
            FontSize = size * 0.36,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });
        return host;
    }

    /// <summary>
    /// Resolves a hex accent color for an entity's avatar. For ByInstance entities the instance
    /// AccentColor is used directly; for ByLocation entities the first member instance's color is used.
    /// Falls back to the platform-branding default (#6B7280) when no match is found.
    /// </summary>
    private string ResolveEntityAccentColor(OversightEntityHealth entity)
    {
        if (_services is null)
        {
            return PlatformBrandingHelper.DefaultAccentHex;
        }

        var instanceId = entity.Kind == OversightEntityKind.Instance
            ? entity.Key
            : entity.MemberInstanceIds.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return PlatformBrandingHelper.DefaultAccentHex;
        }

        var instance = _services.Registry.Instances
            .FirstOrDefault(i => string.Equals(i.Id, instanceId, StringComparison.OrdinalIgnoreCase));
        return instance?.AccentColor ?? PlatformBrandingHelper.DefaultAccentHex;
    }

    /// <summary>
    /// A shape-distinct status glyph (Segoe Fluent Icons) + accessible label for an on-time %, so health
    /// is communicated by shape, not colour alone (WCAG 1.4.1). Thresholds mirror the status-colour bands.
    /// </summary>
    /// <summary>
    /// The pale fill behind a status chip. Pairs with the semantic ink of the same band, which already
    /// clears 4.5:1 on these washes (they sit within a few percent of the card surface).
    /// </summary>
    private Brush StatusWashBrush(int onTimePercent) => onTimePercent switch
    {
        >= 90 => Brush("UmStatusSuccessWashBrush"),
        >= 70 => Brush("UmStatusWarningWashBrush"),
        _ => Brush("UmStatusDangerWashBrush"),
    };

    private static (string Glyph, string Label) StatusGlyph(int onTimePercent) => onTimePercent switch
    {
        >= 90 => ("", "On track"),        // CheckMark
        >= 70 => ("", "Needs attention"), // Warning
        _ => ("", "Behind"),              // ErrorBadge
    };

    /// <summary>
    /// A compact 7-day bar-chart sparkline. Seven vertical bars, color-matched to the account's
    /// status brush, with rounded tops. Falls back to flat stubs when there is no recent activity.
    /// </summary>
    private FrameworkElement BuildSparkline(IReadOnlyList<int> counts, Brush fill)
    {
        const double barWidth = 6;
        const double barGap = 3;
        const double maxH = 20;

        var host = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = barGap,
            VerticalAlignment = VerticalAlignment.Center,
            Height = maxH
        };

        var hasCounts = counts is { Count: >= 1 } && counts.Any(c => c > 0);
        var max = hasCounts ? Math.Max(1, counts.Max()) : 1;

        for (var i = 0; i < 7; i++)
        {
            var value = (counts is not null && i < counts.Count) ? counts[i] : 0;
            var barH = hasCounts ? Math.Max(2, value / (double)max * (maxH - 2)) : 2;
            host.Children.Add(new Rectangle
            {
                Width = barWidth,
                Height = barH,
                Fill = hasCounts ? fill : Brush("TextFillColorDisabledBrush"),
                Opacity = hasCounts ? 0.85 : 0.35,
                VerticalAlignment = VerticalAlignment.Bottom,
                RadiusX = 1.5,
                RadiusY = 1.5
            });
        }

        return host;
    }

    // Neutral text brushes are resolved from THIS control's actual theme (not the app default) so they can't
    // render invisibly on a light surface — see ThemeBrushResolver. Instance method so it can read ActualTheme.
    private Brush Brush(string key) => Services.ThemeBrushResolver.Resolve(this, key);

    private void OnAttentionJump(object sender, RoutedEventArgs e)
    {
        if (_worstEntityFirstInstanceId is not null)
        {
            _services?.Navigation.OpenInstance(_worstEntityFirstInstanceId, null, null);
        }
    }

    private void OnGroupByAccountClick(object sender, RoutedEventArgs e) => SelectMode(GroupByAccountButton);

    private void OnGroupByLocationClick(object sender, RoutedEventArgs e) => SelectMode(GroupByLocationButton);

    private void OnNeedsReplyClick(object sender, RoutedEventArgs e)
    {
        _needsReplyFilterIds = null; // the toolbar button shows the full backlog
        SelectMode(NeedsReplyButton);
    }

    /// <summary>Opens the per-account L1 detail dialog (reply speed, backlog, waiting customers).</summary>
    private async void ShowAccountDetail(string instanceId)
    {
        var instance = _services?.Registry.Instances.FirstOrDefault(i =>
            string.Equals(i.Id, instanceId, StringComparison.OrdinalIgnoreCase));
        if (_services is null || instance is null)
        {
            return;
        }

        var dialog = new UnifiedMessenger.Dialogs.AccountDetailDialog(_services, instance) { XamlRoot = XamlRoot };
        await dialog.ShowManagedAsync();
    }

    /// <summary>Switches to the Needs-reply list scoped to one account/location (from a card's awaiting pill).</summary>
    private void ShowNeedsReplyFor(List<string> instanceIds, string label)
    {
        _needsReplyFilterIds = instanceIds is { Count: > 0 } ? instanceIds : null;
        _needsReplyFilterLabel = label;
        SelectMode(NeedsReplyButton);
    }

    // Segmented control: exactly one of {By account, By location, Needs reply} is active.
    /// <summary>
    /// The view axis: per-account overview, or the flat cross-account reply queue. Grouping is a separate
    /// axis and keeps its own state, so switching to the queue and back does not silently reset it.
    /// </summary>
    private void OnOverviewViewClick(object sender, RoutedEventArgs e) =>
        SelectMode(_groupByLocation ? GroupByLocationButton : GroupByAccountButton);

    private bool _groupByLocation;

    private void SelectMode(ToggleButton active)
    {
        var needsReply = ReferenceEquals(active, NeedsReplyButton);

        // Remember the grouping choice across a trip through the queue. Previously, entering Needs reply
        // unchecked both grouping toggles and coming back left neither selected until something re-rendered.
        if (!needsReply)
        {
            _groupByLocation = ReferenceEquals(active, GroupByLocationButton);
        }

        GroupByAccountButton.IsChecked = !needsReply && !_groupByLocation;
        GroupByLocationButton.IsChecked = !needsReply && _groupByLocation;
        NeedsReplyButton.IsChecked = needsReply;
        OverviewViewButton.IsChecked = !needsReply;

        // Grouping does not apply to a flat cross-account queue. Hiding the control is honest; leaving it
        // visible but inert invites a click that does nothing.
        GroupingControl.Visibility = needsReply ? Visibility.Collapsed : Visibility.Visible;

        // Leaving Needs-reply mode clears any per-account scope.
        if (!needsReply)
        {
            _needsReplyFilterIds = null;
        }

        _lastRenderSignature = string.Empty;
        Render();
    }

    private void OnDefineLocations(object sender, RoutedEventArgs e) =>
        _services?.Navigation.RequestOpenSettings(Services.SettingsNavigationHelper.WorkspaceManagementSectionKey);

    private bool _resyncInProgress;

    /// <summary>
    /// Raised when the unified Re-sync button is clicked. The dashboard orchestrates the full refresh
    /// (oversight history + the activity graph + Google reviews) so there is a single dashboard-wide button.
    /// </summary>
    public event EventHandler? DashboardResyncRequested;

    /// <summary>True while a re-sync is running, so the dashboard can disable re-entry across panels.</summary>
    public bool IsResyncInProgress => _resyncInProgress;

    private void OnResyncClick(object sender, RoutedEventArgs e) =>
        DashboardResyncRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Builds and shows the plain-language weekly report (deterministic, optionally AI-narrated).</summary>
    private async void OnReportClick(object sender, RoutedEventArgs e)
    {
        if (_services is null)
        {
            return;
        }

        var instances = _services.Registry.Instances
            .Where(i => i.IsProfessional && PlatformModuleSettingsHelper.IsPlatformModuleEnabled(i.Platform))
            .ToList();

        var inputs = DashboardReportHelper.GatherInputs(instances);
        var report = BusinessReport.Build(inputs);

        // Optional: let local AI phrase the headline in one encouraging sentence (aggregate facts only,
        // short timeout, degrades silently to the deterministic summary). Off unless EnableLocalAi.
        string? aiHeadline = null;
        var settings = AppSettingsService.Instance.Settings;
        if (settings.EnableLocalAi && !string.IsNullOrWhiteSpace(settings.LocalAiModelName))
        {
            try
            {
                var facts = report.Summary + " Insights: " +
                    string.Join("; ", report.Insights.Take(5).Select(i => $"{i.Title} — {i.Detail}"));
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
                aiHeadline = await Services.Ai.OllamaInferenceClient.Instance.GenerateTextAsync(
                    "Summarise this week's customer-messaging performance for the owner in ONE encouraging, " +
                    "specific sentence (max 28 words). Use only these aggregate facts; never invent names, " +
                    "customers, or numbers. Facts: " + facts,
                    "You are a concise operations assistant. Reply with a single plain sentence, no markdown, no quotes.",
                    settings.LocalAiModelName,
                    cts.Token).ConfigureAwait(true);
                aiHeadline = aiHeadline?.Trim();
            }
            catch
            {
                // deterministic summary stands on its own
            }
        }

        var dialog = new UnifiedMessenger.Dialogs.WeeklyReportDialog(inputs, report, instances, aiHeadline)
        {
            XamlRoot = XamlRoot
        };

        // Opening the report satisfies this week's reminder — reset the weekly clock and clear any banner.
        _ = AppSettingsService.Instance.UpdateAsync(s => s.WeeklyReportLastShownUtc = DateTimeOffset.UtcNow);
        _lastRenderSignature = string.Empty;
        Render();

        await dialog.ShowManagedAsync();
    }

    /// <summary>
    /// Deterministically re-runs history backfill for every professional account (force), then reports
    /// what the IndexedDB read returned and how much was reconciled — so reconciliation no longer
    /// depends on auto-trigger timing, and the result is observable. Public so the dashboard's single
    /// Re-sync button can run it as part of a unified refresh.
    /// </summary>
    public async Task RunResyncAsync()
    {
        if (_services is null || _resyncInProgress)
        {
            return;
        }

        var pros = _services.Registry.Instances.Where(instance => instance.IsProfessional).ToList();
        if (pros.Count == 0)
        {
            // Silently returning made the dashboard's primary recovery button look like a frozen app: a
            // new owner clicks Re-sync, the button stays enabled, and nothing anywhere changes.
            AttentionBanner.Visibility = Visibility.Visible;
            AttentionText.Text = "Nothing to sync yet — add a business account first.";
            return;
        }

        _resyncInProgress = true;
        ResyncButton.IsEnabled = false;
        AttentionBanner.Visibility = Visibility.Visible;
        BeginResyncProgress();

        try
        {
            var n = pros.Count;

            // Reload + probe each account through a small concurrency window instead of strictly one at a
            // time. The slow part is WAITING on each webview's reload + IndexedDB scan, so overlapping a few
            // accounts cuts the wall-clock roughly by the concurrency factor. Everything stays on the UI
            // thread (WebView2 is UI-affine) — the awaits interleave, so the browser process runs the scans
            // concurrently. The bar advances as each account finishes, order-independent. Reload is still
            // needed so a freshly-updated scraper script is (re)injected (injected on document creation only).
            var concurrency = Math.Min(3, n);
            using var gate = new System.Threading.SemaphoreSlim(concurrency);
            var parts = new string[n];
            var completed = 0;
            ResyncEaseToward(Math.Min(0.85, concurrency / (double)n));

            async Task ProcessAccountAsync(int i)
            {
                var instance = pros[i];
                await gate.WaitAsync().ConfigureAwait(true);
                try
                {
                    SetResyncStep(instance.DisplayName, Math.Min(completed + 1, n), n, reloading: true);
                    await _services.SessionManager.ReloadSessionAsync(instance.Id).ConfigureAwait(true);

                    SetResyncStep(instance.DisplayName, Math.Min(completed + 1, n), n, reloading: false);
                    parts[i] = $"{instance.DisplayName}: {await ProbeInstanceDbAsync(instance).ConfigureAwait(true)}";

                    // Kick off the real backfill so reconciliation still happens when the read works.
                    BackfillSyncManager.Instance.Schedule(instance, force: true);
                }
                catch (Exception ex)
                {
                    // The exception text belongs in the log, not on the dashboard. This banner used to
                    // render things like "DHA-2: Object reference not set to an instance of an object.",
                    // which tells the owner nothing they can act on and reads as a broken app.
                    AppLogger.LogWarning("Resync", $"'{instance.Id}': {ex.GetType().Name}: {ex.Message}");
                    parts[i] = $"{instance.DisplayName}: couldn't read this account — open it once, then try again";
                }
                finally
                {
                    gate.Release();
                    var done = ++completed; // continuations run on the UI thread — no torn writes
                    ResyncAnchor((double)done / n);
                    ResyncEaseToward(Math.Min(1.0, (done + concurrency) / (double)n));
                }
            }

            await Task.WhenAll(Enumerable.Range(0, n).Select(ProcessAccountAsync)).ConfigureAwait(true);

            ResyncAnchor(1.0);
            Render();

            AttentionBanner.Visibility = Visibility.Visible;

            // "Probe ·" was the internal name of this operation leaking onto the owner's dashboard.
            AttentionText.Text = string.Join("   |   ", parts);
        }
        finally
        {
            EndResyncProgress();
            ResyncButton.IsEnabled = true;
            _resyncInProgress = false;
        }
    }

    // Re-sync progress bar: a UI-thread timer eases the displayed value toward a soft ceiling between real
    // completion anchors, so the bar visibly moves during the long per-account probe (rather than freezing on
    // an indeterminate spinner) yet only ever advances and snaps to truth as each account finishes.
    private Microsoft.UI.Xaml.DispatcherTimer? _resyncEaseTimer;
    private double _resyncDisplayed;
    private double _resyncCeiling;
    private System.Diagnostics.Stopwatch? _resyncStopwatch;

    // Witty status lines, rotated on a slow cadence so the wait has some personality while still naming the
    // account and its position. {0} = account name. Kept light — this isn't a serious operation.
    private static readonly string[] ResyncReadingQuips =
    {
        "Rifling through {0}'s chat history",
        "Counting who's still waiting at {0}",
        "Seeing who {0} left on read",
        "Tallying {0}'s unanswered questions",
        "Catching up on {0}'s conversations",
        "Asking {0}'s inbox to spill the tea",
        "Reading {0}'s history",
    };
    private static readonly string[] ResyncReloadQuips =
    {
        "Waking up {0}",
        "Nudging {0} back to life",
        "Reloading {0}",
    };

    private string _resyncAccountName = "";
    private int _resyncStepIndex;
    private int _resyncStepTotal;
    private bool _resyncReloading;
    private int _resyncQuipIndex;
    private int _resyncTickCount;

    // ~150ms tick × 24 ≈ every 3.6s the quip rotates.
    private const int ResyncQuipRotateTicks = 24;

    private void BeginResyncProgress()
    {
        _resyncDisplayed = 0;
        _resyncCeiling = 0;
        _resyncQuipIndex = 0;
        _resyncTickCount = 0;
        _resyncStopwatch = System.Diagnostics.Stopwatch.StartNew();
        ResyncProgressRow.Visibility = Visibility.Visible;
        ApplyResyncBar(0);
        AttentionIcon.Glyph = ""; // Sync glyph reads as working, not the caution triangle

        if (_resyncEaseTimer is null)
        {
            _resyncEaseTimer = new Microsoft.UI.Xaml.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _resyncEaseTimer.Tick += (_, _) =>
            {
                _resyncDisplayed += (_resyncCeiling - _resyncDisplayed) * 0.04;
                if (_resyncDisplayed > _resyncCeiling)
                {
                    _resyncDisplayed = _resyncCeiling;
                }
                ApplyResyncBar(_resyncDisplayed);

                _resyncTickCount++;
                if (_resyncTickCount % ResyncQuipRotateTicks == 0)
                {
                    _resyncQuipIndex++;
                }
                // Refresh the status line ~every 900ms so the ETA counts down smoothly (quip rotates slower).
                if (_resyncTickCount % 6 == 0)
                {
                    ApplyResyncStatusText();
                }
            };
        }

        _resyncEaseTimer.Start();
    }

    /// <summary>Records which account/phase the re-sync is on, and refreshes the (witty) status line.</summary>
    private void SetResyncStep(string accountName, int index, int total, bool reloading)
    {
        _resyncAccountName = accountName;
        _resyncStepIndex = index;
        _resyncStepTotal = total;
        _resyncReloading = reloading;
        ApplyResyncStatusText();
    }

    private void ApplyResyncStatusText()
    {
        if (string.IsNullOrEmpty(_resyncAccountName))
        {
            return;
        }

        var pool = _resyncReloading ? ResyncReloadQuips : ResyncReadingQuips;
        var quip = string.Format(pool[_resyncQuipIndex % pool.Length], _resyncAccountName);
        AttentionText.Text = $"{quip} ({_resyncStepIndex} of {_resyncStepTotal})…{ResyncEtaText()}";
    }

    /// <summary>
    /// Best-effort "~time left" from elapsed time vs progress. Shown only once the bar is past the quick
    /// reload phase (>15%) so the estimate is stable; hidden near completion. Approximate by nature.
    /// </summary>
    private string ResyncEtaText()
    {
        if (_resyncStopwatch is null)
        {
            return string.Empty;
        }

        var frac = _resyncDisplayed;
        if (frac < 0.15 || frac >= 0.99)
        {
            return string.Empty;
        }

        var remaining = _resyncStopwatch.Elapsed.TotalSeconds * (1 - frac) / frac;
        if (remaining < 1 || remaining > 3600)
        {
            return string.Empty;
        }

        var span = TimeSpan.FromSeconds(remaining);
        var text = span.TotalMinutes >= 1
            ? $"{(int)span.TotalMinutes}m {span.Seconds:00}s"
            : $"{(int)Math.Ceiling(span.TotalSeconds)}s";
        return $"  ·  ~{text} left";
    }

    /// <summary>Moves the eased soft-cap forward (never backward), so the bar creeps toward it.</summary>
    private void ResyncEaseToward(double ceiling) =>
        _resyncCeiling = Math.Max(_resyncCeiling, Math.Clamp(ceiling, 0, 1));

    /// <summary>Snaps the bar forward to a real completion point.</summary>
    private void ResyncAnchor(double value)
    {
        value = Math.Clamp(value, 0, 1);
        _resyncCeiling = Math.Max(_resyncCeiling, value);
        _resyncDisplayed = Math.Max(_resyncDisplayed, value);
        ApplyResyncBar(_resyncDisplayed);
    }

    private void ApplyResyncBar(double value)
    {
        ResyncProgressBar.Value = value;
        ResyncProgressPercent.Text = $"{(int)Math.Round(value * 100)}%";
    }

    private void EndResyncProgress()
    {
        _resyncEaseTimer?.Stop();
        ResyncProgressRow.Visibility = Visibility.Collapsed;
        AttentionIcon.Glyph = ""; // restore the caution triangle for normal backlog use
    }

    private static async Task<string> ProbeInstanceDbAsync(MessengerInstance instance)
    {
        // Channels with no conversation scraper are not "still loading" — they will never be scanned.
        // Re-sync used to report all three Google Business accounts as "still loading — open this account
        // once to finish loading", which sends the owner off to open a tab that cannot change the outcome.
        if (!PlatformModuleSettingsHelper.IsPlatformModuleEnabled(instance.Platform))
        {
            return "no conversation metrics for this channel";
        }

        // Retry a couple of rounds: a still-loading account settles with a non-'done' diag (the reader
        // returns null), and succeeds once its WhatsApp Web is ready.
        for (var round = 0; round < 3; round++)
        {
            var result = await OversightSnapshotReader.RefreshAsync(instance, harvestPreviews: true).ConfigureAwait(true);
            if (result is { } r)
            {
                var pct = r.Active > 0 ? (int)Math.Round(100.0 * r.CaughtUp / r.Active) : 100;
                return $"{pct}% caught up ({r.CaughtUp}/{r.Active}, {r.Awaiting} awaiting)";
            }
        }

        return "still loading — open this account once to finish loading";
    }
}
