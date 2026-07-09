using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

/// <summary>
/// Dashboard "Reviews" section: per Google Business account, how many reviews are awaiting a reply and the
/// reply rate on the loaded reviews page. Data is scraped on demand (the "Refresh reviews" button) from each
/// account's live session via <see cref="GoogleReviewSnapshotService"/> — Google exposes no aggregate rating
/// or total count on the manager reviews page, so only the actionable reply metrics are shown.
/// </summary>
public sealed partial class ReviewHealthPanel : UserControl
{
    private ApplicationServices? _services;
    private bool _refreshing;

    public ReviewHealthPanel()
    {
        InitializeComponent();
        Loaded += (_, _) => Render();
        // Redraw on theme resolve/change so the code-drawn secondary/tertiary text is re-picked per theme.
        ActualThemeChanged += (_, _) => Render();
    }

    public void ConfigureServices(ApplicationServices services) => _services = services;

    private IEnumerable<MessengerInstance> GoogleInstances() =>
        // NOTE: do NOT gate on IsPlatformModuleEnabled — that is WhatsApp-family-only, so it would exclude
        // Google Business (an embed channel). Google accounts are sidebar-visible embed channels.
        _services?.Registry.Instances.Where(i =>
            i.IsProfessional &&
            PlatformModuleSettingsHelper.IsSidebarVisible(i.Platform) &&
            string.Equals(PlatformDefinition.NormalizePlatformId(i.Platform), "googlebusiness", StringComparison.OrdinalIgnoreCase))
        ?? [];

    public void Render()
    {
        if (_services is null)
        {
            return;
        }

        var accounts = GoogleInstances().ToList();
        if (accounts.Count == 0)
        {
            // No Google Business accounts — nothing to oversee here.
            Visibility = Visibility.Collapsed;
            return;
        }

        Visibility = Visibility.Visible;
        CardsHost.Children.Clear();

        var anyData = false;
        foreach (var instance in accounts)
        {
            var health = GoogleReviewSnapshotService.Instance.Get(instance.Id);
            anyData |= health.HasData;
            CardsHost.Children.Add(BuildCard(instance, health));
        }

        var captured = GoogleReviewSnapshotService.Instance.LastCapturedUtc;
        if (anyData && captured is { } cap)
        {
            UpdatedText.Text = $"Updated {RelativeAge(cap)}";
            UpdatedText.Visibility = Visibility.Visible;
        }
        else
        {
            UpdatedText.Visibility = Visibility.Collapsed;
        }
    }

