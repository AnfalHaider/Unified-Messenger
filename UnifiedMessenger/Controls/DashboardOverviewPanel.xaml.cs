using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Controls.Shared;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

/// <summary>
/// The dashboard's overview row from the mockups: message volume over time, the top-performing accounts
/// leaderboard, and message distribution per account. Additive — it sits alongside the command center
/// rather than replacing any of it, so no shipped capability is traded for the new layout.
/// </summary>
public sealed partial class DashboardOverviewPanel : UserControl
{
    private ApplicationServices _services = ApplicationServiceProvider.Current;
    private const string AccentHex = "#1B75BB";

    public DashboardOverviewPanel()
    {
        InitializeComponent();
        Loaded += (_, _) => Render();
    }

    public void ConfigureServices(ApplicationServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    public void Render()
    {
        var instances = _services.Registry.Instances.Where(i => i.IsProfessional).ToList();
        var view = DashboardOverviewPresenter.Build(instances);

        OverviewTotal.Text = view.TotalMessages > 0
            ? $"{ChartSeriesBuilder.FormatAxisCount(view.TotalMessages)} messages · last 7 days"
            : string.Empty;

        MessagesChart.SetBars(view.MessagesByDay, AccentHex, "No messages in the last 7 days");
        ShareChart.Slices = view.ChannelShare;
        ShareChart.CentreCaption = view.TotalMessages > 0
            ? $"{ChartSeriesBuilder.FormatAxisCount(view.TotalMessages)}\nmessages"
            : string.Empty;
        ShareChart.EmptyHint = "No messages in the last 7 days";

        RenderTopAccounts(view.TopPerformers);
    }

    private void RenderTopAccounts(IReadOnlyList<TopPerformer> performers)
    {
        TopAccountsHost.Children.Clear();

        if (performers.Count == 0)
        {
            // Deliberately explicit: accounts without measured reply data are excluded from ranking rather
            // than shown at a flattering 100%, so "nothing here yet" is the honest state early on.
            TopAccountsHost.Children.Add(new EmptyStateView
            {
                IconGlyph = "\uE9D2",
                Title = "No ranked accounts yet",
                Hint = "Accounts appear here once enough replies have been measured to score them fairly."
            });
            return;
        }

        foreach (var p in performers)
        {
            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var text = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock
            {
                Text = p.DisplayName,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            text.Children.Add(new TextBlock
            {
                Text = $"{p.OnTimePercent}% on time · {p.MeasuredCount} measured"
                       + (p.AwaitingCount > 0 ? $" · {p.AwaitingCount} waiting" : string.Empty),
                FontSize = 11,
                Opacity = 0.7,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            Grid.SetColumn(text, 0);

            var score = new TextBlock
            {
                Text = $"{p.Score}%",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = UmSemanticBrushes.Get(
                    p.Score >= 80 ? "UmStatusSuccessBrush" : p.Score >= 50 ? "UmStatusWarningBrush" : "UmStatusDangerBrush")
            };
            Grid.SetColumn(score, 1);

            ToolTipService.SetToolTip(row,
                $"Score {p.Score} = on-time % minus a capped backlog penalty. Based on {p.MeasuredCount} measured replies.");

            row.Children.Add(text);
            row.Children.Add(score);
            TopAccountsHost.Children.Add(row);
        }
    }
}
