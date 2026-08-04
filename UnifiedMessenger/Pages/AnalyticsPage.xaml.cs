using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using UnifiedMessenger.Controls.Charts;
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
