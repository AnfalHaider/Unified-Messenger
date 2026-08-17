using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using UnifiedMessenger.Controls.Charts;
using UnifiedMessenger.Controls.Shared;
using UnifiedMessenger.Dialogs;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Pages;

/// <summary>
/// The Analytics section: a KPI row and a 2×2 chart grid (messages, response time, SLA, replies-in-15m),
/// over the existing <c>ActivityPatternsPanel</c> detail which is kept so no shipped capability is lost.
/// All numbers come from <see cref="AnalyticsPagePresenter"/>; this page only binds them to the controls.
/// </summary>
public sealed partial class AnalyticsPage : Page
{
    private ApplicationServices _services = ApplicationServiceProvider.Current;
    private bool _suppressRangeChange;

    // WhatsApp-family teal for volume; semantic-ish accents for the timing charts.
    private const string MessagesColor = "#1B75BB";
    private const string ResponseColor = "#22C55E";
    private const string RepliesColor = "#7C3AED";

    public AnalyticsPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is RegistryNavigationArgs { Services: { } services })
        {
            _services = services;
        }

        if (RangeBox.Items.Count == 0)
        {
            _suppressRangeChange = true;
            foreach (var range in DashboardReportHelper.Ranges)
            {
                RangeBox.Items.Add(new ComboBoxItem { Content = range.Label, Tag = range.Days });
            }

            RangeBox.SelectedIndex = 0;
            _suppressRangeChange = false;
        }

        ActivityPatternsPanel.ConfigureServices(_services);
        ActivityPatternsPanel.Render();
        Refresh();
    }

    private int SelectedDays() =>
        RangeBox.SelectedItem is ComboBoxItem { Tag: int days } ? days : DashboardReportHelper.Ranges[0].Days;

    private void Refresh()
    {
        var instances = _services.Registry.Instances
            .Where(i => i.IsProfessional)
            .ToList();

        // Stamped on every refresh, and flagged once it is old enough that the poll has been failing
        // rather than merely lagging.
        var freshness = DataFreshness.Current();
        FreshnessText.Text = freshness.Text;
        FreshnessText.Foreground = freshness.IsStale
            ? UmSemanticBrushes.Get(UmSemanticBrushes.StatusWarningBrushKey, this)
            : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"];

        var view = AnalyticsPagePresenter.Build(instances, SelectedDays());

        BindKpi(MessagesKpi, view.Messages);
        BindKpi(ResponseKpi, view.ResponseTime);
        BindKpi(RepliesKpi, view.Replies15);
        BindKpi(SlaKpi, view.SlaMet);

        MessagesChart.SetBars(view.MessagesByDay, MessagesColor, "No messages in this period");
        ResponseChart.SetSeries(view.ResponseDaily, view.ResponseLabels, ResponseColor,
            formatY: BusinessReport.FormatMinutes, emptyHint: "No replies tracked yet");
        RepliesChart.SetSeries(view.Replies15Daily, view.Replies15Labels, RepliesColor,
            formatY: v => $"{(int)v}%", emptyHint: "No replies tracked yet");

        SlaChart.Slices = BuildSlaSlices(view.Sla);
        SlaChart.CentreCaption = view.Sla.HasData ? $"{SlaMetPercent(view.Sla)}%\nSLA met" : string.Empty;

        ShareChart.Slices = view.AccountShare;
        ShareChart.CentreCaption = view.TotalMessages > 0
            ? $"{ChartSeriesBuilder.FormatAxisCount(view.TotalMessages)}\nmessages"
            : string.Empty;
        ShareChart.EmptyHint = "No messages in this period";

        RenderLeaderboard(view.TopPerformers);
    }

    /// <summary>
    /// The account leaderboard. Shows on-time reply % — the real, nameable measurement — rather than a
    /// blended score, and puts backlog beside it instead of inside it, so a row says what it means.
    /// </summary>
    private void RenderLeaderboard(IReadOnlyList<TopPerformer> performers)
    {
        TopAccountsHost.Children.Clear();

        if (performers.Count == 0)
        {
            // Accounts without measured reply data are excluded rather than shown at a flattering 100%,
            // so "nothing here yet" is the honest state early on.
            TopAccountsHost.Children.Add(new EmptyStateView
            {
                IconGlyph = "",
                Title = "No ranked accounts yet",
                Hint = "Accounts appear here once enough replies have been measured to rank them fairly."
            });
            return;
        }

        for (var i = 0; i < performers.Count; i++)
        {
            TopAccountsHost.Children.Add(BuildLeaderboardRow(performers[i], rank: i + 1));
        }
    }

    private static FrameworkElement BuildLeaderboardRow(TopPerformer performer, int rank)
    {
        var row = new Grid { ColumnSpacing = 10, VerticalAlignment = VerticalAlignment.Center };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var rankBadge = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(12),
            Background = Application.Current.Resources.TryGetValue("CardBackgroundFillColorSecondaryBrush", out var bg)
                && bg is Brush brush ? brush : null,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = rank.ToString(CultureInfo.InvariantCulture),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Grid.SetColumn(rankBadge, 0);

        var detail = performer.AwaitingCount > 0
            ? $"{performer.MeasuredCount} replies measured · {performer.AwaitingCount} waiting"
            : $"{performer.MeasuredCount} replies measured";

        var text = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = performer.DisplayName,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        text.Children.Add(new TextBlock
        {
            Text = detail,
            FontSize = 11,
            Opacity = 0.7,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        Grid.SetColumn(text, 1);

        var percent = new TextBlock
        {
            Text = $"{performer.OnTimePercent}%",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = UmSemanticBrushes.Get(performer.OnTimePercent switch
            {
                >= 80 => "UmStatusSuccessBrush",
                >= 50 => "UmStatusWarningBrush",
                _ => "UmStatusDangerBrush"
            })
        };
        Grid.SetColumn(percent, 2);

        row.Children.Add(rankBadge);
        row.Children.Add(text);
        row.Children.Add(percent);

        ToolTipService.SetToolTip(row,
            $"{performer.DisplayName} replied on time to {performer.OnTimePercent}% of {performer.MeasuredCount} measured conversations.");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(row,
            $"Rank {rank}. {performer.DisplayName}, {performer.OnTimePercent}% on time. {detail}");
        return row;
    }

    private static void BindKpi(KpiStatCard card, AnalyticsKpi kpi)
    {
        card.Value = kpi.Value;
        card.Delta = kpi.Delta;
    }

    private static int SlaMetPercent(SlaBreakdown sla) =>
        sla.Total <= 0 ? 0 : (int)Math.Round(sla.Met * 100.0 / sla.Total);

    private static IReadOnlyList<DonutSlice> BuildSlaSlices(SlaBreakdown sla) =>
        ChartSeriesBuilder.BuildShareSlices(
        [
            ("SLA met", "#22C55E", sla.Met),
            ("SLA missed", "#DC2626", sla.Missed),
            ("No SLA", "#94A3B8", sla.NoSla)
        ]);

    private void RangeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_suppressRangeChange)
        {
            Refresh();
        }
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await WeeklyReportDialog.PickSavePathAsync("Message analytics", "CSV", ".csv");
            if (path is not null)
            {
                await MessageAnalyticsService.Instance.ExportCsvAsync(
                    _services.Registry.Instances.ToList(), path);
            }
        }
        catch
        {
            // Best-effort export; a failed save must not take the page down.
        }
    }
}
