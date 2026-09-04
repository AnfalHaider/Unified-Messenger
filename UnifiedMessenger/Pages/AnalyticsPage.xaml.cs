using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using UnifiedMessenger.Controls.Charts;
using UnifiedMessenger.Controls.Shared;
using UnifiedMessenger.Dialogs;
using UnifiedMessenger.Models;
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

        PopulateBranchBox();

        ActivityPatternsPanel.ConfigureServices(_services);
        ActivityPatternsPanel.BranchScope = SelectedBranchKey();
        ActivityPatternsPanel.Render();
        Refresh();
    }

    private int SelectedDays() =>
        RangeBox.SelectedItem is ComboBoxItem { Tag: int days } ? days : DashboardReportHelper.Ranges[0].Days;

    private void AddAccountButton_Click(object sender, RoutedEventArgs e) =>
        _services.Navigation.RequestAddInstance();

    /// <summary>The branch selected, or null for "All branches".</summary>
    private string? SelectedBranchKey() =>
        BranchBox.SelectedItem is ComboBoxItem { Tag: string key } && !string.IsNullOrWhiteSpace(key)
            ? key
            : null;

    /// <summary>
    /// The professional accounts this page covers. Every consumer must go through here — the CSV export
    /// used the unfiltered registry, which would write the whole business to a file while the screen showed
    /// one branch.
    /// </summary>
    private List<MessengerInstance> ScopedInstances() =>
        BranchWorkspaceHelper
            .FilterByBranchKey(_services.Registry.Instances.Where(i => i.IsProfessional), SelectedBranchKey())
            .ToList();

    private void PopulateBranchBox()
    {
        var branches = _services.Registry.Instances
            .Where(i => i.IsProfessional)
            .Select(BranchWorkspaceHelper.ResolveBranchKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // One branch is not a choice — showing the filter would imply a scoping that is not happening.
        var worthShowing = branches.Count > 1;
        BranchBox.Visibility = worthShowing ? Visibility.Visible : Visibility.Collapsed;
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

        ActivityPatternsPanel.BranchScope = SelectedBranchKey();
        ActivityPatternsPanel.Render();
        Refresh();
    }

    private void Refresh()
    {
        // PopulateBranchBox is NOT called from here, and must not be. Refresh runs from
        // BranchBox_SelectionChanged, and PopulateBranchBox clears and rebuilds BranchBox.Items — so calling
        // it from here rebuilt a ComboBox from inside its own SelectionChanged handler and the app hung on
        // every branch switch. The _suppressRangeChange flag does not help: Items.Clear() re-enters WinUI's
        // selection machinery regardless of what the handler does with the event.
        //
        // The account list cannot change while this page is on screen, so the box is populated once on
        // navigation, exactly like RangeBox.
        var instances = ScopedInstances();

        // Nothing connected is a different state from nothing happening, and the page had no way to say
        // so — it rendered zeros, which reads as a report on a bad week rather than an empty install.
        var hasAccounts = instances.Count > 0;
        NoAccountsState.Visibility = hasAccounts ? Visibility.Collapsed : Visibility.Visible;
        AnalyticsContent.Visibility = hasAccounts ? Visibility.Visible : Visibility.Collapsed;
        if (!hasAccounts)
        {
            return;
        }

        // Stamped on every refresh, and flagged once it is old enough that the poll has been failing
        // rather than merely lagging.
        // Which of these accounts the figures below are actually about. Same helper the business report
        // uses, so the screen and an exported .md cannot disagree about the same set.
        var scopeLine = ChannelScope.Describe(instances);
        ChannelScopeText.Text = scopeLine;
        ChannelScopeText.Visibility = string.IsNullOrEmpty(scopeLine)
            ? Visibility.Collapsed
            : Visibility.Visible;

        var freshness = DataFreshness.Current();
        FreshnessText.Text = freshness.Text;
        FreshnessText.Foreground = freshness.IsStale
            ? UmSemanticBrushes.Get(UmSemanticBrushes.StatusWarningBrushKey, this)
            : Services.ThemeBrushResolver.Resolve(this, "TextFillColorTertiaryBrush");

        var view = AnalyticsPagePresenter.Build(instances, SelectedDays());

        BindKpi(MessagesKpi, view.Messages);
        BindKpi(ResponseKpi, view.ResponseTime);
        BindKpi(RepliesKpi, view.Replies15);
        BindKpi(SlaKpi, view.SlaMet);

        // Name the SLA tile's own threshold. "Replies (15m)" is a fixed 15-minute figure; "SLA Met" uses
        // whatever target the owner set — which DEFAULTS TO 15, so out of the box these two tiles show the
        // same percentage under different words, side by side, with nothing to say why. Observed live: both
        // read 33%. Worse, an owner who later changes their target sees them silently diverge with no
        // explanation on screen. Putting the minutes in the label makes the sameness legible when they
        // agree and the difference legible when they do not.
        SlaKpi.Label = $"SLA Met ({AppSettingsService.Instance.Settings.SlaThresholdMinutes}m)";

        MessagesChart.SetBars(view.MessagesByDay, MessagesColor, "No messages in this period");
        ResponseChart.SetSeries(view.ResponseDaily, view.ResponseLabels, ResponseColor,
            formatY: BusinessReport.FormatMinutes, emptyHint: "No replies tracked yet");
        RepliesChart.SetSeries(view.Replies15Daily, view.Replies15Labels, RepliesColor,
            formatY: v => $"{(int)v}%", emptyHint: "No replies tracked yet");

        SlaChart.Slices = BuildSlaSlices(view.Sla);
        SlaChart.CentreCaption = view.Sla.HasData ? $"{CaughtUpPercent(view.Sla)}%\ncaught up" : string.Empty;

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
            Background = Services.ThemeBrushResolver.Resolve("CardBackgroundFillColorSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = rank.ToString(CultureInfo.InvariantCulture),
                FontSize = UmScale.Text.Body,
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
            FontSize = UmScale.Text.BodyStrong,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        text.Children.Add(new TextBlock
        {
            Text = detail,
            FontSize = UmScale.Text.Caption,
            Opacity = 0.7,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        Grid.SetColumn(text, 1);

        var percent = new TextBlock
        {
            Text = $"{performer.OnTimePercent}%",
            FontSize = UmScale.Text.Subtitle,
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

    /// <summary>
    /// Share of threads that are caught up, over every thread including the ones we cannot time.
    /// </summary>
    /// <remarks>
    /// <see cref="MetricMath.HonestPercent"/>, not <c>Math.Round</c>: rounding reserved nothing for
    /// "not quite everything", so 99.6% printed as 100% directly above a visible "Behind" slice. That is
    /// the same rounding lie the rest of the app was fixed for; this site was missed because it does its
    /// own arithmetic instead of going through the shared helper.
    /// </remarks>
    internal static int CaughtUpPercent(SlaBreakdown sla) =>
        sla.Total <= 0 ? 0 : MetricMath.HonestPercent(sla.Met, sla.Total);

    /// <summary>
    /// Slice labels say what the numbers ARE — see the comment on the section header in the XAML.
    /// </summary>
    internal static IReadOnlyList<DonutSlice> BuildSlaSlices(SlaBreakdown sla) =>
        ChartSeriesBuilder.BuildShareSlices(
        [
            ("Caught up", "#22C55E", sla.Met),
            ("Behind", "#DC2626", sla.Missed),
            ("Not measured", "#94A3B8", sla.NoSla)
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
                // ScopedInstances, not the whole registry — an export that contradicts the screen it was
                // taken from is worse than no export.
                await MessageAnalyticsService.Instance.ExportCsvAsync(ScopedInstances(), path);
            }
        }
        catch (Exception ex)
        {
            await _services.Dialog.ShowErrorAsync("Couldn't export the data", UserFacingError.Describe("Analytics.Export", ex));
        }
    }
}
