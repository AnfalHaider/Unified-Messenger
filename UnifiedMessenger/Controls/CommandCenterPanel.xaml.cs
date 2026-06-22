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
    private string _searchQuery = string.Empty;
    private bool _compact;
    private string? _worstEntityFirstInstanceId;

    private void OnSearchChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        _searchQuery = sender.Text?.Trim() ?? string.Empty;
        _lastRenderSignature = string.Empty; // search isn't part of the data signature — force the rebuild
        Render();
    }

    private void OnDensityToggled(object sender, RoutedEventArgs e)
    {
        _compact = DensityToggle.IsOn;
        _lastRenderSignature = string.Empty;
        Render();
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

        // Resolve the worst entity's first instance id for the Jump button.
        _worstEntityFirstInstanceId = null;
        if (!string.IsNullOrWhiteSpace(snapshot.WorstEntityKey))
        {
            var worst = snapshot.Entities.FirstOrDefault(e =>
                string.Equals(e.Key, snapshot.WorstEntityKey, StringComparison.OrdinalIgnoreCase));
            _worstEntityFirstInstanceId = worst?.MemberInstanceIds.FirstOrDefault();
        }

        if (TryBuildDigestBanner(instances, out var digestText))
        {
            AttentionText.Text = digestText;
            AttentionBanner.Visibility = Visibility.Visible;
            AttentionJumpButton.Visibility = Visibility.Collapsed;
        }
        else if (snapshot.TotalUrgent > 0 || snapshot.TotalDropped > 0)
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

        CardsHost.Children.Clear();
        CardsHost.Spacing = _compact ? 4 : 8;
        if (snapshot.Entities.Count == 0)
        {
            // Distinguish "no accounts" from "accounts exist but haven't finished their first local-history
            // scan yet" — on startup the WhatsApp IndexedDB read takes a few seconds, and showing "no
            // accounts" during that window is misleading.
            CardsHost.Children.Add(new TextBlock
            {
                Text = instances.Count > 0
                    ? "Syncing accounts — reading each account's local history…"
                    : "No professional accounts yet — add one to see oversight here.",
                Foreground = Brush("TextFillColorSecondaryBrush"),
                TextWrapping = TextWrapping.WrapWholeWords
            });
            return;
        }

        // "Needs reply" mode: a single flat, cross-account list of every awaiting customer, worst-first —
        // the unified "work through the backlog" view (replaces the standalone Work Queue page).
        if (needsReply)
        {
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
        var (windowStart, windowEnd) = WindowRange();

        var rows = instances
            .SelectMany(inst => OversightChatSnapshotService.Instance
                .GetAwaiting(inst.Id, windowStart, windowEnd)
                .Select(chat => (Instance: inst, Chat: chat)))
            .OrderByDescending(x => x.Chat.Unread)
            .ThenBy(x => x.Chat.LastActivityUtc)
            .Take(200)
            .ToList();

        if (rows.Count == 0)
        {
            CardsHost.Children.Add(new TextBlock
            {
                Text = "All caught up — no customers are waiting on a reply.",
                Foreground = secondary,
                TextWrapping = TextWrapping.WrapWholeWords,
                Margin = new Thickness(2, 6, 2, 0)
            });
            return;
        }

        foreach (var (inst, chat) in rows)
        {
            CardsHost.Children.Add(BuildNeedsReplyRow(inst, chat, secondary, danger));
        }
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
            Background = Brush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = Brush("CardStrokeColorDefaultBrush"),
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
        var statusBrush = !hasLiveData
            ? secondary
            : entity.OnTimePercent >= 90
                ? Brush("SystemFillColorSuccessBrush")
                : entity.OnTimePercent >= 70
                    ? Brush("SystemFillColorCautionBrush")
                    : Brush("SystemFillColorCriticalBrush");

        var card = new StackPanel
        {
            Spacing = _compact ? 4 : 8,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // ── Top row: accent bar + avatar circle + name + awaiting badge ──────────────────────
        var topRow = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Status accent bar
        topRow.Children.Add(new Border
        {
            Width = 4,
            Height = 24,
            CornerRadius = new CornerRadius(2),
            Background = entity.IsStale ? danger : statusBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        });

        // Avatar: colored circle with initials
        var accentColor = ResolveEntityAccentColor(entity);
        var avatarBrush = new SolidColorBrush(PlatformBrandingHelper.ParseAccentColor(accentColor));
        var initials = PlatformBrandingHelper.GetInitials(entity.DisplayName);
        var avatar = new Grid
        {
            Width = 28,
            Height = 28,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(avatar, 1);
        avatar.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 28, Height = 28, Fill = avatarBrush });
        avatar.Children.Add(new TextBlock
        {
            Text = initials,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });

        // Account name (with location count when grouped)
        var nameText = entity.Kind == OversightEntityKind.Location
            ? $"{entity.DisplayName}  ({entity.AccountCount})"
            : entity.DisplayName;
        var nameBlock = new TextBlock
        {
            Text = nameText,
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(nameBlock, 2);

        // Awaiting badge (right-aligned)
        var awaitingText = !entity.HasChatData || !hasLiveData
            ? "—"
            : entity.AwaitingCount == 1
                ? "1 awaiting"
                : $"{entity.AwaitingCount} awaiting";
        var awaitingBlock = new TextBlock
        {
            Text = awaitingText,
            Foreground = entity.AwaitingCount > 0 ? danger : secondary,
            FontWeight = entity.AwaitingCount > 0 ? FontWeights.SemiBold : FontWeights.Normal,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        // Trailing cell. In compact density the % hero (which carries the status glyph) is hidden, so the
        // status would be colour-only — add the shape-distinct glyph here so compact stays WCAG 1.4.1 clean.
        FrameworkElement trailingCell = awaitingBlock;
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
            trailing.Children.Add(awaitingBlock);
            trailingCell = trailing;
        }

        Grid.SetColumn(trailingCell, 3);
        topRow.Children.Add(avatar);
        topRow.Children.Add(nameBlock);
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
            Grid.SetColumn(pctCell, 0);
            metricRow.Children.Add(pctCell);
        }

        var sparklineHost = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Spacing = 4 };
        var sparkline = BuildSparkline(entity.TrendCounts, statusBrush);
        ToolTipService.SetToolTip(sparkline, "Activity over the last 7 days");
        sparklineHost.Children.Add(sparkline);

        var freshnessText = entity.IsStale
            ? "stale — reconnect"
            : entity.HistoricalOpenCount > 0
                ? $"synced · {entity.HistoricalOpenCount} from history"
                : "synced";
        sparklineHost.Children.Add(new TextBlock
        {
            Text = freshnessText,
            FontSize = 10,
            Foreground = entity.IsStale ? danger : secondary,
            HorizontalAlignment = HorizontalAlignment.Right
        });
        Grid.SetColumn(sparklineHost, 1);
        metricRow.Children.Add(sparklineHost);
        card.Children.Add(metricRow);

        // ── Sub-metrics row: urgent + dropped + SLA-late ─────────────────────────────────────
        // "late" = open conversations past their business-hours reply SLA (MASTER-PLAN §8 on-time signal),
        // surfaced alongside the caught-up % so responsiveness — not just unread state — is visible.
        if (hasLiveData && (entity.UrgentCount > 0 || entity.DroppedCount > 0 || entity.SlaBreachedCount > 0))
        {
            var caution = Brush("SystemFillColorCautionBrush");
            var subMetrics = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            if (entity.UrgentCount > 0)
            {
                subMetrics.Children.Add(BuildSubMetric(entity.UrgentCount, "urgent", danger));
            }
            if (entity.SlaBreachedCount > 0)
            {
                var late = BuildSubMetric(entity.SlaBreachedCount, "late", caution);
                ToolTipService.SetToolTip(late, "Open conversations past their business-hours reply SLA");
                subMetrics.Children.Add(late);
            }
            if (entity.DroppedCount > 0)
            {
                subMetrics.Children.Add(BuildSubMetric(entity.DroppedCount, "dropped", danger));
            }
            card.Children.Add(subMetrics);
        }

        return card;
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

    private static StackPanel BuildSubMetric(int count, string label, Brush foreground)
    {
        var cell = new StackPanel { Orientation = Orientation.Vertical, Spacing = 0 };
        cell.Children.Add(new TextBlock
        {
            Text = count.ToString(),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = foreground
        });
        cell.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 10,
            Foreground = foreground,
            Opacity = 0.75
        });
        return cell;
    }

    /// <summary>
    /// A compact 7-day bar-chart sparkline. Seven vertical bars, color-matched to the account's
    /// status brush, with rounded tops. Falls back to flat stubs when there is no recent activity.
    /// </summary>
    private static FrameworkElement BuildSparkline(IReadOnlyList<int> counts, Brush fill)
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

    private static Brush Brush(string key) =>
        Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush
            ? brush
            : new SolidColorBrush(Microsoft.UI.Colors.Gray);

    private void OnAttentionJump(object sender, RoutedEventArgs e)
    {
        if (_worstEntityFirstInstanceId is not null)
        {
            _services?.Navigation.OpenInstance(_worstEntityFirstInstanceId, null, null);
        }
    }

    private void OnGroupByAccountClick(object sender, RoutedEventArgs e) => SelectMode(GroupByAccountButton);

    private void OnGroupByLocationClick(object sender, RoutedEventArgs e) => SelectMode(GroupByLocationButton);

    private void OnNeedsReplyClick(object sender, RoutedEventArgs e) => SelectMode(NeedsReplyButton);

    // Segmented control: exactly one of {By account, By location, Needs reply} is active.
    private void SelectMode(ToggleButton active)
    {
        GroupByAccountButton.IsChecked = ReferenceEquals(active, GroupByAccountButton);
        GroupByLocationButton.IsChecked = ReferenceEquals(active, GroupByLocationButton);
        NeedsReplyButton.IsChecked = ReferenceEquals(active, NeedsReplyButton);
        _lastRenderSignature = string.Empty;
        Render();
    }

    private void OnDefineLocations(object sender, RoutedEventArgs e) =>
        _services?.Navigation.RequestOpenSettings(Services.SettingsNavigationHelper.WorkspaceManagementSectionKey);

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
