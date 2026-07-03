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

    private void OnHourClick(object sender, RoutedEventArgs e) => SelectDimension(ActivityDimension.HourOfDay);

    private void OnDayClick(object sender, RoutedEventArgs e) => SelectDimension(ActivityDimension.DayOfWeek);

    private void OnMonthClick(object sender, RoutedEventArgs e) => SelectDimension(ActivityDimension.Month);

    private void SelectDimension(ActivityDimension dimension)
    {
        _dimension = dimension;
        HourButton.IsChecked = dimension == ActivityDimension.HourOfDay;
        DayButton.IsChecked = dimension == ActivityDimension.DayOfWeek;
        MonthButton.IsChecked = dimension == ActivityDimension.Month;
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
        var breakdown = MessageAnalyticsService.Instance.BuildActivityBreakdown(_dimension, instances, from, to);

        ChartHost.Children.Clear();
        ChartHost.ColumnDefinitions.Clear();
        AxisHost.Children.Clear();
        AxisHost.ColumnDefinitions.Clear();
        LegendHost.ItemsSource = null;

        var count = breakdown.Labels.Count;
        if (count == 0 || !breakdown.HasData)
        {
            ChartHost.Children.Add(new TextBlock
            {
                Text = "No activity recorded yet for this selection — patterns build up as messages arrive. If you just updated, click Re-sync.",
                Foreground = Brush("TextFillColorSecondaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.WrapWholeWords
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

    private static Brush Brush(string key) =>
        Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush
            ? brush
            : new SolidColorBrush(Microsoft.UI.Colors.Gray);
}
