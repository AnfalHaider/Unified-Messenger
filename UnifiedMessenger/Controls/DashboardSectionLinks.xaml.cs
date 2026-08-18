using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

/// <summary>
/// The dashboard's footer row: one card per section that owns a subject the dashboard deliberately does
/// <b>not</b> render — Analytics, Reviews and Reports.
/// </summary>
/// <remarks>
/// These replaced in-place copies of the analytics charts, the activity graph and the review-health panel.
/// Those panels each live on their own page now, and a dashboard that repeats them is two things at once:
/// a second place to maintain, and a second place to disagree with the first. A card carries only the one
/// number that answers "is it worth opening?", then links there — a pointer, not a duplicate.
/// </remarks>
public sealed partial class DashboardSectionLinks : UserControl
{
    private ApplicationServices _services = ApplicationServiceProvider.Current;

    public DashboardSectionLinks()
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
        LinksHost.Children.Clear();

        Add(0, ShellSection.Analytics, "", "Analytics", MessagesSummary(),
            "Message volume, response time, SLA and activity patterns.");
        Add(1, ShellSection.Reviews, "", "Reviews", ReviewsSummary(),
            "Google Business review health and which reviews still need a reply.");
        Add(2, ShellSection.Reports, "", "Reports", "Weekly business report",
            "A plain-language report with anomaly detection, for any recent period.");
    }

    private string MessagesSummary()
    {
        var instances = _services.Registry.Instances.Where(i => i.IsProfessional).ToList();
        if (instances.Count == 0)
        {
            return "No professional accounts yet";
        }

        var now = DateTimeOffset.Now;
        var total = MessageAnalyticsService.Instance
            .BuildActivityPatterns(ActivityDimension.DayOfWeek, instances, now.AddDays(-7), now)
            .Total;

        return total > 0
            ? $"{ChartSeriesBuilder.FormatAxisCount(total)} messages · last 7 days"
            : "No messages in the last 7 days";
    }

    private string ReviewsSummary()
    {
        var googleAccounts = _services.Registry.Instances
            .Where(i => PlatformDefinition.NormalizePlatformId(i.Platform)
                .Equals("googlebusiness", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (googleAccounts.Count == 0)
        {
            return "No Google Business account connected";
        }

        var unanswered = 0;
        var anyData = false;
        foreach (var account in googleAccounts)
        {
            var health = GoogleReviewSnapshotService.Instance.Get(account.Id);
            if (!health.HasData)
            {
                continue;
            }

            anyData = true;
            unanswered += health.Unanswered;
        }

        if (!anyData)
        {
            return "Not scanned yet";
        }

        return unanswered == 0
            ? "All reviews answered"
            : $"{unanswered} review{(unanswered == 1 ? string.Empty : "s")} need a reply";
    }

    private void Add(int column, ShellSection section, string glyph, string title, string summary, string tooltip)
    {
        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = UmScale.Text.BodyStrong,
            FontWeight = FontWeights.SemiBold
        });
        text.Children.Add(new TextBlock
        {
            Text = summary,
            FontSize = UmScale.Text.Body,
            Opacity = 0.7,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new FontIcon { Glyph = glyph, FontSize = UmScale.Icon.Md, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(icon, 0);
        Grid.SetColumn(text, 1);

        var chevron = new FontIcon
        {
            Glyph = "",
            FontSize = UmScale.Icon.Sm,
            Opacity = 0.5,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(chevron, 2);

        row.Children.Add(icon);
        row.Children.Add(text);
        row.Children.Add(chevron);

        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(14, 12, 14, 12),
            CornerRadius = new CornerRadius(8),
            Content = row
        };
        ToolTipService.SetToolTip(button, tooltip);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, $"Open {title}. {summary}");
        button.Click += (_, _) => _services.Navigation.RequestSection(section);

        Grid.SetColumn(button, column);
        LinksHost.Children.Add(button);
    }
}
