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
    private DispatcherTimer? _autoRefreshTimer;

    /// <summary>
    /// Reviews used to re-scrape ONLY on a manual Re-sync, so a review you had already replied to kept
    /// showing as "pending" until you remembered to Re-sync. Now we re-scrape whenever the dashboard opens
    /// and every few minutes while it's on screen. Reviews change slowly, so this cadence is plenty — and the
    /// refresh is passive (allowNavigate:false), so it can never pull the owner off the page they're reading.
    /// </summary>
    private static readonly TimeSpan AutoRefreshInterval = TimeSpan.FromMinutes(5);

    public ReviewHealthPanel()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Render();
            StartAutoRefresh();
        };
        Unloaded += (_, _) => _autoRefreshTimer?.Stop();
        // Redraw on theme resolve/change so the code-drawn secondary/tertiary text is re-picked per theme.
        ActualThemeChanged += (_, _) => Render();
    }

    private void StartAutoRefresh()
    {
        // One straight away — the owner has often just replied to a review and come back to the dashboard.
        _ = RefreshAsync(allowNavigate: false);

        _autoRefreshTimer ??= new DispatcherTimer { Interval = AutoRefreshInterval };
        _autoRefreshTimer.Tick -= OnAutoRefreshTick;
        _autoRefreshTimer.Tick += OnAutoRefreshTick;
        _autoRefreshTimer.Start();
    }

    private void OnAutoRefreshTick(object? sender, object e) => _ = RefreshAsync(allowNavigate: false);

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
            Text = BuildSubtitle(instance.Id, health),
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
                var row = new StackPanel { Spacing = 1 };

                var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                head.Children.Add(new TextBlock
                {
                    Text = review.Reviewer,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                var meta = ReviewMeta(review);
                if (!string.IsNullOrEmpty(meta))
                {
                    head.Children.Add(new TextBlock
                    {
                        Text = meta,
                        FontSize = 11,
                        Foreground = secondary,
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }
                row.Children.Add(head);

                if (!string.IsNullOrWhiteSpace(review.Text))
                {
                    // Wraps to 3 lines rather than the old single clipped line — enough to judge a review
                    // without opening it. The tooltip carries the whole thing.
                    row.Children.Add(new TextBlock
                    {
                        Text = review.Text,
                        FontSize = 11,
                        Foreground = secondary,
                        TextWrapping = TextWrapping.Wrap,
                        MaxLines = 3,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    });
                }

                var rowButton = new Button
                {
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(8, 5, 8, 5),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Tag = (instance.Id, review.Reviewer, review.Index),
                    Content = row
                };
                ToolTipService.SetToolTip(
                    rowButton,
                    new ToolTip
                    {
                        Content = string.IsNullOrWhiteSpace(review.Text)
                            ? "Open this review in Google"
                            : review.Text + "\n\nClick to open this review in Google",
                        MaxWidth = 460
                    });
                rowButton.Click += OnOpenReviewClick;
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

    /// <summary>"★★★☆☆ · 2 days ago" — either half is omitted when the page didn't yield it.</summary>
    private static string ReviewMeta(GoogleReviewSnapshotService.PendingReview review)
    {
        var parts = new List<string>(2);
        if (review.Stars is >= 1 and <= 5)
        {
            parts.Add(new string('★', review.Stars) + new string('☆', 5 - review.Stars));
        }

        if (!string.IsNullOrWhiteSpace(review.Age))
        {
            parts.Add(review.Age);
        }

        return string.Join(" · ", parts);
    }

    // Opens the account and scrolls straight to that review, highlighted. Google gives reviews no individual
    // URL, so this finds the card on the page instead of deep-linking. If it can't (list never renders), the
    // account is still open on its reviews page — no error, just a manual scroll.
    private async void OnOpenReviewClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ValueTuple<string, string, int> tag } ||
            string.IsNullOrWhiteSpace(tag.Item1))
        {
            return;
        }

        _services?.Navigation.OpenInstance(tag.Item1, null, null);
        try
        {
            await GoogleReviewSnapshotService.Instance.FocusReviewAsync(tag.Item1, tag.Item2, tag.Item3);
        }
        catch
        {
            // Best-effort — the account is open regardless.
        }
    }

    /// <summary>
    /// Scrapes review health from each Google account's live session and re-renders. Public so the dashboard's
    /// single Re-sync button drives it (the per-section Refresh button was removed).
    /// </summary>
    public async Task RefreshAsync(bool allowNavigate = true)
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
                // Manual Re-sync only: the official rating + lifetime total live on the Google Search merchant
                // view, not the reviews manager, so grab them first (this navigates there via
                // business.google.com's redirect). Throttled to 6h internally. The reviews scrape below then
                // navigates back to /reviews. The passive background refresh skips this entirely.
                if (allowNavigate)
                {
                    await GoogleReviewSnapshotService.Instance.ScrapeRatingAsync(instance.Id);
                }

                await GoogleReviewSnapshotService.Instance.ScrapeAsync(instance.Id, allowNavigate);
                Render();
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>
    /// e.g. "4.6 ★ · 239 reviews · 100% replied (50 on this page)". The rating and lifetime total come from the
    /// Google Search merchant view — the only surface that carries them. The replied % is still computed from
    /// the loaded reviews page, so it stays explicitly labelled rather than implying it covers every review.
    /// </summary>
    private static string BuildSubtitle(string instanceId, GoogleReviewSnapshotService.ReviewHealth health)
    {
        if (!health.HasData)
        {
            return "Open this account, then Re-sync to load.";
        }

        var parts = new List<string>();
        if (GoogleReviewSnapshotService.Instance.GetRating(instanceId) is { } r)
        {
            if (!string.IsNullOrWhiteSpace(r.Rating))
            {
                parts.Add($"{r.Rating} ★");
            }

            if (r.Total is { } total)
            {
                parts.Add($"{total} reviews");
            }
        }

        parts.Add($"{health.ReplyRatePercent}% replied ({health.Total} on this page)");
        return string.Join(" · ", parts);
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
