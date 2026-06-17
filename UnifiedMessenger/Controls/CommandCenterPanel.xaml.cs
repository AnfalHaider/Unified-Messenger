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

    private OversightWindow SelectedWindow() =>
        ((WindowSelector?.SelectedItem as ComboBoxItem)?.Tag as string) switch
        {
            "Week" => OversightWindow.Week,
            "All" => OversightWindow.All,
            _ => OversightWindow.Today
        };

    private void OnWindowChanged(object sender, SelectionChangedEventArgs e) => Render();

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
            ? $"Rolled up by location · on-time for {windowLabel}"
            : $"Per account · on-time for {windowLabel} · group into locations (Ctrl+K)";
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

    private Border BuildRow(OversightEntityHealth entity)
    {
        var clickable = entity.Kind == OversightEntityKind.Instance;
        var border = new Border
        {
            Background = Brush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = Brush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(0.5),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 11, 14, 11),
            Child = BuildRowContent(entity, clickable)
        };

        if (clickable)
        {
            border.Tapped += (_, _) => OpenAccount(entity.Key);
        }

        return border;
    }

    /// <summary>
    /// Drill-down: open the account focused on its worst-waiting open conversation (most overdue),
    /// so the user lands directly on the customer who has waited longest. Falls back to just opening
    /// the account when there is no open thread.
    /// </summary>
    private void OpenAccount(string instanceId)
    {
        if (_services is null || string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        var worst = _services.ThreadRegistry.GetAllThreads()
            .Where(thread =>
                string.Equals(thread.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase)
                && !thread.IsReplied
                && !thread.IsSpamOrPromo)
            .OrderByDescending(thread => thread.IsSlaBreached)
            .ThenByDescending(thread => thread.LatencyMinutes)
            .FirstOrDefault();

        if (worst is not null && !string.IsNullOrWhiteSpace(worst.ConversationKey))
        {
            _services.Navigation.OpenInstance(instanceId, worst.ConversationKey, worst.CustomerName);
        }
        else
        {
            _services.Navigation.OpenInstance(instanceId);
        }
    }

    private StackPanel BuildRowContent(OversightEntityHealth entity, bool clickable = false)
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

        row.Children.Add(new TextBlock
        {
            Text = hasLiveData ? $"{entity.OnTimePercent}% on time" : $"no activity {_emptyStateWindowLabel}",
            Foreground = statusBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 110
        });

        row.Children.Add(new TextBlock
        {
            Text = $"{entity.UrgentCount} urgent",
            Foreground = entity.UrgentCount > 0 ? danger : secondary,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 80
        });

        row.Children.Add(new TextBlock
        {
            Text = $"{entity.DroppedCount} dropped",
            Foreground = entity.DroppedCount > 0 ? danger : secondary,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 90
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

        if (clickable)
        {
            row.Children.Add(new FontIcon
            {
                Glyph = "",
                FontSize = 13,
                Foreground = secondary,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

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
        AttentionText.Text = "Re-syncing history from each account…";

        foreach (var instance in pros)
        {
            BackfillSyncManager.Instance.Schedule(instance, force: true);
        }

        var ids = pros.Select(p => p.Id).ToList();
        for (var i = 0; i < 60; i++) // up to ~30s
        {
            await Task.Delay(500);
            var allDone = ids.All(id => BackfillSyncManager.Instance.GetState(id)
                is BackfillSyncState.Completed or BackfillSyncState.Failed or BackfillSyncState.Skipped);
            if (allDone)
            {
                break;
            }
        }

        int found = 0, answered = 0, migrated = 0;
        string? firstDiag = null;
        foreach (var id in ids)
        {
            var result = BackfillSyncManager.Instance.GetLastResult(id);
            if (result is null)
            {
                continue;
            }

            found += result.DbConversationsFound;
            answered += result.AnsweredReconciled;
            migrated += result.KeysMigrated;
            firstDiag ??= result.DbDiagnostic;
        }

        Render();

        // Set the banner AFTER Render so the diagnostic isn't overwritten by the needs-attention summary.
        AttentionBanner.Visibility = Visibility.Visible;
        var summary =
            $"Re-sync done · {found} conversations read · {answered} marked answered · {migrated} keys migrated across {pros.Count} account(s).";
        if (found == 0 && !string.IsNullOrWhiteSpace(firstDiag))
        {
            summary += $"  [{firstDiag}]";
        }

        AttentionText.Text = summary;
        ResyncButton.IsEnabled = true;
        _resyncInProgress = false;
    }
}