    private FrameworkElement BuildCard(MessengerInstance instance, GoogleReviewSnapshotService.ReviewHealth health)
    {
        var secondary = Brush("TextFillColorSecondaryBrush");
        var danger = Brush("SystemFillColorCriticalBrush");
        var success = Brush("SystemFillColorSuccessBrush");

        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var avatar = ProfileAvatarService.CreateAvatar(instance, 30);
        avatar.Margin = new Thickness(0, 0, 10, 0);
        avatar.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(avatar, 0);
        grid.Children.Add(avatar);

        var nameCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
        Grid.SetColumn(nameCol, 1);
        nameCol.Children.Add(new TextBlock
        {
            Text = instance.DisplayName,
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        nameCol.Children.Add(new TextBlock
        {
            Text = health.HasData
                ? $"{health.ReplyRatePercent}% replied · {health.Total} reviewed (this page)"
                : "Open this account, then Refresh reviews to load.",
            FontSize = 11,
            Foreground = Brush("TextFillColorTertiaryBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        grid.Children.Add(nameCol);

        var pending = health.Pending ?? [];

        FrameworkElement trailing;
        if (!health.HasData)
        {
            trailing = new TextBlock { Text = "—", Foreground = secondary, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        }
        else if (health.Unanswered > 0)
        {
            // Clickable: opens the account's Google reviews page so the owner can reply straight away.
            var chip = new Button
            {
                Background = Brush("SystemFillColorCriticalBackgroundBrush"),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(9, 3, 9, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Tag = instance.Id,
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 5,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = health.Unanswered == 1 ? "1 to reply" : $"{health.Unanswered} to reply",
                            Foreground = danger,
                            FontWeight = FontWeights.SemiBold,
                            FontSize = 12
                        },
                        new FontIcon { Glyph = "", FontSize = 11, Foreground = danger, VerticalAlignment = VerticalAlignment.Center }
                    }
                }
            };
            ToolTipService.SetToolTip(chip, "Open this account's Google reviews to reply");
            chip.Click += OnOpenReviewsClick;
            trailing = chip;
        }
        else
        {
            trailing = new TextBlock
            {
                Text = "all replied",
                Foreground = success,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        Grid.SetColumn(trailing, 2);
        grid.Children.Add(trailing);

        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(grid);

        // "Which ones": list the awaiting reviews (reviewer + snippet), each a click-through to reply.
        if (pending.Count > 0)
        {
            var list = new StackPanel { Spacing = 4, Margin = new Thickness(40, 2, 0, 0) };
            foreach (var review in pending.Take(6))
            {
                var row = new StackPanel { Spacing = 0 };
                row.Children.Add(new TextBlock
                {
                    Text = review.Reviewer,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                if (!string.IsNullOrWhiteSpace(review.Snippet))
                {
                    row.Children.Add(new TextBlock
                    {
                        Text = review.Snippet,
                        FontSize = 11,
                        Foreground = secondary,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        TextWrapping = TextWrapping.NoWrap
                    });
                }

                var rowButton = new Button
                {
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(8, 5, 8, 5),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Tag = instance.Id,
                    Content = row
                };
                ToolTipService.SetToolTip(rowButton, "Open this account's reviews to reply");
                rowButton.Click += OnOpenReviewsClick;
                list.Children.Add(rowButton);
            }

            if (health.Unanswered > pending.Count)
            {
                list.Children.Add(new TextBlock
                {
                    Text = $"+ {health.Unanswered - pending.Count} more awaiting a reply",
                    FontSize = 11,
                    Foreground = Brush("TextFillColorTertiaryBrush"),
                    Margin = new Thickness(8, 2, 0, 0)
                });
            }

            body.Children.Add(list);
        }
        else if (health is { HasData: true, Unanswered: > 0 })
        {
            body.Children.Add(new TextBlock
            {
                Text = "Couldn't read the individual reviews from the page — click “to reply” to open them in Google.",
                FontSize = 11,
                Foreground = Brush("TextFillColorTertiaryBrush"),
                Margin = new Thickness(40, 0, 0, 0),
                TextWrapping = TextWrapping.WrapWholeWords
            });
        }

        return new Border
        {
            Background = Brush("CardBackgroundFillColorSecondaryBrush"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 14, 10),
            Child = body
        };
    }

    private void OnOpenReviewsClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string instanceId } && !string.IsNullOrWhiteSpace(instanceId))
        {
            _services?.Navigation.OpenInstance(instanceId, null, null);
        }
    }

    /// <summary>
    /// Scrapes review health from each Google account's live session and re-renders. Public so the dashboard's
    /// single Re-sync button drives it (the per-section Refresh button was removed).
    /// </summary>
    public async Task RefreshAsync()
    {
        if (_services is null || _refreshing)
        {
            return;
        }

        var accounts = GoogleInstances().ToList();
        if (accounts.Count == 0)
        {
            return;
        }

        _refreshing = true;
        try
        {
            foreach (var instance in accounts)
            {
                await GoogleReviewSnapshotService.Instance.ScrapeAsync(instance.Id);
                Render();
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    private static string RelativeAge(DateTimeOffset whenUtc)
    {
        var span = DateTimeOffset.UtcNow - whenUtc;
        if (span < TimeSpan.Zero)
        {
            span = TimeSpan.Zero;
        }

        if (span.TotalMinutes < 1)
        {
            return "just now";
        }
        if (span.TotalMinutes < 60)
        {
            var m = (int)Math.Round(span.TotalMinutes);
            return m == 1 ? "1 min ago" : $"{m} min ago";
        }
        if (span.TotalHours < 24)
        {
            var h = (int)Math.Round(span.TotalHours);
            return h == 1 ? "1 hr ago" : $"{h} hrs ago";
        }

        var d = (int)Math.Round(span.TotalDays);
        return d == 1 ? "1 day ago" : $"{d} days ago";
    }

    // Instance (not static) so neutral text brushes resolve from THIS control's actual theme — otherwise the
    // secondary/tertiary labels can render invisibly in light mode. See ThemeBrushResolver.
    private Brush Brush(string key) => Services.ThemeBrushResolver.Resolve(this, key);
}
