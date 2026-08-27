using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using UnifiedMessenger.Dialogs;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Pages;

/// <summary>
/// The Reports section — the business report as a first-class page rather than a dialog.
/// </summary>
/// <remarks>
/// The report body is rendered by <see cref="WeeklyReportDialog.Populate"/>, the same builder the dialog
/// uses, so the two surfaces cannot drift apart. The dialog remains the launch-from-dashboard path; this
/// is the browsable one.
/// </remarks>
public sealed partial class ReportsPage : Page
{
    private ApplicationServices _services = ApplicationServiceProvider.Current;
    private BusinessReportResult? _report;
    private bool _suppressRangeChange;

    public ReportsPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        // A report built from a stale scrape has to say so — the numbers in it look identical either way.
        var freshness = DataFreshness.Current();
        FreshnessText.Text = freshness.Text;
        FreshnessText.Foreground = freshness.IsStale
            ? UmSemanticBrushes.Get(UmSemanticBrushes.StatusWarningBrushKey, this)
            : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"];

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

        Rebuild(ResolveSelectedDays());
    }

    private int ResolveSelectedDays() =>
        RangeBox.SelectedItem is ComboBoxItem { Tag: int days }
            ? days
            : DashboardReportHelper.Ranges[0].Days;

    private void Rebuild(int days)
    {
        var instances = _services.Registry.Instances.ToList();
        var inputs = DashboardReportHelper.GatherInputs(instances, days);
        _report = BusinessReport.Build(inputs);

        // The AI-narrated headline is deliberately not requested here: it costs an Ollama round-trip and
        // this page rebuilds on every range change. The deterministic summary is always shown.
        WeeklyReportDialog.Populate(ReportBody, inputs, _report, aiSummary: null, instances);
    }

    private void RangeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressRangeChange)
        {
            return;
        }

        Rebuild(ResolveSelectedDays());
    }

    private async void SaveMarkdownButton_Click(object sender, RoutedEventArgs e)
    {
        if (_report is null)
        {
            return;
        }

        try
        {
            var path = await WeeklyReportDialog.PickSavePathAsync("Business report", "Markdown", ".md");
            if (path is not null)
            {
                await File.WriteAllTextAsync(path, _report.Markdown);
            }
        }
        catch (Exception ex)
        {
            // A save the owner asked for that silently does nothing is worse than an error: they walk away
            // believing the file is there. A full or read-only disk is the common cause.
            await _services.Dialog.ShowErrorAsync("Couldn't save the report", ex.Message);
        }
    }

    private async void ExportCsvButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await WeeklyReportDialog.PickSavePathAsync("Message analytics", "CSV", ".csv");
            if (path is not null)
            {
                await MessageAnalyticsService.Instance.ExportCsvAsync(
                    _services.Registry.Instances.ToList(),
                    path);
            }
        }
        catch (Exception ex)
        {
            await _services.Dialog.ShowErrorAsync("Couldn't export the data", ex.Message);
        }
    }
}
