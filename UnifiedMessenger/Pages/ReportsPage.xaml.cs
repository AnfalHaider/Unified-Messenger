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
            : Services.ThemeBrushResolver.Resolve(this, "TextFillColorTertiaryBrush");

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

        PopulateBranchBox();
        Rebuild(ResolveSelectedDays());
    }

    private int ResolveSelectedDays() =>
        RangeBox.SelectedItem is ComboBoxItem { Tag: int days }
            ? days
            : DashboardReportHelper.Ranges[0].Days;

    /// <summary>All branches present, or null for "All branches".</summary>
    private string? SelectedBranchKey() =>
        BranchBox.SelectedItem is ComboBoxItem { Tag: string key } && !string.IsNullOrWhiteSpace(key)
            ? key
            : null;

    /// <summary>
    /// The accounts this report covers. Every consumer on this page must go through here — the CSV export
    /// used the unfiltered registry, which would have written the whole business to a file while the screen
    /// showed one branch.
    /// </summary>
    private List<MessengerInstance> ScopedInstances() =>
        BranchWorkspaceHelper.FilterByBranchKey(_services.Registry.Instances, SelectedBranchKey()).ToList();

    private void PopulateBranchBox()
    {
        var branches = _services.Registry.Instances
            .Select(BranchWorkspaceHelper.ResolveBranchKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // One branch is not a choice. Showing a filter whose only option is the thing already on screen is
        // furniture, and it invites the reader to believe the report is scoped when nothing is being scoped.
        var worthShowing = branches.Count > 1;
        BranchBox.Visibility = worthShowing ? Visibility.Visible : Visibility.Collapsed;
        BranchLabel.Visibility = worthShowing ? Visibility.Visible : Visibility.Collapsed;
        if (!worthShowing)
        {
            return;
        }

        var previous = SelectedBranchKey();

        _suppressRangeChange = true;
        BranchBox.Items.Clear();
        BranchBox.Items.Add(new ComboBoxItem { Content = "All branches", Tag = string.Empty });
        foreach (var branch in branches)
        {
            BranchBox.Items.Add(new ComboBoxItem { Content = branch, Tag = branch });
        }

        // Keep the reader's selection across a refresh unless the branch itself has gone.
        var restored = previous is null
            ? 0
            : branches.FindIndex(b => b.Equals(previous, StringComparison.OrdinalIgnoreCase)) + 1;
        BranchBox.SelectedIndex = restored > 0 ? restored : 0;
        _suppressRangeChange = false;
    }

    private void BranchBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressRangeChange)
        {
            return;
        }

        Rebuild(ResolveSelectedDays());
    }

    private void AddAccountButton_Click(object sender, RoutedEventArgs e) =>
        _services.Navigation.RequestAddInstance();

    private void Rebuild(int days)
    {
        var instances = ScopedInstances();

        // Without this the page reached WeeklyReportDialog.Populate, whose only empty branch says
        // "Nothing notable in this period — activity looks steady". That is the no-anomalies message, and
        // telling a brand-new install its activity looks steady is a confident answer to a question the
        // app has no data for.
        var hasAccounts = instances.Count > 0;
        NoAccountsState.Visibility = hasAccounts ? Visibility.Collapsed : Visibility.Visible;
        ReportsContent.Visibility = hasAccounts ? Visibility.Visible : Visibility.Collapsed;
        if (!hasAccounts)
        {
            return;
        }

        var inputs = DashboardReportHelper.GatherInputs(instances, days, SelectedBranchKey());
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
            await _services.Dialog.ShowErrorAsync("Couldn't save the report", UserFacingError.Describe("Reports.Save", ex));
        }
    }

    private async void ExportCsvButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await WeeklyReportDialog.PickSavePathAsync("Message analytics", "CSV", ".csv");
            if (path is not null)
            {
                // ScopedInstances, not the whole registry. Exporting every account while the page shows one
                // branch would write a file that silently contradicts the screen it was exported from.
                await MessageAnalyticsService.Instance.ExportCsvAsync(ScopedInstances(), path);
            }
        }
        catch (Exception ex)
        {
            await _services.Dialog.ShowErrorAsync("Couldn't export the data", UserFacingError.Describe("Reports.Export", ex));
        }
    }
}
