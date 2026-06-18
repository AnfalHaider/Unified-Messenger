using System.Text.Json;
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
            _ => OversightWindow.Today
        };

    private void OnWindowChanged(object sender, SelectionChangedEventArgs e) => Render();

    private DateTimeOffset? WindowStart()
    {
        var nowLocal = DateTimeOffset.Now;
        return SelectedWindow() switch
        {
            OversightWindow.Today => new DateTimeOffset(nowLocal.Date, nowLocal.Offset),
            OversightWindow.Week => new DateTimeOffset(nowLocal.Date.AddDays(-6), nowLocal.Offset),
            _ => null
        };
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

    public void Render()
    {
        if (_services is null)
        {
            return;
        }

        var grouping = GroupToggle.IsOn ? OversightGrouping.ByLocation : OversightGrouping.ByInstance;
        var window = SelectedWindow();
        var instances = _services.Registry.Instances.Where(instance => instance.IsProfessional).ToList();
        var snapshot = _services.Oversight.BuildSnapshot(grouping, instances, window);

        var windowLabel = window switch
        {
            OversightWindow.Today => "today",
            OversightWindow.Week => "the last 7 days",
            _ => "all time"
        };
        SubtitleText.Text = grouping == OversightGrouping.ByLocation
            ? $"Rolled up by location · caught up among chats active {windowLabel}"
            : $"Per account · caught up among chats active {windowLabel} · group into locations (Ctrl+K)";
        _emptyStateWindowLabel = windowLabel;

        if (snapshot.TotalUrgent > 0 || snapshot.TotalDropped > 0)
        {
            AttentionText.Text = snapshot.AttentionSummary;
            AttentionBanner.Visibility = Visibility.Visible;
        }
        else
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

        return new Expander
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Header = BuildRowContent(location),
            Content = content
        };
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
        var windowStart = WindowStart();

        var items = entity.MemberInstanceIds
            .SelectMany(id => OversightChatSnapshotService.Instance.GetAwaiting(id, windowStart)
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
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            line.Children.Add(new TextBlock
            {
                Text = FriendlyChatName(chat.CustomerName, chat.ConversationKey),
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 260,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            line.Children.Add(new TextBlock
            {
                Text = chat.Unread == 1 ? "1 unread" : $"{chat.Unread} unread",
                Foreground = danger,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });

            var item = new Button
            {
                Content = line,
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

    private static List<OversightChatSnapshotService.ChatEntry> ParseChatEntries(JsonElement root)
    {
        var list = new List<OversightChatSnapshotService.ChatEntry>();
        if (!root.TryGetProperty("conversations", out var convs) || convs.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var c in convs.EnumerateArray())
        {
            var unread = c.TryGetProperty("unreadCount", out var u) && u.TryGetInt32(out var uv) ? uv : 0;
            var ts = c.TryGetProperty("lastActivityTimestampUtc", out var t) ? t.GetString() : null;
            var key = c.TryGetProperty("conversationKey", out var k) ? k.GetString() ?? "" : "";
            var name = c.TryGetProperty("customerName", out var n) ? n.GetString() ?? "" : "";
            if (DateTimeOffset.TryParse(ts, out var when))
            {
                list.Add(new OversightChatSnapshotService.ChatEntry(key, name, unread, when.ToUniversalTime()));
            }
        }

        return list;
    }

    private static async Task<string> ProbeInstanceDbAsync(MessengerInstance instance)
    {
        // The JS scan self-settles via a watchdog, so a still-loading page yields a 'watchdog-timeout'
        // diag rather than hanging. Retry a couple of rounds so an account whose WhatsApp Web is mid-load
        // succeeds once it's ready.
        var lastNote = "no result";
        for (var round = 0; round < 3; round++)
        {
            var start = await InstanceSessionManager.Instance
                .TryExecuteScriptOnInstanceAsync(
                    instance.Id,
                    "window.__umStartDbConversationScan ? window.__umStartDbConversationScan(2000) : 'NOFN'")
                .ConfigureAwait(true);

            if (start is not null && start.Contains("NOFN"))
            {
                return "scan fn missing (script not injected)";
            }

            for (var attempt = 0; attempt < 36; attempt++) // ~11s, watchdog settles by 8s
            {
                await Task.Delay(300).ConfigureAwait(true);
                var raw = await InstanceSessionManager.Instance
                    .TryExecuteScriptOnInstanceAsync(
                        instance.Id,
                        "window.__umGetDbConversationResult ? window.__umGetDbConversationResult() : 'NOFN'")
                    .ConfigureAwait(true);

                if (string.IsNullOrWhiteSpace(raw) || raw == "null" || raw == "\"\"")
                {
                    continue; // not settled yet
                }

                try
                {
                    using var doc = JsonDocument.Parse(JsonSerializer.Deserialize<string>(raw) ?? "");
                    var root = doc.RootElement;
                    var stage = root.TryGetProperty("diag", out var diag) &&
                                diag.TryGetProperty("stage", out var s) ? s.GetString() : "?";

                    if (stage == "done")
                    {
                        var chats = ParseChatEntries(root);
                        OversightChatSnapshotService.Instance.Update(instance.Id, chats, DateTimeOffset.UtcNow);

                        var total = chats.Count;
                        var caughtUp = chats.Count(c => c.Unread <= 0);
                        var pct = total > 0 ? (int)Math.Round(100.0 * caughtUp / total) : 100;
                        return $"{pct}% caught up ({caughtUp}/{total}, {total - caughtUp} awaiting)";
                    }

                    lastNote = $"not ready ({stage})";
                    break; // settled but unusable (e.g. watchdog-timeout) → retry the whole scan
                }
                catch
                {
                    return "parse-fail";
                }
            }
        }

        return lastNote + " — open this account once to finish loading";
    }
}
