using System.Text;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Backfill;
using Windows.Foundation;

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

    private bool _digestShown;
    private string _lastRenderSignature = string.Empty;

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

        var grouping = GroupToggle.IsOn ? OversightGrouping.ByLocation : OversightGrouping.ByInstance;
        var window = SelectedWindow();
        var (rangeStart, rangeEnd) = WindowRange();
        var instances = _services.Registry.Instances.Where(instance => instance.IsProfessional).ToList();
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
        SubtitleText.Text = grouping == OversightGrouping.ByLocation
            ? $"Rolled up by location · caught up among chats active {windowLabel}"
            : $"Per account · caught up among chats active {windowLabel} · group into locations (Ctrl+K)";
        _emptyStateWindowLabel = windowLabel;

        if (TryBuildDigestBanner(instances, out var digestText))
        {
            AttentionText.Text = digestText;
            AttentionBanner.Visibility = Visibility.Visible;
        }
        else if (snapshot.TotalUrgent > 0 || snapshot.TotalDropped > 0)
        {
            AttentionText.Text = snapshot.AttentionSummary;
            AttentionBanner.Visibility = Visibility.Visible;
        }
        else if (!_resyncInProgress)
        {
            AttentionBanner.Visibility = Visibility.Collapsed;
        }

        CardsHost.Children.Clear();
        if (snapshot.Entities.Count == 0)
        {
            CardsHost.Children.Add(new TextBlock
            {
                Text = "No professional accounts yet — add one to see oversight here.",
                Foreground = Brush("TextFillColorSecondaryBrush"),
                TextWrapping = TextWrapping.WrapWholeWords
            });
            return;
        }

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
                CardsHost.Children.Add(BuildExpander(location, members));
            }
        }
        else
        {
            foreach (var entity in snapshot.Entities)
            {
                CardsHost.Children.Add(BuildRow(entity));
            }
        }
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
            Header = BuildRowContent(location),
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
            Padding = new Thickness(12, 4, 12, 10),
            Header = BuildRowContent(entity),
            Content = BuildAwaitingPanel(entity),
            IsExpanded = _expandedKeys.Contains(entity.Key)
        };

        // Preserve open/closed state across the 20s auto-refresh re-render.
        expander.Expanding += (_, _) => _expandedKeys.Add(entity.Key);
        expander.Collapsed += (_, _) => _expandedKeys.Remove(entity.Key);
        return expander;
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
            var topLine = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            topLine.Children.Add(new TextBlock
            {
                Text = FriendlyChatName(chat.CustomerName, chat.ConversationKey),
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 260,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            topLine.Children.Add(new TextBlock
            {
                Text = chat.Unread == 1 ? "1 unread" : $"{chat.Unread} unread",
                Foreground = danger,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });

            var column = new StackPanel { Spacing = 1 };
            column.Children.Add(topLine);
            if (!string.IsNullOrWhiteSpace(chat.Preview))
            {
                // A glimpse of the last message (scraped from the sidebar preview).
                column.Children.Add(new TextBlock
                {
                    Text = chat.Preview,
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

    private StackPanel BuildRowContent(OversightEntityHealth entity)
    {
        var secondary = Brush("TextFillColorSecondaryBrush");
        var danger = Brush("SystemFillColorCriticalBrush");
        var hasLiveData = entity.MeasuredCount > 0;
        var statusBrush = !hasLiveData
            ? secondary
            : entity.OnTimePercent >= 90
                ? Brush("SystemFillColorSuccessBrush")
                : entity.OnTimePercent >= 70
                    ? Brush("SystemFillColorCautionBrush")
                    : Brush("SystemFillColorCriticalBrush");

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            VerticalAlignment = VerticalAlignment.Center
        };

        row.Children.Add(new Ellipse
        {
            Width = 9,
            Height = 9,
            Fill = entity.IsStale ? danger : Brush("SystemFillColorSuccessBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });

        row.Children.Add(new TextBlock
        {
            Text = entity.Kind == OversightEntityKind.Location
                ? $"{entity.DisplayName}  ({entity.AccountCount})"
                : entity.DisplayName,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 170,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        var statusText = !entity.HasChatData
            ? "syncing…"
            : hasLiveData
                ? $"{entity.OnTimePercent}% caught up"
                : $"no activity {_emptyStateWindowLabel}";
        row.Children.Add(new TextBlock
        {
            Text = statusText,
            Foreground = statusBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 110
        });

        var awaitingText = !entity.HasChatData || !hasLiveData
            ? "—"
            : entity.AwaitingCount == 1
                ? "1 awaiting reply"
                : $"{entity.AwaitingCount} awaiting reply";

        // Plain text — expand the row (accordion) to see and open the actual waiting customers.
        row.Children.Add(new TextBlock
        {
            Text = awaitingText,
            Foreground = entity.AwaitingCount > 0 ? danger : secondary,
            FontWeight = entity.AwaitingCount > 0 ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 170
        });

        var sparkline = BuildSparkline(entity.TrendCounts, statusBrush);
        ToolTipService.SetToolTip(sparkline, "Activity over the last 7 days");
        row.Children.Add(sparkline);

        var freshness = entity.IsStale
            ? "stale — reconnect"
            : entity.HistoricalOpenCount > 0
                ? $"synced · {entity.HistoricalOpenCount} from history"
                : "synced";
        row.Children.Add(new TextBlock
        {
            Text = freshness,
            FontSize = 12,
            Foreground = secondary,
            VerticalAlignment = VerticalAlignment.Center
        });


        return row;
    }

    /// <summary>
    /// A compact 7-day activity sparkline derived from <see cref="OversightEntityHealth.TrendCounts"/>.
    /// Falls back to a flat baseline when there is no recent activity to plot.
    /// </summary>
    private FrameworkElement BuildSparkline(IReadOnlyList<int> counts, Brush stroke)
    {
        const double width = 64;
        const double height = 18;

        var host = new Grid
        {
            Width = width,
            Height = height,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (counts is null || counts.Count < 2 || counts.All(c => c == 0))
        {
            host.Children.Add(new Rectangle
            {
                Height = 1,
                Fill = Brush("TextFillColorDisabledBrush"),
                Opacity = 0.6,
                VerticalAlignment = VerticalAlignment.Center
            });
            return host;
        }

        var max = Math.Max(1, counts.Max());
        var step = width / (counts.Count - 1);
        var points = new PointCollection();
        for (var i = 0; i < counts.Count; i++)
        {
            var x = i * step;
            var y = (height - 1) - (counts[i] / (double)max) * (height - 2);
            points.Add(new Point(x, y));
        }

        host.Children.Add(new Polyline
        {
            Points = points,
            Stroke = stroke,
            StrokeThickness = 1.5,
            StrokeLineJoin = PenLineJoin.Round
        });
        return host;
    }

    private static Brush Brush(string key) =>
        Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush
            ? brush
            : new SolidColorBrush(Microsoft.UI.Colors.Gray);

    private void OnGroupToggled(object sender, RoutedEventArgs e) => Render();

    private void OnRefresh(object sender, RoutedEventArgs e) => Render();

    private bool _resyncInProgress;

    /// <summary>
    /// Deterministically re-runs history backfill for every professional account (force), then reports
    /// what the IndexedDB read returned and how much was reconciled — so reconciliation no longer
    /// depends on auto-trigger timing, and the result is observable.
    /// </summary>
    private async void OnResyncHistory(object sender, RoutedEventArgs e)
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
        AttentionText.Text = "Probing each account's local history…";

        // Direct diagnostic: run the IndexedDB scan straight on each webview and read the raw result,
        // bypassing the backfill pipeline so we isolate whether the read itself works.
        var parts = new List<string>();
        foreach (var instance in pros)
        {
            var line = await ProbeInstanceDbAsync(instance);
            parts.Add($"{instance.DisplayName}: {line}");

            // Also kick off the real backfill so reconciliation still happens when the read works.
            BackfillSyncManager.Instance.Schedule(instance, force: true);
        }

        Render();

        AttentionBanner.Visibility = Visibility.Visible;
        AttentionText.Text = "Probe · " + string.Join("   |   ", parts);
        ResyncButton.IsEnabled = true;
        _resyncInProgress = false;
    }

    private static async Task<string> ProbeInstanceDbAsync(MessengerInstance instance)
    {
        // Retry a couple of rounds: a still-loading account settles with a non-'done' diag (the reader
        // returns null), and succeeds once its WhatsApp Web is ready.
        for (var round = 0; round < 3; round++)
        {
            var result = await OversightSnapshotReader.RefreshAsync(instance).ConfigureAwait(true);
            if (result is { } r)
            {
                var pct = r.Active > 0 ? (int)Math.Round(100.0 * r.CaughtUp / r.Active) : 100;
                return $"{pct}% caught up ({r.CaughtUp}/{r.Active}, {r.Awaiting} awaiting)";
            }
        }

        return "still loading — open this account once to finish loading";
    }
}
