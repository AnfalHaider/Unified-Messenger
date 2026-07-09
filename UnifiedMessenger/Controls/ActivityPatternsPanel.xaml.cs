using System.Text;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

/// <summary>
/// One filterable activity-patterns graph: inbound (customer) message volume bucketed by hour of day,
/// day of week, or month, across all professional accounts or one, over a chosen date range. Reads the
/// on-device activity history persisted by <see cref="MessageAnalyticsService"/> — fully local, no cloud.
/// </summary>
public sealed partial class ActivityPatternsPanel : UserControl
{
    private const double ChartHeight = 190;
    private const double BarMaxHeight = 165;

    private ApplicationServices? _services;
    private ActivityDimension _dimension = ActivityDimension.HourOfDay;
    private bool _populatingAccounts;

    public ActivityPatternsPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        // Redraw when the theme resolves/changes so the code-drawn legend + axis brushes are re-picked for the
        // right theme (they render once, so without this they can stay stuck on the first-render theme).
        ActualThemeChanged += (_, _) => Render();
    }

    public void ConfigureServices(ApplicationServices services) => _services = services;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_services is null && ApplicationServiceProvider.IsInitialized)
        {
            _services = ApplicationServiceProvider.Current;
        }

        PopulateAccounts();
        Render();
    }

    private void PopulateAccounts()
    {
        if (_services is null)
        {
            return;
        }

        _populatingAccounts = true;
        AccountSelector.Items.Clear();
        AccountSelector.Items.Add(new ComboBoxItem { Content = "All accounts", Tag = string.Empty });
        foreach (var instance in ProfessionalInstances())
        {
            AccountSelector.Items.Add(new ComboBoxItem { Content = instance.DisplayName, Tag = instance.Id });
        }

        AccountSelector.SelectedIndex = 0;
        _populatingAccounts = false;
    }

    private IEnumerable<MessengerInstance> ProfessionalInstances() =>
        _services?.Registry.Instances
            .Where(i => i.IsProfessional && PlatformModuleSettingsHelper.IsPlatformModuleEnabled(i.Platform))
        ?? [];

    private bool _heatmapMode;

    private void OnHourClick(object sender, RoutedEventArgs e) => SelectDimension(ActivityDimension.HourOfDay);

    private void OnDayClick(object sender, RoutedEventArgs e) => SelectDimension(ActivityDimension.DayOfWeek);

    private void OnMonthClick(object sender, RoutedEventArgs e) => SelectDimension(ActivityDimension.Month);

    private void OnHeatmapClick(object sender, RoutedEventArgs e)
    {
        _heatmapMode = true;
        HourButton.IsChecked = false;
        DayButton.IsChecked = false;
        MonthButton.IsChecked = false;
        HeatmapButton.IsChecked = true;
        Render();
    }

    private void SelectDimension(ActivityDimension dimension)
    {
        _dimension = dimension;
        _heatmapMode = false;
        HourButton.IsChecked = dimension == ActivityDimension.HourOfDay;
        DayButton.IsChecked = dimension == ActivityDimension.DayOfWeek;
        MonthButton.IsChecked = dimension == ActivityDimension.Month;
        HeatmapButton.IsChecked = false;
        Render();
    }

    private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_populatingAccounts)
        {
            Render();
        }
    }

    private IReadOnlyList<MessengerInstance> ResolveInstances()
    {
        var selectedId = (AccountSelector.SelectedItem as ComboBoxItem)?.Tag as string;
        if (!string.IsNullOrEmpty(selectedId))
        {
            return ProfessionalInstances().Where(i => i.Id == selectedId).ToList();
        }

        return ProfessionalInstances().ToList();
    }

    private (DateTimeOffset? From, DateTimeOffset? To) ResolveRange()
    {
        var now = DateTimeOffset.Now;
        return ((RangeSelector.SelectedItem as ComboBoxItem)?.Tag as string) switch
        {
            "30" => (now.AddDays(-30), (DateTimeOffset?)null),
            "year" => (new DateTimeOffset(new DateTime(now.Year, 1, 1), now.Offset), null),
            "all" => (null, null),
            _ => (now.AddDays(-90), null)
        };
    }

    /// <summary>Re-queries the history log for the current filters and redraws the bars + insight.</summary>
    public void Render()
    {
        if (_services is null)
        {
            return;
        }

        var instances = ResolveInstances();
        var (from, to) = ResolveRange();

        if (_heatmapMode)
        {
            RenderHeatmap(instances, from, to);
            return;
        }

        ChartArea.Visibility = Visibility.Visible;
        HeatmapHost.Visibility = Visibility.Collapsed;

        var breakdown = MessageAnalyticsService.Instance.BuildActivityBreakdown(_dimension, instances, from, to);

        ChartHost.Children.Clear();
        ChartHost.ColumnDefinitions.Clear();
        AxisHost.Children.Clear();
        AxisHost.ColumnDefinitions.Clear();
        LegendHost.ItemsSource = null;

        var count = breakdown.Labels.Count;
        if (count == 0 || !breakdown.HasData)
        {
            ChartHost.Children.Add(new Controls.Shared.EmptyStateView
            {
                IconGlyph = "", // Report / bar chart
                Title = "No activity yet for this selection",
                Hint = "Patterns build up as customer messages arrive. If you just updated, click Re-sync.",
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            InsightText.Text = "Once a few days of activity accrue, the busiest hour, day, and month surface here.";
            RenderWeekOverWeek(instances);
            return;
        }

        // Colour each account's contribution: with a single visible series, one accent + a highlighted peak;
        // with several, stack each account's segment in a distinct colour so you can see who drives each bar.
        // (Same-platform accounts share one brand accent, so ChartPalette assigns distinct hues when needed.)
        var multiSeries = breakdown.Series.Count > 1;
        var seriesColors = ChartPalette.ResolveSeriesColors(breakdown.Series);
        var labelBrush = Brush("TextFillColorTertiaryBrush");
        var peakLabelBrush = Brush("SystemFillColorCautionBrush");
        var singleFill = Brush("AccentFillColorDefaultBrush");
        var peakFill = Brush("SystemFillColorCautionBrush");
        var max = Math.Max(1, breakdown.Totals.Max());

        for (var i = 0; i < count; i++)
        {
            ChartHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            AxisHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var isPeak = i == breakdown.PeakIndex;
            var total = breakdown.Totals[i];
            var fullHeight = Math.Max(total > 0 ? 3 : 0, total / (double)max * BarMaxHeight);

            var column = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(3, 0, 3, 0) };

            // Value label above the peak bucket.
            if (isPeak && total > 0)
            {
                column.Children.Add(new TextBlock
                {
                    Text = total.ToString(),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = peakLabelBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 2)
                });
            }

            // Per-bucket tooltip: the total plus each account's share.
            var tip = new StringBuilder();
            tip.Append(breakdown.Labels[i]).Append(" · ").Append(total).Append(total == 1 ? " message" : " messages");

            if (total > 0)
            {
                if (multiSeries)
                {
                    var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
                    var segments = breakdown.Series
                        .Select((s, idx) => (Series: s, Value: s.Values[i], Index: idx))
                        .Where(x => x.Value > 0)
                        .ToList();
                    for (var s = 0; s < segments.Count; s++)
                    {
                        var (series, value, _) = segments[s];
                        var segHeight = Math.Max(2, value / (double)total * fullHeight);
                        var colorHex = seriesColors.TryGetValue(series.InstanceId, out var hex) ? hex : series.AccentColor;
                        stack.Children.Add(new Border
                        {
                            Height = segHeight,
                            Background = new SolidColorBrush(PlatformBrandingHelper.ParseAccentColor(colorHex)),
                            // Round only the very top segment.
                            CornerRadius = s == 0 ? new CornerRadius(3, 3, 0, 0) : new CornerRadius(0),
                            HorizontalAlignment = HorizontalAlignment.Stretch
                        });
                        tip.Append('\n').Append(series.DisplayName).Append(": ").Append(value);
                    }

                    column.Children.Add(stack);
                }
                else
                {
                    column.Children.Add(new Border
                    {
                        Height = fullHeight,
                        Background = isPeak ? peakFill : singleFill,
                        Opacity = isPeak ? 1.0 : 0.55,
                        CornerRadius = new CornerRadius(3, 3, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    });
                }
            }

            ToolTipService.SetToolTip(column, tip.ToString());
            Grid.SetColumn(column, i);
            ChartHost.Children.Add(column);

            // Axis labels: show all for ≤12 buckets; thin to every 3rd for the 24-hour view.
            var showLabel = count <= 12 || i % 3 == 0 || isPeak;
            var axisLabel = new TextBlock
            {
                Text = showLabel ? breakdown.Labels[i] : string.Empty,
                FontSize = 10,
                Foreground = isPeak ? peakLabelBrush : labelBrush,
                FontWeight = isPeak ? FontWeights.SemiBold : FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(axisLabel, i);
            AxisHost.Children.Add(axisLabel);
        }

        RenderLegend(multiSeries ? breakdown.Series : [], seriesColors);

        var prep = _dimension switch
        {
            ActivityDimension.HourOfDay => "around",
            ActivityDimension.DayOfWeek => "on",
            _ => "in"
        };
        var rangeWord = (RangeSelector.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "the selected range";
        InsightText.Text =
            $"{breakdown.Total} messages in {rangeWord.ToLowerInvariant()} · busiest {prep} {breakdown.PeakLabel} " +
            $"({breakdown.Totals[breakdown.PeakIndex]} at the peak).";

        RenderWeekOverWeek(instances);
    }

    /// <summary>Renders the per-account colour legend (empty list hides it — single-account or no data).</summary>
    private void RenderLegend(
        IReadOnlyList<ActivityAccountSeries> series,
        IReadOnlyDictionary<string, string> seriesColors)
    {
        if (series.Count == 0)
        {
            LegendHost.ItemsSource = null;
            return;
        }

        var chips = series.Select(s =>
        {
            var colorHex = seriesColors.TryGetValue(s.InstanceId, out var hex) ? hex : s.AccentColor;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            row.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse
            {
                Width = 9,
                Height = 9,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = new SolidColorBrush(PlatformBrandingHelper.ParseAccentColor(colorHex))
            });
            row.Children.Add(new TextBlock
            {
                Text = $"{s.DisplayName} ({s.Total})",
                FontSize = 11,
                Foreground = Brush("TextFillColorSecondaryBrush"),
                VerticalAlignment = VerticalAlignment.Center
            });
            return (FrameworkElement)row;
        }).ToList();

        LegendHost.ItemsSource = chips;
    }

    private static readonly string[] HeatmapDayLabels = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

    /// <summary>
    /// Renders the weekday × hour "busiest times" heatmap for staffing/coverage planning: 7 rows × 24 cells
    /// coloured by relative volume, with an hour axis and a plain-language coverage nudge in the insight line.
    /// </summary>
    private void RenderHeatmap(IReadOnlyList<MessengerInstance> instances, DateTimeOffset? from, DateTimeOffset? to)
    {
        ChartArea.Visibility = Visibility.Collapsed;
        HeatmapHost.Visibility = Visibility.Visible;
        HeatmapHost.Children.Clear();
        HeatmapHost.ColumnDefinitions.Clear();
        HeatmapHost.RowDefinitions.Clear();

        var map = MessageAnalyticsService.Instance.BuildWeekHourHeatmap(instances, from, to);
        if (!map.HasData)
        {
            HeatmapHost.Children.Add(new TextBlock
            {
                Text = "No activity yet for this selection — the heat map builds up as messages arrive. If you just updated, click Re-sync.",
                Foreground = Brush("TextFillColorSecondaryBrush"),
                TextWrapping = TextWrapping.WrapWholeWords,
                Margin = new Thickness(0, 8, 0, 0)
            });
            InsightText.Text = "Once a week or two of activity accrues, your busiest weekday-and-hour windows surface here for planning coverage.";
            RenderWeekOverWeek(instances);
            return;
        }

        var accentColor = (Brush("AccentFillColorDefaultBrush") as SolidColorBrush)?.Color ?? Microsoft.UI.Colors.SteelBlue;
        var labelBrush = Brush("TextFillColorTertiaryBrush");
        var max = Math.Max(1, map.PeakValue);

        // Column 0 = day label; columns 1..24 = hours. Row 0 = hour axis; rows 1..7 = weekdays.
        HeatmapHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
        for (var h = 0; h < 24; h++)
        {
            HeatmapHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        HeatmapHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var d = 0; d < 7; d++)
        {
            HeatmapHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
        }

        // Hour axis (every 3 hours to avoid crowding).
        for (var h = 0; h < 24; h += 3)
        {
            var lbl = new TextBlock
            {
                Text = h == 0 ? "12a" : h < 12 ? $"{h}a" : h == 12 ? "12p" : $"{h - 12}p",
                FontSize = 9,
                Foreground = labelBrush,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetRow(lbl, 0);
            Grid.SetColumn(lbl, h + 1);
            HeatmapHost.Children.Add(lbl);
        }

        for (var d = 0; d < 7; d++)
        {
            var dayLabel = new TextBlock
            {
                Text = HeatmapDayLabels[d],
                FontSize = 10,
                Foreground = labelBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(dayLabel, d + 1);
            Grid.SetColumn(dayLabel, 0);
            HeatmapHost.Children.Add(dayLabel);

            for (var h = 0; h < 24; h++)
            {
                var value = map.Grid[d][h];
                var intensity = value / (double)max; // 0..1
                var cell = new Border
                {
                    Margin = new Thickness(1),
                    CornerRadius = new CornerRadius(2),
                    Background = value == 0
                        ? Brush("ControlFillColorSecondaryBrush")
                        : new SolidColorBrush(Windows.UI.Color.FromArgb(
                            (byte)(38 + intensity * 217), accentColor.R, accentColor.G, accentColor.B))
                };
                var hourLabel = h == 0 ? "12 AM" : h < 12 ? $"{h} AM" : h == 12 ? "12 PM" : $"{h - 12} PM";
                ToolTipService.SetToolTip(cell, $"{HeatmapDayLabels[d]} {hourLabel} · {value} message{(value == 1 ? "" : "s")}");
                Grid.SetRow(cell, d + 1);
                Grid.SetColumn(cell, h + 1);
                HeatmapHost.Children.Add(cell);
            }
        }

        var blockEnd = map.BusiestBlockStartHour + 3;
        string Fmt(int h) => h == 0 || h == 24 ? "12 AM" : h < 12 ? $"{h} AM" : h == 12 ? "12 PM" : $"{h - 12} PM";
        InsightText.Text =
            $"Busiest on {map.BusiestDayName}, {Fmt(map.BusiestBlockStartHour)}–{Fmt(blockEnd)} — that window carries " +
            $"{map.BusiestBlockSharePercent}% of the week's messages. Make sure it's covered.";

        RenderWeekOverWeek(instances);
    }

    /// <summary>#37: a plain-language week-over-week trend line (this week vs last, busiest day).</summary>
    private void RenderWeekOverWeek(IReadOnlyList<MessengerInstance> instances)
    {
        var wow = MessageAnalyticsService.Instance.GetWeekOverWeek(instances);
        if (!wow.HasData)
        {
            WeekRow.Visibility = Visibility.Collapsed;
            return;
        }

        string trend;
        if (wow.LastWeekTotal == 0)
        {
            trend = "first week of activity";
        }
        else if (wow.DeltaPercent == 0)
        {
            trend = "about the same as last week";
        }
        else
        {
            var dir = wow.DeltaPercent > 0 ? "up" : "down";
            trend = $"{dir} {Math.Abs(wow.DeltaPercent)}% vs last week";
        }

        var busiest = string.IsNullOrEmpty(wow.BusiestDay) ? string.Empty : $" · busiest on {wow.BusiestDay}";
        WeekText.Text = $"This week: {wow.ThisWeekTotal} messages, {trend}{busiest}.";
        WeekRow.Visibility = Visibility.Visible;
    }

    // Instance (not static) so neutral text brushes resolve from THIS control's actual theme — otherwise the
    // legend/axis labels can render invisibly in light mode. See ThemeBrushResolver.
    private Brush Brush(string key) => Services.ThemeBrushResolver.Resolve(this, key);
}
