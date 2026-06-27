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
        var patterns = MessageAnalyticsService.Instance.BuildActivityPatterns(_dimension, instances, from, to);

        ChartHost.Children.Clear();
        ChartHost.ColumnDefinitions.Clear();
        AxisHost.Children.Clear();
        AxisHost.ColumnDefinitions.Clear();

        var count = patterns.Labels.Count;
        if (count == 0 || !patterns.HasData)
        {
            ChartHost.Children.Add(new TextBlock
            {
                Text = "No activity recorded yet for this selection — patterns build up as messages arrive.",
                Foreground = Brush("TextFillColorSecondaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.WrapWholeWords
            });
            InsightText.Text = "Once a few days of activity accrue, the busiest hour, day, and month surface here.";
            return;
        }

        var max = Math.Max(1, patterns.Values.Max());
        var barFill = Brush("AccentFillColorDefaultBrush");
        var peakFill = Brush("SystemFillColorCautionBrush");
        var labelBrush = Brush("TextFillColorTertiaryBrush");
        var peakLabelBrush = Brush("SystemFillColorCautionBrush");

        for (var i = 0; i < count; i++)
        {
            ChartHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            AxisHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var isPeak = i == patterns.PeakIndex;
            var value = patterns.Values[i];
            var barHeight = Math.Max(3, value / (double)max * BarMaxHeight);

            var column = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            if (isPeak)
            {
                column.Children.Add(new TextBlock
                {
                    Text = value.ToString(),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = peakLabelBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 2)
                });
            }

            column.Children.Add(new Border
            {
                Height = barHeight,
                Background = isPeak ? peakFill : barFill,
                Opacity = isPeak ? 1.0 : 0.45,
                CornerRadius = new CornerRadius(3, 3, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(3, 0, 3, 0)
            });

            Grid.SetColumn(column, i);
            ChartHost.Children.Add(column);

            // Axis labels: show all for ≤12 buckets; thin to every 3rd for the 24-hour view.
            var showLabel = count <= 12 || i % 3 == 0 || isPeak;
            var axisLabel = new TextBlock
            {
                Text = showLabel ? patterns.Labels[i] : string.Empty,
                FontSize = 10,
                Foreground = isPeak ? peakLabelBrush : labelBrush,
                FontWeight = isPeak ? FontWeights.SemiBold : FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.None
            };
            Grid.SetColumn(axisLabel, i);
            AxisHost.Children.Add(axisLabel);
        }

        var prep = _dimension switch
        {
            ActivityDimension.HourOfDay => "around",
            ActivityDimension.DayOfWeek => "on",
            _ => "in"
        };
        InsightText.Text = $"Busiest {prep} {patterns.PeakLabel} — {patterns.Values[patterns.PeakIndex]} messages at the peak.";
    }

    private static Brush Brush(string key) =>
        Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush
            ? brush
            : new SolidColorBrush(Microsoft.UI.Colors.Gray);
}
