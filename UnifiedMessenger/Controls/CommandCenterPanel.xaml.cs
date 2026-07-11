using System.Text;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Ai;
using UnifiedMessenger.Services.Backfill;
using UnifiedMessenger.ViewModels;

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
        var nowLocal = DateTimeOffset.Now;
        switch (SelectedWindow())
        {
            case OversightWindow.Today:
                return (new DateTimeOffset(nowLocal.Date, nowLocal.Offset), null);
            case OversightWindow.Week:
                return (new DateTimeOffset(nowLocal.Date.AddDays(-6), nowLocal.Offset), null);
            case OversightWindow.Custom:
                DateTimeOffset? start = FromDatePicker?.Date is { } f
                    ? new DateTimeOffset(f.Date, f.Offset)
                    : null;
                DateTimeOffset? end = ToDatePicker?.Date is { } t
                    ? new DateTimeOffset(t.Date, t.Offset).AddDays(1).AddTicks(-1)
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
                .Append(',').Append(e.HistoricalOpenCount).Append(',').Append(e.IsStale ? 1 : 0).Append(';');
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
        text = $"{since}{digest.NewAwaiting} new awaiting reply · {digest.TotalAwaiting} total across {digest.AccountsWithAwaiting} {accountWord}";
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
        // Oversight cards are only meaningful for platforms that contribute scraped metrics (WhatsApp
        // family). Embed channels (Google Business / Telegram / Messenger / generic) are visible+usable in
        // the sidebar but have no chat store to scan, so including them here would strand them at "syncing…"
        // forever. They simply don't appear in the command center.
        var instances = _services.Registry.Instances
            .Where(instance => instance.IsProfessional &&
                               PlatformModuleSettingsHelper.IsPlatformModuleEnabled(instance.Platform))
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
                FontSize = 12,
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
            BorderBrush = Brush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = _compact ? new Thickness(14, 4, 14, 6) : new Thickness(16, 10, 16, 12),
            Header = BuildHeader(entity),
            Content = BuildAwaitingPanel(entity),
            IsExpanded = _expandedKeys.Contains(entity.Key)
        };

        // Preserve open/closed state across the 20s auto-refresh re-render.
        expander.Expanding += (_, _) => _expandedKeys.Add(entity.Key);
        expander.Collapsed += (_, _) => _expandedKeys.Remove(entity.Key);

        // Full-height status rail to the left of the card — status by position+colour (the % hero glyph
        // carries the non-colour cue for WCAG). Stale accounts read critical.
        var hasLiveData = entity.MeasuredCount > 0;
        var railBrush = entity.IsStale
            ? Brush("SystemFillColorCriticalBrush")
            : !hasLiveData
                ? Brush("TextFillColorDisabledBrush")
                : StatusBrushForPercent(entity.OnTimePercent);
        var rail = new Border
        {
            Width = 3,
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
                FontSize = 12,
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
                    FontSize = 12,
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

            var capturedInstanceId = instanceId;
            var capturedChat = chat;
            item.Click += (_, _) =>
                _services?.Navigation.OpenInstance(capturedInstanceId, capturedChat.ConversationKey, capturedChat.CustomerName);
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

        var rows = scoped
            .SelectMany(inst => OversightChatSnapshotService.Instance
                .GetAwaiting(inst.Id)
                .Select(chat => (Instance: inst, Chat: chat)))
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
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        });
        header.Children.Add(new TextBlock
        {
            Text = count == 1 ? "· 1 waiting" : $"· {count} waiting",
            FontSize = 11,
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
        int fresh = 0, quarter = 0, hour = 0, old = 0;
        foreach (var since in waitingSinceUtc)
        {
            var mins = (now - since).TotalMinutes;
            if (mins < 15) fresh++;
            else if (mins < 60) quarter++;
            else if (mins < 240) hour++;
            else old++;
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
            content.Children.Add(new TextBlock { Text = count.ToString(), FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = fg, VerticalAlignment = VerticalAlignment.Center });
            content.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = fg, VerticalAlignment = VerticalAlignment.Center });
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

        AddBand(old, "waiting >4h", "SystemFillColorCriticalBrush", "Waiting more than 4 hours — reply to these first.");
        AddBand(hour, "1–4h", "SystemFillColorCautionBrush", "Waiting 1 to 4 hours.");
        AddBand(quarter, "15m–1h", "SystemFillColorAttentionBrush", "Waiting 15 minutes to 1 hour.");
        AddBand(fresh, "<15m", "SystemFillColorSuccessBrush", "Just arrived — under 15 minutes.");
        return strip;
    }

    /// <summary>A "Showing: &lt;account&gt; ✕" chip above the scoped Needs-reply list; click clears the scope.</summary>
    private FrameworkElement BuildScopeChip(string label)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new TextBlock
        {
            Text = $"Showing: {label}",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        content.Children.Add(new FontIcon { Glyph = "", FontSize = 11, VerticalAlignment = VerticalAlignment.Center });

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
            FontSize = 13,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        if (!string.IsNullOrWhiteSpace(enrichedPreview))
        {
            left.Children.Add(new TextBlock
            {
                Text = enrichedPreview,
                Foreground = secondary,
                FontSize = 12,
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
            Glyph = "", // Warning (ErrorBadge family) — Segoe Fluent
            FontSize = 11,
            Foreground = danger,
            VerticalAlignment = VerticalAlignment.Center
        });
        unreadLine.Children.Add(new TextBlock
        {
            Text = chat.Unread > 0 ? (chat.Unread == 1 ? "1 unread" : $"{chat.Unread} unread") : "needs reply",
            Foreground = danger,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        right.Children.Add(unreadLine);
        right.Children.Add(new TextBlock
        {
            Text = $"{inst.DisplayName} · {RelativeAge(chat.LastActivityUtc)}",
            Foreground = secondary,
            FontSize = 11,
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
            _services?.Navigation.OpenInstance(capturedId, capturedChat.ConversationKey, capturedChat.CustomerName);

        // Row = the click-through button + an overflow menu (mark handled elsewhere / snooze).
        var rowGrid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(button, 0);
        rowGrid.Children.Add(button);

        var overflow = BuildAwaitingActionButton(inst.Id, chat, displayName);
        Grid.SetColumn(overflow, 1);
        overflow.VerticalAlignment = VerticalAlignment.Center;
        overflow.Margin = new Thickness(4, 0, 0, 0);
        rowGrid.Children.Add(overflow);
        return rowGrid;
    }

    /// <summary>
    /// The per-chat overflow menu on an awaiting row: mark it handled elsewhere (drops off the list until a
    /// newer customer message arrives) or snooze it for a while. Both suppress it from every awaiting metric
    /// via <see cref="AwaitingOverrideStore"/> and self-expire.
    /// </summary>
    private Button BuildAwaitingActionButton(string instanceId, OversightChatSnapshotService.ChatEntry chat, string displayName)
    {
        var flyout = new MenuFlyout();

        void Refresh()
        {
            _lastRenderSignature = string.Empty;
            Render();
        }

        var handled = new MenuFlyoutItem { Text = "Mark handled (replied elsewhere)", Icon = new FontIcon { Glyph = "" } };
        handled.Click += (_, _) =>
        {
            AwaitingOverrideStore.Instance.MarkHandled(instanceId, chat.ConversationKey, chat.LastActivityUtc);
            Refresh();
        };
        flyout.Items.Add(handled);
        flyout.Items.Add(new MenuFlyoutSeparator());

        void AddSnooze(string label, TimeSpan duration)
        {
            var item = new MenuFlyoutItem { Text = label };
            item.Click += (_, _) =>
            {
                AwaitingOverrideStore.Instance.Snooze(instanceId, chat.ConversationKey, DateTimeOffset.UtcNow + duration);
                Refresh();
            };
            flyout.Items.Add(item);
        }

        AddSnooze("Snooze 1 hour", TimeSpan.FromHours(1));
        AddSnooze("Snooze 4 hours", TimeSpan.FromHours(4));
        AddSnooze("Snooze until tomorrow", TimeSpan.FromHours(Math.Max(1, 24 - DateTime.Now.Hour)));

        var button = new Button
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8),
            Content = new FontIcon { Glyph = "", FontSize = 14 }, // More (…)
            Flyout = flyout
        };
        ToolTipService.SetToolTip(button, $"Handle or snooze {displayName}");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, $"Actions for {displayName}");
        return button;
    }

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
            overallPct = (int)Math.Round(live.Sum(e => (long)e.OnTimePercent * e.MeasuredCount) / (double)measured);
        }

        var totalAwaiting = entities.Sum(e => e.AwaitingCount);
        var behind = entities.Count(e => e.AwaitingCount > 0);

        // Record today's whole-business KPIs so the two tiles can show a daily micro-trend.
        if (overallPct is { } recPct)
        {
            KpiTrendStore.Instance.Record(recPct, totalAwaiting);
        }

        tiles.Add(new KpiTileViewModel
        {
            Label = "Caught up",
            Value = overallPct is { } p ? $"{p}%" : "—",
            ValueBrush = overallPct is { } pp ? StatusBrushForPercent(pp) : secondary,
            Hint = "unread cleared, across accounts",
            ActionKey = overallPct is null ? string.Empty : "caughtup",
            Trend = KpiTrendStore.Instance.GetCaughtUpTrend(),
            Tooltip = "Share of active chats with no unread messages. This measures unread cleared — not reply speed (see Response time)."
        });

        tiles.Add(new KpiTileViewModel
        {
            Label = "Awaiting reply",
            Value = totalAwaiting.ToString(),
            ValueBrush = totalAwaiting > 0 ? primary : success,
            Hint = behind switch { 0 => "all accounts clear", 1 => "1 account behind", _ => $"{behind} accounts behind" },
            ActionKey = totalAwaiting > 0 ? "awaiting" : string.Empty,
            Trend = KpiTrendStore.Instance.GetAwaitingTrend(),
            Tooltip = "Customers still waiting on a first reply. Click to see them, most urgent first."
        });

        // Response time (FRT) + SLA compliance — forward-tracked from real message timestamps.
        var slaThreshold = AppSettingsService.Instance.Settings.SlaThresholdMinutes;
        var response = ResponseTimeTracker.Instance.GetStats(instances, rangeStart, rangeEnd, slaThreshold);
        tiles.Add(new KpiTileViewModel
        {
            Label = "Response time",
            Value = response.HasData ? FormatMinutes(response.MedianMinutes) : "—",
            ValueBrush = response.HasData ? ResponseBrush(response.MedianMinutes, slaThreshold) : secondary,
            Hint = response.HasData ? $"median · {response.SampleCount} replies" : "builds as you reply",
            Tooltip = $"Median time from a customer's message to your first reply (measured live since tracking began). Target: under {slaThreshold} min."
        });

        tiles.Add(new KpiTileViewModel
        {
            Label = "SLA met",
            Value = response.HasData ? $"{response.SlaCompliancePercent}%" : "—",
            ValueBrush = response.HasData ? StatusBrushForPercent(response.SlaCompliancePercent) : secondary,
            Hint = response.HasData ? $"replied within {slaThreshold} min" : $"target {slaThreshold} min",
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
            ValueBrush = busyHour == "—" ? secondary : caution,
            Hint = busyDay == "—" ? "peak hour" : $"peak hour · {busyDay}",
            ActionKey = busyHour == "—" ? string.Empty : "busiest",
            Tooltip = "Your peak inbound hour and weekday — plan coverage around it. Click to open the activity graph."
        });

        KpiBand.ItemsSource = tiles;
        KpiBand.Visibility = Visibility.Visible;

        RenderHero(overallPct, totalAwaiting, behind, entities, instances);
        RenderBriefing(entities, instances, overallPct, totalAwaiting, behind, busyHour);
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
        IReadOnlyList<MessengerInstance> instances)
    {
        if (overallPct is null)
        {
            // No live data yet (first launch, still syncing) — the skeleton/empty state carries this.
            HeroCard.Visibility = Visibility.Collapsed;
            return;
        }

        var caughtUp = totalAwaiting == 0;
        // Semantic red/green pops the same in both themes, so the app-level Brush() lookup is fine here.
        var accent = caughtUp ? Brush("SystemFillColorSuccessBrush") : Brush("SystemFillColorCriticalBrush");
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
                FontSize = 30,
                Foreground = accent,
                VerticalAlignment = VerticalAlignment.Center
            });
            headline.Children.Add(new TextBlock
            {
                Text = "You're all caught up",
                FontSize = 24,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        else
        {
            headline.Children.Add(new TextBlock
            {
                Text = totalAwaiting.ToString(),
                FontSize = 42,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = accent,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -2, 0, 0)
            });
            headline.Children.Add(new TextBlock
            {
                Text = totalAwaiting == 1 ? "customer is waiting\nfor a reply" : "customers are waiting\nfor a reply",
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                LineHeight = 19,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        text.Children.Add(headline);

        text.Children.Add(new TextBlock
        {
            Text = BuildHeroSubtext(caughtUp, overallPct.Value, accountsBehind, entities, instances),
            FontSize = 12.5,
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
        HeroCard.BorderBrush = Brush("CardStrokeColorDefaultBrush");
        HeroCard.Visibility = Visibility.Visible;
        // (Depth via ThemeShadow was removed — the accent rail + large number already give the hero enough
        //  visual weight, and the imperative Shadow+Translation was an unverified, finicky variable.)
    }

    /// <summary>The hero's supporting line — oldest wait + the account furthest behind + overall caught-up %.</summary>
    private string BuildHeroSubtext(
        bool caughtUp,
        int overallPct,
        int accountsBehind,
        IReadOnlyList<OversightEntityHealth> entities,
        IReadOnlyList<MessengerInstance> instances)
    {
        if (caughtUp)
        {
            return $"No customers are waiting on a reply · {overallPct}% caught up overall.";
        }

        var parts = new List<string>(3);

        var ids = instances.Where(i => !string.IsNullOrWhiteSpace(i.Id)).Select(i => i.Id).ToList();
        var digest = OversightChatSnapshotService.Instance.BuildDigest(ids, null);
        if (digest.OldestActivityUtc is { } oldest)
        {
            var mins = (DateTimeOffset.UtcNow - oldest).TotalMinutes;
            if (mins >= 1)
            {
                parts.Add($"oldest {FormatMinutes(mins)}");
            }
        }

        var worst = entities.Where(e => e.AwaitingCount > 0)
            .OrderByDescending(e => e.AwaitingCount)
            .ThenBy(e => e.OnTimePercent)
            .FirstOrDefault();
        if (worst is not null && !string.IsNullOrWhiteSpace(worst.DisplayName))
        {
            parts.Add(worst.DisplayName);
        }

        parts.Add($"{overallPct}% caught up overall");
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
    private void OnKpiTileTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
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
        string busyHour)
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

        string heuristic;
        if (totalAwaiting == 0)
        {
            heuristic = $"All caught up — nothing waiting on a reply.{projectionNote}{busy}";
        }
        else
        {
            var customers = totalAwaiting == 1 ? "1 customer is" : $"{totalAwaiting} customers are";
            var accountWord = accountsBehind == 1 ? "account" : "accounts";
            var start = worst is not null
                ? $" Start with {worst.DisplayName} ({worst.AwaitingCount} waiting, {worst.OnTimePercent}% caught up)."
                : string.Empty;
            heuristic = $"{customers} waiting across {accountsBehind} {accountWord}.{start}{projectionNote}";
        }

        var displayText = heuristic;
        var isAi = false;
        if (AppSettingsService.Instance.Settings.EnableLocalAi)
        {
            var signature = $"{overallPct}|{totalAwaiting}|{accountsBehind}|{worst?.Key}|{worst?.AwaitingCount}|{busyHour}|{eod.Projected}|{busierThanUsual}";
            var cached = OversightInsightService.Instance.TryGet(BriefingCacheKey, signature);
            if (cached is not null)
            {
                displayText = cached;
                isAi = true;
            }
            else
            {
                var prompt =
                    $"Across {entities.Count} accounts: {totalAwaiting} customer(s) waiting, {accountsBehind} account(s) " +
                    $"behind, {overallPct}% caught up overall." +
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

        var customerWord = entity.AwaitingCount == 1 ? "customer is" : "customers are";
        var sb = new StringBuilder();
        sb.Append("Needs attention — ").Append(entity.AwaitingCount).Append(' ').Append(customerWord)
            .Append(" waiting on a reply");
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

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        content.Children.Add(new TextBlock
        {
            Text = isAi ? "✦ AI" : "✦",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = badge,
            Opacity = 0.9,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 0, 0)
        });
        content.Children.Add(new TextBlock
        {
            Text = displayText,
            Foreground = fg,
            FontSize = 12,
            TextWrapping = TextWrapping.WrapWholeWords
        });

        return new Border
        {
            Background = bg,
            BorderBrush = Brush("CardStrokeColorDefaultBrush"),
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
        nameColumn.Children.Add(new TextBlock
        {
            Text = nameText,
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        if (!_compact)
        {
            // Per-card data freshness: when this account's chats were last read. Locations show their
            // least-fresh member so a silently-stale branch account can't hide behind a fresh sibling.
            var capturedAt = entity.MemberInstanceIds
                .Select(OversightChatSnapshotService.Instance.TryGetCapturedAtUtc)
                .Where(t => t is not null)
                .DefaultIfEmpty(null)
                .Min();
            var freshness = entity.IsStale
                ? "stale — right-click the account → Refresh WebView, then Re-sync"
                : capturedAt is { } cap
                    ? $"updated {RelativeAge(cap)}{(entity.HistoricalOpenCount > 0 ? $" · {entity.HistoricalOpenCount} chats tracked" : string.Empty)}"
                    : "waiting for first sync…";
            var freshnessBlock = new TextBlock
            {
                Text = freshness,
                FontSize = 11,
                Foreground = entity.IsStale ? danger : Brush("TextFillColorTertiaryBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            ToolTipService.SetToolTip(freshnessBlock,
                "When this account's chat data was last read. Numbers on this card are only as fresh as this stamp — click Re-sync to update.");
            nameColumn.Children.Add(freshnessBlock);
        }

        // Awaiting pill (right-aligned): a soft danger chip when behind, quiet text when caught up.
        FrameworkElement awaitingVisual;
        if (!entity.HasChatData || !hasLiveData)
        {
            awaitingVisual = new TextBlock
            {
                Text = "—", Foreground = secondary, FontSize = 12, VerticalAlignment = VerticalAlignment.Center
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
                    Text = entity.AwaitingCount == 1 ? "1 awaiting" : $"{entity.AwaitingCount} awaiting",
                    Foreground = danger,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12
                }
            };
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
                Text = "caught up", Foreground = secondary, FontSize = 12, VerticalAlignment = VerticalAlignment.Center
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
                FontSize = 14,
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
                Content = new FontIcon { Glyph = "", FontSize = 14 } // BarChart
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
            var stateBlock = new TextBlock
            {
                Text = !entity.HasChatData ? "syncing…" : $"no activity {_emptyStateWindowLabel}",
                Foreground = secondary,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(stateBlock, 0);
            metricRow.Children.Add(stateBlock);
        }
        else
        {
            var pctCell = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };

            // WCAG 1.4.1: status must not be conveyed by colour alone. A shape-distinct glyph
            // (check / warning / error) encodes health independently of the % colour.
            var (statusGlyph, statusLabel) = StatusGlyph(entity.OnTimePercent);
            var glyphIcon = new FontIcon
            {
                Glyph = statusGlyph,
                FontSize = 16,
                Foreground = statusBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            ToolTipService.SetToolTip(glyphIcon, statusLabel);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(glyphIcon, statusLabel);
            pctCell.Children.Add(glyphIcon);

            pctCell.Children.Add(new TextBlock
            {
                Text = $"{entity.OnTimePercent}%",
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = statusBrush
            });
            pctCell.Children.Add(new TextBlock
            {
                Text = "caught up",
                FontSize = 12,
                Foreground = secondary,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 4)
            });
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
            FontSize = 9,
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

            var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

            // Show reply speed only once there's live data; a perpetual "measuring…" on every card just
            // reads as stuck (the KPI band's "Response time — · builds as you reply" sets that once).
            if (resp.HasData)
            {
                var replies = resp.SampleCount == 1 ? "1 reply measured" : $"{resp.SampleCount} replies measured";
                chips.Children.Add(BuildMetricChip(
                    "",
                    $"reply ~{FormatMinutes(resp.MedianMinutes)}",
                    ResponseBrush(resp.MedianMinutes, slaMinutes),
                    $"Median time from a customer's message to this account's first reply ({replies}). Target: under {slaMinutes} min."));
            }

            if (resp.AnsweredToday > 0)
            {
                chips.Children.Add(BuildMetricChip(
                    "",
                    resp.AnsweredToday == 1 ? "1 answered today" : $"{resp.AnsweredToday} answered today",
                    success,
                    "Waiting customers this account replied to today — work done, not just work pending."));
            }

            if (pastSlaCount > 0)
            {
                chips.Children.Add(BuildMetricChip(
                    "",
                    $"{pastSlaCount} past {slaMinutes}m",
                    caution,
                    $"Of the {entity.AwaitingCount} awaiting, {pastSlaCount} have already waited longer than your {slaMinutes}-minute reply target — reply to these first."));
            }

            if (entity.UrgentCount > 0)
            {
                chips.Children.Add(BuildMetricChip(
                    "",
                    $"{entity.UrgentCount} urgent",
                    danger,
                    "Messages whose wording looks urgent (triage keywords / local AI)."));
            }

            if (entity.DroppedCount > 0)
            {
                chips.Children.Add(BuildMetricChip(
                    "",
                    $"{entity.DroppedCount} dropped",
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
                    FontSize = 11,
                    Foreground = Brush("TextFillColorTertiaryBrush"),
                    TextWrapping = TextWrapping.WrapWholeWords
                });
            }
        }

        return card;
    }

    /// <summary>A small icon+text pill for the card's detail row; the tooltip carries the plain-language explanation.</summary>
    private FrameworkElement BuildMetricChip(string glyph, string text, Brush foreground, string tooltip)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        content.Children.Add(new FontIcon
        {
            Glyph = glyph,
            FontSize = 11,
            Foreground = foreground,
            VerticalAlignment = VerticalAlignment.Center
        });
        content.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = foreground,
            VerticalAlignment = VerticalAlignment.Center
        });

        var chip = new Border
        {
            Background = Brush("CardBackgroundFillColorSecondaryBrush"),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 3, 8, 3),
            Child = content
        };
        ToolTipService.SetToolTip(chip, tooltip);
        return chip;
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
            CornerRadius = new CornerRadius(4),
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
            CornerRadius = new CornerRadius(15),
            Background = Brush("ControlFillColorSecondaryBrush")
        });
        top.Children.Add(lines);

        var inner = new StackPanel { Spacing = 12 };
        inner.Children.Add(top);
        inner.Children.Add(Bar(230, 18));

        var cardBorder = new Border
        {
            Background = Brush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = Brush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
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
        await dialog.ShowAsync();
    }

    /// <summary>Switches to the Needs-reply list scoped to one account/location (from a card's awaiting pill).</summary>
    private void ShowNeedsReplyFor(List<string> instanceIds, string label)
    {
        _needsReplyFilterIds = instanceIds is { Count: > 0 } ? instanceIds : null;
        _needsReplyFilterLabel = label;
        SelectMode(NeedsReplyButton);
    }

    // Segmented control: exactly one of {By account, By location, Needs reply} is active.
    private void SelectMode(ToggleButton active)
    {
        GroupByAccountButton.IsChecked = ReferenceEquals(active, GroupByAccountButton);
        GroupByLocationButton.IsChecked = ReferenceEquals(active, GroupByLocationButton);
        NeedsReplyButton.IsChecked = ReferenceEquals(active, NeedsReplyButton);
        // Leaving Needs-reply mode clears any per-account scope.
        if (!ReferenceEquals(active, NeedsReplyButton))
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

        await dialog.ShowAsync();
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
            return;
        }

        _resyncInProgress = true;
        ResyncButton.IsEnabled = false;
        AttentionBanner.Visibility = Visibility.Visible;
        BeginResyncProgress();

        try
        {
            var n = pros.Count;

            // Reload each account's WebView first so the latest scraper script is (re)injected — script is
            // only injected on document creation, so without this an app update wouldn't take effect until a
            // manual "Refresh WebView". The probe below waits for each page to re-render before harvesting.
            // Reload phase carries the first 15% of the bar; the slow per-account probe carries the rest.
            for (var i = 0; i < n; i++)
            {
                SetResyncStep(pros[i].DisplayName, i + 1, n, reloading: true);
                ResyncEaseToward(0.13 * (i + 1) / n);
                await _services.SessionManager.ReloadSessionAsync(pros[i].Id);
                ResyncAnchor(0.15 * (i + 1) / n);
            }

            // Direct diagnostic: run the IndexedDB scan straight on each webview and read the raw result,
            // bypassing the backfill pipeline so we isolate whether the read itself works. Each account owns
            // an equal 85%/n slot; the bar eases up to just below its boundary, then snaps there when the
            // probe returns — so it moves during the long wait and stays honest at every account boundary.
            var parts = new List<string>();
            for (var i = 0; i < n; i++)
            {
                var instance = pros[i];
                var boundary = 0.15 + 0.85 * (i + 1) / n;
                SetResyncStep(instance.DisplayName, i + 1, n, reloading: false);
                ResyncEaseToward(boundary - 0.02);

                var line = await ProbeInstanceDbAsync(instance);
                parts.Add($"{instance.DisplayName}: {line}");

                // Also kick off the real backfill so reconciliation still happens when the read works.
                BackfillSyncManager.Instance.Schedule(instance, force: true);
                ResyncAnchor(boundary);
            }

            ResyncAnchor(1.0);
            Render();

            AttentionBanner.Visibility = Visibility.Visible;
            AttentionText.Text = "Probe · " + string.Join("   |   ", parts);
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

                if (++_resyncTickCount % ResyncQuipRotateTicks == 0)
                {
                    _resyncQuipIndex++;
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
        AttentionText.Text = $"{quip} ({_resyncStepIndex} of {_resyncStepTotal})…";
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
