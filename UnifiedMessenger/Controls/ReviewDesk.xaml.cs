using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Ai;
using Windows.System;

namespace UnifiedMessenger.Controls;

/// <summary>
/// The Reviews surface: what to answer first, across every location.
/// </summary>
/// <remarks>
/// <para>
/// Laid out to <c>docs/design/review-desk-spec.md</c>. It replaced the old ReviewHealthPanel on this page (deleted in v4.99.47)
/// rather than sitting above it — the page was showing a queue and then repeating the same information as
/// per-account cards underneath.
/// </para>
/// <para>
/// <b>The rule this control exists to hold.</b> Several tiles in the approved design need data the app
/// cannot yet compute — rating history, monthly velocity, reply times. Each of those renders a stated gap,
/// never a plausible number. A fabricated median reply time is worse than an empty one, because the owner
/// cannot tell it is fabricated, and the whole brief is "no wrong numbers".
/// </para>
/// </remarks>
public sealed partial class ReviewDesk : UserControl
{
    private ApplicationServices? _services;
    private IReadOnlyList<QueuedReview> _queue = [];
    private ReviewUrgency? _filter;
    private int _selected;
    private bool _refreshing;
    private DispatcherTimer? _autoRefreshTimer;

    private readonly List<Button> _rows = [];

    /// <summary>
    /// Reviews change slowly, but the owner is often on this page immediately after replying to one. The
    /// 30-minute background pass is the safety net; this is the "I am looking at it" cadence.
    /// </summary>
    private static readonly TimeSpan AutoRefreshInterval = TimeSpan.FromMinutes(5);

    /// <summary>Rows shown before the list is truncated. The remainder is stated, not implied.</summary>
    private const int MaxRows = 12;

    public ReviewDesk()
    {
        InitializeComponent();
        QueueHost.KeyDown += OnQueueKeyDown;
        CheckNowButton.Click += async (_, _) => await RefreshAsync(allowNavigate: true);
        ActualThemeChanged += (_, _) => Render();

        Loaded += (_, _) =>
        {
            Render();
            StartAutoRefresh();
        };
        Unloaded += (_, _) => _autoRefreshTimer?.Stop();
    }

    public void ConfigureServices(ApplicationServices services) => _services = services;

    private void StartAutoRefresh()
    {
        _ = RefreshAsync(allowNavigate: false);

        _autoRefreshTimer ??= new DispatcherTimer { Interval = AutoRefreshInterval };
        _autoRefreshTimer.Tick -= OnAutoRefreshTick;
        _autoRefreshTimer.Tick += OnAutoRefreshTick;
        _autoRefreshTimer.Start();
    }

    private void OnAutoRefreshTick(object? sender, object e) => _ = RefreshAsync(allowNavigate: false);

    /// <summary>
    /// Re-reads every Google account, then redraws.
    /// </summary>
    /// <param name="allowNavigate">
    /// True only for the owner-driven "Check now", which is also the only path allowed to bypass the
    /// service's freshness floor — a refresh button that answers with a cached number is a broken button.
    /// </param>
    public async Task RefreshAsync(bool allowNavigate = true)
    {
        if (_services is null || _refreshing)
        {
            return;
        }

        var accounts = GoogleAccounts().ToList();
        if (accounts.Count == 0)
        {
            Render();
            return;
        }

        _refreshing = true;
        CheckNowButton.IsEnabled = false;
        try
        {
            foreach (var instance in accounts)
            {
                if (allowNavigate)
                {
                    // Rating and lifetime total live on the Search merchant view, not the reviews manager,
                    // so this navigates away and the reviews scrape below navigates back.
                    await GoogleReviewSnapshotService.Instance.ScrapeRatingAsync(instance.Id);
                }

                await GoogleReviewSnapshotService.Instance.ScrapeAsync(
                    instance.Id, allowNavigate, force: allowNavigate);
                Render();
            }
        }
        finally
        {
            _refreshing = false;
            CheckNowButton.IsEnabled = true;
        }
    }

    public void Render()
    {
        if (_services is null)
        {
            return;
        }

        var accounts = GoogleAccounts().ToList();
        var snapshots = accounts
            .Select(instance => (
                Instance: instance,
                Name: string.IsNullOrWhiteSpace(instance.DisplayName) ? "Google Business" : instance.DisplayName,
                Health: GoogleReviewSnapshotService.Instance.Get(instance.Id),
                Rating: EffectiveRating(instance.Id)))
            .ToList();

        _queue = ReviewQueue.Build(snapshots.Select(s =>
            (s.Instance.Id, s.Name, (GoogleReviewSnapshotService.ReviewHealth?)s.Health)));

        var anyRead = snapshots.Any(s => s.Health.HasData);

        RenderHeader(snapshots, anyRead);
        RenderHero(snapshots, anyRead);
        RenderAlert();
        RenderKpis(snapshots, anyRead);
        RenderThemes();
        RenderFilters();
        RenderQueue(accounts.Count, anyRead);
        RenderAskPanel();
        RenderBranches(snapshots);
    }

    /// <summary>
    /// The account's rating and lifetime total: this session's scrape if it has run, otherwise the last
    /// reading on disk.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The scrape's own cache lives only in memory, and the rating is read at most every six hours, so for
    /// the first minutes of every run the hero showed "—" for figures the app had known perfectly well the
    /// day before. Persisting the readings only fixes that if something actually falls back to them.
    /// </para>
    /// <para>
    /// Safe to substitute because these are slow-moving profile facts — a lifetime total and a rating to one
    /// decimal — not live counts. The queue deliberately does NOT fall back this way: it needs the reviews
    /// themselves, and history stores only the numbers.
    /// </para>
    /// </remarks>
    private static GoogleReviewSnapshotService.ProfileRating? EffectiveRating(string instanceId)
    {
        if (GoogleReviewSnapshotService.Instance.GetRating(instanceId) is { Total: not null } live)
        {
            return live;
        }

        var stored = ReviewHistoryStore.Instance.GetHistory(instanceId)
            .LastOrDefault(point => point is { Rating: not null, LifetimeTotal: > 0 });

        return stored is { Rating: { } rating, LifetimeTotal: { } total }
            ? new GoogleReviewSnapshotService.ProfileRating(
                rating.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
                total,
                new DateTimeOffset(stored.Day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero))
            : null;
    }

    private IEnumerable<MessengerInstance> GoogleAccounts() =>
        _services is null
            ? []
            : _services.Registry.Instances.Where(instance =>
                string.Equals(
                    PlatformDefinition.NormalizePlatformId(instance.Platform),
                    "googlebusiness",
                    StringComparison.OrdinalIgnoreCase));

    // ---- header ---------------------------------------------------------------------------------------

    private void RenderHeader(
        IReadOnlyList<(MessengerInstance Instance, string Name, GoogleReviewSnapshotService.ReviewHealth Health,
            GoogleReviewSnapshotService.ProfileRating? Rating)> snapshots,
        bool anyRead)
    {
        var parts = new List<string>
        {
            snapshots.Count == 1 ? "1 Google location" : $"{snapshots.Count} Google locations"
        };

        if (GoogleReviewSnapshotService.Instance.LastCapturedUtc is { } captured)
        {
            parts.Add($"checked {RelativeAge(captured)}");
        }

        // The design's header said "covers all 239 reviews". It cannot say "all" while the scrape reads one
        // page of 50 per account — that wording is the exact false-completeness this surface must avoid.
        var loaded = snapshots.Sum(s => s.Health.HasData ? s.Health.Total : 0);
        var lifetime = SumLifetime(snapshots);
        if (anyRead)
        {
            parts.Add(ReviewCoverage.Describe(loaded, lifetime));
        }

        HeaderSub.Text = string.Join(" · ", parts);
    }

    private static int? SumLifetime(
        IReadOnlyList<(MessengerInstance Instance, string Name, GoogleReviewSnapshotService.ReviewHealth Health,
            GoogleReviewSnapshotService.ProfileRating? Rating)> snapshots)
    {
        // Only meaningful if EVERY location reported one; summing the two we know and calling it the total
        // would understate the business.
        if (snapshots.Count == 0 || snapshots.Any(s => s.Rating?.Total is null))
        {
            return null;
        }

        return snapshots.Sum(s => s.Rating!.Value.Total!.Value);
    }

    /// <summary>
    /// Sub-line for the Unanswered tile, naming the set the low-star count was computed over.
    /// </summary>
    internal static string LowStarSub(int lowStars, int shown, int unanswered)
    {
        var suffix = ReviewCoverage.DescribeQueueSample(shown, unanswered);
        var body = lowStars > 0 ? $"{lowStars} at 3 stars or below" : "none at 3 stars or below";
        return suffix.Length > 0 ? $"{body} {suffix}" : body;
    }

    /// <summary>
    /// How many reviews are actually awaiting a reply, across every location.
    /// </summary>
    /// <remarks>
    /// This — not <c>_queue.Count</c> — is the number to show. The queue only holds the reviews the scrape
    /// built preview text for (the first handful per page), while <c>Health.Unanswered</c> is the full
    /// reply-button count the same pass recorded. The sidebar badge has always used this one, so rendering
    /// the queue length here made the badge and the page contradict each other on screen.
    /// </remarks>
    private static int SumUnanswered(
        IReadOnlyList<(MessengerInstance Instance, string Name, GoogleReviewSnapshotService.ReviewHealth Health,
            GoogleReviewSnapshotService.ProfileRating? Rating)> snapshots) =>
        snapshots.Sum(s => s.Health.HasData ? s.Health.Unanswered : 0);

    // ---- hero -----------------------------------------------------------------------------------------

    private void RenderHero(
        IReadOnlyList<(MessengerInstance Instance, string Name, GoogleReviewSnapshotService.ReviewHealth Health,
            GoogleReviewSnapshotService.ProfileRating? Rating)> snapshots,
        bool anyRead)
    {
        HeroHost.Children.Clear();

        // --- rating across all locations ---
        var ratingBlock = new StackPanel { Spacing = UmScale.Space.Xs };
        var average = WeightedRating(snapshots);

        ratingBlock.Children.Add(new TextBlock
        {
            Text = average?.ToString("0.0") ?? "—",
            FontSize = UmScale.Text.Hero,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        ratingBlock.Children.Add(new TextBlock
        {
            Text = average is { } a ? StarsFor(a) : "☆☆☆☆☆",
            FontSize = UmScale.Text.Body,
            Foreground = Brush("UmStatusWarningBrush")
        });

        var lifetime = SumLifetime(snapshots);
        ratingBlock.Children.Add(new TextBlock
        {
            // Google publishes a rating per location, never one for the business. This is a mean weighted by
            // each location's review count, and it says so — an unlabelled aggregate would read as Google's
            // own figure and disagree with every profile page the owner checks it against.
            Text = lifetime is { } total
                ? $"{total:N0} reviews · weighted across {snapshots.Count} locations"
                : "all locations",
            FontSize = UmScale.Text.Caption,
            Foreground = Brush("TextFillColorTertiaryBrush")
        });
        HeroHost.Children.Add(ratingBlock);

        // --- trend: not yet knowable ---
        var trend = new StackPanel
        {
            Spacing = UmScale.Space.Xs,
            Margin = new Thickness(UmScale.Space.Lg, 0, 0, 0),
            BorderBrush = Brush("UmHairlineBrush"),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Padding = new Thickness(UmScale.Space.Lg, 0, 0, 0)
        };
        var ids = snapshots.Select(s => s.Instance.Id).ToList();
        var combined = ReviewHistoryStore.Instance.GetCombinedHistory(ids);
        var ratingChange = ReviewTrend.RatingChange(combined, 180);

        trend.Children.Add(Label(ratingChange is null ? "Rating trend" : "Rating"));

        if (ratingChange is { } change)
        {
            var delta = change.To - change.From;
            trend.Children.Add(new TextBlock
            {
                Text = $"{change.From:0.0} → {change.To:0.0}",
                FontSize = UmScale.Text.Body,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            trend.Children.Add(new TextBlock
            {
                // Rounded to one decimal because that is the precision Google publishes; showing "up 0.04"
                // from a figure given as 4.6 would be inventing significance.
                Text = Math.Abs(delta) < 0.05
                    ? $"steady {ReviewTrend.SpanLabel(change.OverDays)}"
                    : $"{(delta > 0 ? "up" : "down")} {Math.Abs(delta):0.0} {ReviewTrend.SpanLabel(change.OverDays)}",
                FontSize = UmScale.Text.Caption,
                Foreground = Math.Abs(delta) < 0.05
                    ? Brush("TextFillColorTertiaryBrush")
                    : delta > 0 ? Brush("UmStatusSuccessBrush") : Brush("UmStatusDangerBrush")
            });
        }
        else
        {
            trend.Children.Add(new TextBlock
            {
                // A flat line drawn from one reading would be a claim about stability that one measurement
                // cannot support, so the panel says what it is waiting for instead.
                Text = HistoryPending(combined),
                FontSize = UmScale.Text.Caption,
                Foreground = Brush("TextFillColorTertiaryBrush"),
                TextWrapping = TextWrapping.WrapWholeWords,
                MaxWidth = 190
            });
        }
        Grid.SetColumn(trend, 1);
        HeroHost.Children.Add(trend);

        // --- needs a reply ---
        var unanswered = SumUnanswered(snapshots);
        var waiting = new StackPanel { Spacing = UmScale.Space.Xs, HorizontalAlignment = HorizontalAlignment.Right };
        waiting.Children.Add(Label("Needs a reply", HorizontalAlignment.Right));
        waiting.Children.Add(new TextBlock
        {
            Text = anyRead ? unanswered.ToString() : "—",
            FontSize = UmScale.Text.Hero,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Right,
            Foreground = unanswered > 0 ? Brush("UmStatusDangerBrush") : Brush("TextFillColorPrimaryBrush")
        });
        waiting.Children.Add(new TextBlock
        {
            // "Oldest waiting" is computed over the queue, and the queue is the newest slice of the
            // backlog — so when it is a sample, the oldest review in it is nowhere near the oldest the
            // business has. Say which it is rather than quietly presenting one as the other.
            Text = ReviewCoverage.QueueIsSample(_queue.Count, unanswered)
                ? $"showing the {_queue.Count:N0} most recent"
                : OldestWaitingLabel() is { } oldest ? $"oldest waiting {oldest}" : "nothing waiting",
            FontSize = UmScale.Text.Caption,
            Foreground = Brush("TextFillColorTertiaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Right
        });
        Grid.SetColumn(waiting, 3);
        HeroHost.Children.Add(waiting);
    }

    /// <summary>
    /// The business-wide rating: a mean of each location's rating weighted by its review count.
    /// </summary>
    /// <remarks>
    /// Null unless every location has both a rating and a total. A partial average would silently describe
    /// a different business from the one the owner runs.
    /// </remarks>
    private static double? WeightedRating(
        IReadOnlyList<(MessengerInstance Instance, string Name, GoogleReviewSnapshotService.ReviewHealth Health,
            GoogleReviewSnapshotService.ProfileRating? Rating)> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return null;
        }

        double weighted = 0;
        var totalWeight = 0;

        foreach (var snapshot in snapshots)
        {
            if (snapshot.Rating is not { Total: { } count } rating ||
                !double.TryParse(rating.Rating, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var value) ||
                count <= 0)
            {
                return null;
            }

            weighted += value * count;
            totalWeight += count;
        }

        return totalWeight > 0 ? weighted / totalWeight : null;
    }

    /// <summary>
    /// Star glyphs for a rating, with a half star rather than a rounded-up whole one.
    /// </summary>
    /// <remarks>
    /// This rounded away from zero, so anything from 4.5 to 4.99 drew five filled stars directly beneath
    /// the number "4.6" — the glyphs contradicting the figure they illustrate, on the one screen whose
    /// subject is what customers think of the business. Five filled stars is a claim of perfection and
    /// belongs to 5.0 alone.
    /// </remarks>
    internal static string StarsFor(double rating)
    {
        var clamped = Math.Clamp(rating, 0, 5);
        var full = (int)Math.Floor(clamped);
        var half = clamped - full >= 0.25 && full < 5;

        return new string('★', full)
             + (half ? "⯨" : string.Empty)
             + new string('☆', 5 - full - (half ? 1 : 0));
    }

    private string? OldestWaitingLabel()
    {
        if (_queue.Count == 0)
        {
            return null;
        }

        var oldest = _queue
            .Select(review => ReviewAge.SortKey(review.Age))
            .Where(span => span > TimeSpan.MinValue)
            .DefaultIfEmpty(TimeSpan.MinValue)
            .Max();

        return oldest == TimeSpan.MinValue
            ? null
            : ReviewAge.ShortLabel(_queue.First(r => ReviewAge.SortKey(r.Age) == oldest).Age);
    }

    // ---- critical strip -------------------------------------------------------------------------------

    private void RenderAlert()
    {
        var worst = _queue.FirstOrDefault(review => review.Urgency == ReviewUrgency.Critical);
        if (worst.InstanceId is null or "")
        {
            AlertStrip.Visibility = Visibility.Collapsed;
            AlertStrip.Child = null;
            return;
        }

        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = UmScale.Space.Xs, VerticalAlignment = VerticalAlignment.Center };
        var age = ReviewAge.ShortLabel(worst.Age);
        text.Children.Add(new TextBlock
        {
            Text = $"★ {worst.Stars} · {(string.IsNullOrWhiteSpace(worst.Reviewer) ? "A customer" : worst.Reviewer)} " +
                   $"left a {(worst.Stars == 1 ? "one" : "two")}-star review" +
                   (string.IsNullOrWhiteSpace(age) ? string.Empty : $" {age} ago"),
            FontSize = UmScale.Text.Body,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Brush("UmStatusDangerBrush"),
            TextWrapping = TextWrapping.WrapWholeWords
        });
        text.Children.Add(new TextBlock
        {
            Text = $"{worst.AccountName} · still unanswered",
            FontSize = UmScale.Text.Caption,
            Foreground = Brush("TextFillColorSecondaryBrush")
        });
        layout.Children.Add(text);

        var open = new Button
        {
            Content = "Open it",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(UmScale.Space.Md, 0, 0, 0)
        };
        open.Click += async (_, _) => await OpenAsync(worst);
        Grid.SetColumn(open, 1);
        layout.Children.Add(open);

        AlertStrip.Child = layout;
        AlertStrip.Visibility = Visibility.Visible;
    }

    // ---- KPI strip ------------------------------------------------------------------------------------

    private void RenderKpis(
        IReadOnlyList<(MessengerInstance Instance, string Name, GoogleReviewSnapshotService.ReviewHealth Health,
            GoogleReviewSnapshotService.ProfileRating? Rating)> snapshots,
        bool anyRead)
    {
        KpiHost.Children.Clear();
        KpiHost.ColumnDefinitions.Clear();

        var lowStars = _queue.Count(r => r.Urgency is ReviewUrgency.Critical or ReviewUrgency.Elevated);
        var answered = snapshots.Sum(s => s.Health.HasData ? s.Health.Answered : 0);
        var loaded = snapshots.Sum(s => s.Health.HasData ? s.Health.Total : 0);
        var replyRate = loaded > 0 ? MetricMath.HonestPercent(answered, loaded) : 0;
        var unanswered = SumUnanswered(snapshots);
        var sampleSuffix = ReviewCoverage.DescribeQueueSample(_queue.Count, unanswered);

        var ids = snapshots.Select(s => s.Instance.Id).ToList();
        var combined = ReviewHistoryStore.Instance.GetCombinedHistory(ids);

        var gained = ReviewTrend.ReviewsGained(combined, 30);
        var quietest = QuietestBranch(snapshots);

        var tiles = new List<(string Label, string Value, string Sub, bool Known)>
        {
            // The value is the real reply-button count; lowStars is only ever computed over the queue, so
            // when the queue is a sample the sub-line has to name what it counted.
            ("Unanswered",
                anyRead ? unanswered.ToString() : "—",
                LowStarSub(lowStars, _queue.Count, unanswered),
                anyRead),

            ("Oldest waiting",
                OldestWaitingLabel() ?? "—",
                sampleSuffix.Length > 0
                    ? $"{OldestBranch() ?? "oldest"} · {sampleSuffix}"
                    : OldestBranch() ?? "nothing waiting",
                anyRead),

            ("Reply rate",
                anyRead ? $"{replyRate}%" : "—",
                ReplyRateSub(combined, answered, loaded, anyRead),
                anyRead),

            // Velocity comes from the lifetime total's increase between two readings, not from per-review
            // dates the scrape never sees. The span is always the one actually measured — "+2 in 4 days"
            // rather than a figure that reads as a month's worth.
            gained is { } g
                ? ("New reviews", $"+{Math.Max(0, g.To - g.From)}", ReviewTrend.SpanLabel(g.OverDays), true)
                : ("New reviews", "—", HistoryPending(combined), false),

            // Still genuinely unobtainable: nothing on the page says when a reply was posted.
            ("Median reply time", "—", "Google doesn't publish reply dates.", false),

            quietest is { } q
                ? ("Quietest branch", q.Name, $"no new review in {q.Days} days", true)
                : ("Quietest branch", "—", HistoryPending(combined), false)
        };

        for (var i = 0; i < tiles.Count; i++)
        {
            KpiHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var tile = BuildKpi(tiles[i]);
            Grid.SetColumn(tile, i);
            KpiHost.Children.Add(tile);
        }
    }

    /// <summary>
    /// How the page says "not yet" — with how far off it is, so it reads as progress rather than breakage.
    /// </summary>
    private static string HistoryPending(IReadOnlyList<ReviewDayPoint> combined) => combined.Count switch
    {
        0 => "Starts building today.",
        1 => "One day recorded — needs a second.",
        _ => $"{combined.Count} days recorded so far."
    };

    private static string ReplyRateSub(
        IReadOnlyList<ReviewDayPoint> combined, int answered, int loaded, bool anyRead)
    {
        if (!anyRead)
        {
            return "not read yet";
        }

        var basis = $"{answered:N0} of {loaded:N0} read";
        return ReviewTrend.ReplyRateChange(combined, 30) is { } change && change.To != change.From
            ? $"{basis} · {(change.To > change.From ? "+" : "")}{change.To - change.From} {ReviewTrend.SpanLabel(change.OverDays)}"
            : basis;
    }

    /// <summary>
    /// The location that has gone longest without a new review.
    /// </summary>
    /// <remarks>
    /// Only reported once at least one location has two readings to compare; a branch cannot be called quiet
    /// on the strength of a single measurement.
    /// </remarks>
    private static (string Name, int Days)? QuietestBranch(
        IReadOnlyList<(MessengerInstance Instance, string Name, GoogleReviewSnapshotService.ReviewHealth Health,
            GoogleReviewSnapshotService.ProfileRating? Rating)> snapshots)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        (string Name, int Days)? worst = null;

        foreach (var snapshot in snapshots)
        {
            var history = ReviewHistoryStore.Instance.GetHistory(snapshot.Instance.Id);
            if (ReviewTrend.DaysSinceNewReview(history, today) is not { } days)
            {
                continue;
            }

            if (worst is null || days > worst.Value.Days)
            {
                worst = (snapshot.Name, days);
            }
        }

        return worst;
    }

    private string? OldestBranch()
    {
        if (_queue.Count == 0)
        {
            return null;
        }

        var oldest = _queue
            .Select(review => ReviewAge.SortKey(review.Age))
            .Where(span => span > TimeSpan.MinValue)
            .DefaultIfEmpty(TimeSpan.MinValue)
            .Max();

        return oldest == TimeSpan.MinValue
            ? null
            : _queue.First(r => ReviewAge.SortKey(r.Age) == oldest).AccountName;
    }

    private Border BuildKpi((string Label, string Value, string Sub, bool Known) tile)
    {
        var body = new StackPanel { Spacing = UmScale.Space.Xs };
        body.Children.Add(Label(tile.Label));
        body.Children.Add(new TextBlock
        {
            Text = tile.Value,
            FontSize = UmScale.Text.Metric,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = tile.Known ? Brush("TextFillColorPrimaryBrush") : Brush("TextFillColorTertiaryBrush")
        });
        body.Children.Add(new TextBlock
        {
            Text = tile.Sub,
            FontSize = UmScale.Text.Caption,
            Foreground = Brush("TextFillColorTertiaryBrush"),
            TextWrapping = TextWrapping.WrapWholeWords
        });

        return new Border
        {
            Background = Brush("UmSurfaceBrush"),
            BorderBrush = Brush("UmHairlineBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(UmScale.Space.Md),
            Child = body
        };
    }

    // ---- themes ---------------------------------------------------------------------------------------

    /// <summary>
    /// One line on what the waiting reviews keep saying.
    /// </summary>
    /// <remarks>
    /// The counts come from <see cref="ReviewThemes"/>, never from the model. When local AI is on, the model
    /// is handed the finished sentence and asked to phrase it more naturally — it is given no reviews and no
    /// numbers to work out, so it has nothing to be wrong about. With AI off, the computed sentence is what
    /// shows, which is why this strip works at all without Ollama.
    /// </remarks>
    private void RenderThemes()
    {
        var withText = _queue.Count(r => !string.IsNullOrWhiteSpace(r.Text));
        var themes = ReviewThemes.Extract(_queue);
        var computed = ReviewThemes.Describe(themes, withText);

        if (computed is null)
        {
            ThemesStrip.Visibility = Visibility.Collapsed;
            ThemesStrip.Child = null;
            return;
        }

        // NO MODEL RUNS HERE, and the reason is worth keeping.
        //
        // This line used to be handed to the local model to "rephrase more naturally", on the theory that a
        // model given a finished sentence — no reviews, no arithmetic — had nothing left to be wrong about.
        // That theory was wrong. Observed live with phi3:mini, the computed sentence
        //   "Two of the 3 waiting reviews with text mention good results, all at Google Depilex DHA-2."
        // was rendered on the dashboard as
        //   "Two positive waiter experiences were mentioned in the last three Google reviews about our
        //    product, Depladuril HA-2, praising its effectiveness."
        // It read "waiting" as "waiter", invented a product name, invented what the reviews praised, and
        // turned a salon into a product line. A small model rewriting a sentence is still generating text,
        // and every word it changes is a word it can get wrong.
        //
        // The computed sentence is already plain English and is correct by construction, so the rewrite was
        // adding latency and risk for nothing. Tier 3's value here is the counting, which is deterministic.
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = UmScale.Space.Sm };
        row.Children.Add(new TextBlock
        {
            Text = "✦",
            FontSize = UmScale.Text.Body,
            Foreground = Brush("AccentFillColorDefaultBrush"),
            VerticalAlignment = VerticalAlignment.Top
        });
        row.Children.Add(new TextBlock
        {
            Text = computed,
            FontSize = UmScale.Text.Body,
            Foreground = Brush("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.WrapWholeWords
        });

        ThemesStrip.Child = row;
        ThemesStrip.Visibility = Visibility.Visible;
    }


    // ---- filters --------------------------------------------------------------------------------------

    private void RenderFilters()
    {
        FilterHost.Children.Clear();

        var counts = new (string Label, ReviewUrgency? Urgency)[]
        {
            ("All", null),
            ("Unhappy", ReviewUrgency.Critical),
            ("Mixed", ReviewUrgency.Elevated),
            ("Rating unread", ReviewUrgency.Unrated),
            ("Positive", ReviewUrgency.Routine)
        };

        foreach (var (label, urgency) in counts)
        {
            var n = urgency is null ? _queue.Count : _queue.Count(r => r.Urgency == urgency);

            // A filter that leads to a guaranteed-empty list is a dead end, so buckets with nothing in them
            // are not offered at all. "All" always stays, because it is the way back.
            if (n == 0 && urgency is not null)
            {
                continue;
            }

            var isOn = _filter == urgency;
            var chip = new Button
            {
                Content = $"{label}  {n}",
                FontSize = UmScale.Text.Caption,
                Padding = new Thickness(UmScale.Space.Sm, UmScale.Space.Xs, UmScale.Space.Sm, UmScale.Space.Xs),
                CornerRadius = new CornerRadius(12),
                Background = isOn ? Brush("UmAccentWashBrush") : Brush("UmSurfaceBrush"),
                BorderBrush = isOn ? Brush("AccentFillColorDefaultBrush") : Brush("UmHairlineStrongBrush"),
                BorderThickness = new Thickness(1),
                Tag = urgency
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
                chip, $"Show {label.ToLowerInvariant()} reviews, {n} of them.");

            chip.Click += (s, _) =>
            {
                _filter = (s as Button)?.Tag as ReviewUrgency?;
                _selected = 0;
                Render();
            };
            FilterHost.Children.Add(chip);
        }
    }

    // ---- queue ----------------------------------------------------------------------------------------

    private void RenderQueue(int accountCount, bool anyRead)
    {
        QueueHost.Children.Clear();
        _rows.Clear();

        var visible = _filter is null
            ? _queue
            : _queue.Where(r => r.Urgency == _filter).ToList();

        if (visible.Count == 0)
        {
            // A filter matching nothing is NOT an empty queue, and saying "nothing waiting for a reply"
            // while seven reviews wait is exactly the kind of false all-clear this surface exists to avoid.
            // Reachable without doing anything odd: filter to Unhappy, answer both, and the chip disappears
            // on the next render while _filter still points at it.
            string message;
            if (accountCount == 0)
            {
                message = "No Google Business account is connected.";
            }
            else if (_filter is { } activeFilter && _queue.Count > 0)
            {
                var label = ReviewQueue.Label(activeFilter).ToLowerInvariant();
                message = _queue.Count == 1
                    ? $"No {label} reviews. 1 other review is still waiting — clear the filter to see it."
                    : $"No {label} reviews. {_queue.Count} others are still waiting — clear the filter to see them.";
            }
            else
            {
                message = ReviewQueue.Summarise([], anyRead);
            }

            QueueHost.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = UmScale.Text.Body,
                Foreground = Brush("TextFillColorSecondaryBrush"),
                TextWrapping = TextWrapping.WrapWholeWords
            });
            return;
        }

        _selected = Math.Clamp(_selected, 0, Math.Min(visible.Count, MaxRows) - 1);

        for (var i = 0; i < visible.Count && i < MaxRows; i++)
        {
            var row = BuildRow(visible[i], i);
            _rows.Add(row);
            QueueHost.Children.Add(row);
        }

        if (visible.Count > MaxRows)
        {
            QueueHost.Children.Add(new TextBlock
            {
                Text = $"+ {visible.Count - MaxRows} more waiting",
                FontSize = UmScale.Text.Caption,
                Foreground = Brush("TextFillColorTertiaryBrush"),
                Margin = new Thickness(UmScale.Space.Sm, UmScale.Space.Xs, 0, 0)
            });
        }

        ApplySelection(moveFocus: false);
    }

    private Button BuildRow(QueuedReview review, int position)
    {
        var content = new StackPanel { Spacing = UmScale.Space.Sm };

        var top = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = UmScale.Space.Sm
        };
        top.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(review.Reviewer) ? "Reviewer" : review.Reviewer,
            FontSize = UmScale.Text.BodyStrong,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });

        var age = ReviewAge.ShortLabel(review.Age);
        top.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(age)
                ? review.AccountName
                : $"{review.AccountName} · {age} ago",
            FontSize = UmScale.Text.Caption,
            Foreground = Brush("TextFillColorTertiaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });

        top.Children.Add(BuildChip(
            review.Stars is >= 1 and <= 5
                ? $"★ {review.Stars} · {ReviewQueue.Label(review.Urgency)}"
                : ReviewQueue.Label(review.Urgency),
            review.Urgency));
        content.Children.Add(top);

        content.Children.Add(new TextBlock
        {
            // A star-only review is common and still needs answering, so the row says so plainly rather
            // than rendering an empty gap that looks like a scrape failure.
            Text = string.IsNullOrWhiteSpace(review.Text) ? "Rating only — no written review." : review.Text,
            FontSize = UmScale.Text.Body,
            Foreground = string.IsNullOrWhiteSpace(review.Text)
                ? Brush("TextFillColorTertiaryBrush")
                : Brush("TextFillColorSecondaryBrush"),
            FontStyle = string.IsNullOrWhiteSpace(review.Text)
                ? Windows.UI.Text.FontStyle.Italic
                : Windows.UI.Text.FontStyle.Normal,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 3,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, UmScale.Space.Xs, 0, 0)
        });

        // Severity also reads as a coloured edge, but never ONLY as colour — the chip above carries the
        // star count and the word, so the ranking survives being read by someone who cannot see the hue.
        var rail = new Border
        {
            Width = UmScale.Space.Xs,
            CornerRadius = new CornerRadius(2),
            Background = UrgencyBrush(review.Urgency),
            Margin = new Thickness(0, 0, UmScale.Space.Md, 0)
        };

        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(rail, 0);
        Grid.SetColumn(content, 1);
        layout.Children.Add(rail);
        layout.Children.Add(content);

        // Actions live inside the row so the draft can be shown in place. Only offered when local AI is on —
        // a button that always fails is worse than no button.
        if (ReviewReplyService.Instance.IsEnabled)
        {
            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = UmScale.Space.Sm,
                Margin = new Thickness(0, UmScale.Space.Sm, 0, 0)
            };

            var draftHost = new StackPanel { Spacing = UmScale.Space.Sm };

            var draftButton = new Button { Content = "Draft a reply", FontSize = UmScale.Text.Caption };
            draftButton.Click += async (_, _) => await DraftForAsync(review, draftButton, draftHost);
            actions.Children.Add(draftButton);

            content.Children.Add(actions);
            content.Children.Add(draftHost);
        }

        var row = new Button
        {
            Padding = new Thickness(UmScale.Space.Md),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Brush("UmHairlineBrush"),
            Background = Brush("UmSurfaceBrush"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Tag = position,
            Content = layout
        };

        row.Click += async (_, _) =>
        {
            _selected = position;
            ApplySelection(moveFocus: false);
            await OpenAsync(review);
        };
        row.GotFocus += (_, _) =>
        {
            _selected = position;
            ApplySelection(moveFocus: false);
        };

        ToolTipService.SetToolTip(row, new ToolTip
        {
            Content = string.IsNullOrWhiteSpace(review.Text)
                ? "Open this review in Google"
                : review.Text + "\n\nOpen in Google",
            MaxWidth = 460
        });

        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
            row,
            $"{ReviewQueue.Label(review.Urgency)} review from " +
            $"{(string.IsNullOrWhiteSpace(review.Reviewer) ? "an unnamed reviewer" : review.Reviewer)} " +
            $"at {review.AccountName}, waiting {(string.IsNullOrWhiteSpace(age) ? "an unknown time" : age)}. " +
            "Press Enter to open it in Google.");

        return row;
    }

    private Border BuildChip(string text, ReviewUrgency urgency)
    {
        var wash = urgency switch
        {
            ReviewUrgency.Critical => "UmStatusDangerWashBrush",
            ReviewUrgency.Elevated => "UmStatusWarningWashBrush",
            ReviewUrgency.Routine => "UmStatusSuccessWashBrush",
            _ => "UmSurfaceSunkenBrush"
        };

        return new Border
        {
            Background = Brush(wash),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(UmScale.Space.Sm, 2, UmScale.Space.Sm, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = text,
                FontSize = UmScale.Text.Caption,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = UrgencyBrush(urgency)
            }
        };
    }

    private Brush UrgencyBrush(ReviewUrgency urgency) => urgency switch
    {
        ReviewUrgency.Critical => Brush("UmStatusDangerBrush"),
        ReviewUrgency.Elevated => Brush("UmStatusWarningBrush"),
        ReviewUrgency.Unrated => Brush("UmStatusMutedBrush"),
        _ => Brush("UmStatusSuccessBrush")
    };

    // ---- ask for a review -----------------------------------------------------------------------------

    /// <summary>
    /// Customers whose WhatsApp conversation ended with them saying thank you, and a drafted request.
    /// </summary>
    /// <remarks>
    /// Hidden entirely when there is nobody to ask — an empty "ask for a review" panel is an invitation to
    /// lower the bar on who qualifies, and the bar is the feature.
    /// </remarks>
    private void RenderAskPanel()
    {
        if (_services is null)
        {
            return;
        }

        var whatsAppAccounts = _services.Registry.Instances
            .Where(i => i.IsProfessional)
            .Where(i => PlatformDefinition.NormalizePlatformId(i.Platform) is "whatsapp" or "whatsappbusiness")
            .Select(i => (
                i.Id,
                Name: string.IsNullOrWhiteSpace(i.DisplayName) ? "WhatsApp" : i.DisplayName,
                Chats: OversightChatSnapshotService.Instance.GetChats(i.Id)))
            .ToList();

        var candidates = ReviewAskCandidates.Select(
            whatsAppAccounts, ReviewAsks.Current.AskedPhones(), DateTimeOffset.UtcNow);

        if (candidates.Count == 0)
        {
            AskPanel.Visibility = Visibility.Collapsed;
            AskPanel.Child = null;
            return;
        }

        var body = new StackPanel { Spacing = UmScale.Space.Sm };
        body.Children.Add(new TextBlock
        {
            Text = "Ask for a review",
            FontSize = UmScale.Text.BodyStrong,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        body.Children.Add(new TextBlock
        {
            Text = "Customers who messaged recently and said thank you. Drafted for WhatsApp — you press send.",
            FontSize = UmScale.Text.Caption,
            Foreground = Brush("TextFillColorTertiaryBrush"),
            TextWrapping = TextWrapping.WrapWholeWords
        });

        foreach (var candidate in candidates)
        {
            body.Children.Add(BuildAskRow(candidate));
        }

        // Asked-count only. The app can count what it asked and can count what arrived, but it cannot know
        // that one caused the other — printing the two side by side would invite exactly that reading.
        var askedRecently = ReviewAsks.Current.AskedWithin(30);
        body.Children.Add(new TextBlock
        {
            Text = askedRecently > 0
                ? $"{askedRecently} asked in the last 30 days. Never sends on its own."
                : "Never sends on its own — you send every message yourself.",
            FontSize = UmScale.Text.Caption,
            Foreground = Brush("TextFillColorTertiaryBrush"),
            Margin = new Thickness(0, UmScale.Space.Sm, 0, 0)
        });

        AskPanel.Child = body;
        AskPanel.Visibility = Visibility.Visible;
    }

    private Grid BuildAskRow(ReviewAskCandidate candidate)
    {
        var line = new Grid { Margin = new Thickness(0, UmScale.Space.Xs, 0, UmScale.Space.Xs) };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var who = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        who.Children.Add(new TextBlock
        {
            Text = candidate.CustomerName,
            FontSize = UmScale.Text.Body,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        who.Children.Add(new TextBlock
        {
            Text = $"{candidate.AccountName} · messaged {ReviewAskCandidates.WhenLabel(candidate.LastActivityUtc, DateTimeOffset.UtcNow)} · said thank you",
            FontSize = UmScale.Text.Caption,
            Foreground = Brush("TextFillColorTertiaryBrush")
        });
        line.Children.Add(who);

        var draft = new Button
        {
            Content = "Draft",
            FontSize = UmScale.Text.Caption,
            VerticalAlignment = VerticalAlignment.Center
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
            draft, $"Draft a review request to {candidate.CustomerName}. Opens WhatsApp; you send it.");
        draft.Click += async (_, _) => await DraftAskAsync(candidate, draft);
        Grid.SetColumn(draft, 1);
        line.Children.Add(draft);

        return line;
    }

    /// <summary>
    /// Puts the request on the clipboard, opens that customer's WhatsApp chat, and records the ask.
    /// </summary>
    /// <remarks>
    /// <b>The send is the owner's.</b> Nothing here transmits a message; WhatsApp is opened on the right
    /// conversation with the text ready to paste. The ask is recorded as soon as the chat is opened rather
    /// than on some later confirmation the app cannot observe — erring towards never asking twice, which is
    /// the direction that protects the customer.
    /// </remarks>
    private async Task DraftAskAsync(ReviewAskCandidate candidate, Button trigger)
    {
        if (_services is null)
        {
            return;
        }

        trigger.IsEnabled = false;
        try
        {
            var instance = _services.Registry.Instances.FirstOrDefault(i => i.Id == candidate.InstanceId);
            if (instance is null)
            {
                return;
            }

            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(ReviewAskDraft.Build(candidate.CustomerName, candidate.AccountName));
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);

            await ReviewAsks.Current.MarkAskedAsync(candidate.AskKey);

            await ConversationFocusHelper.TryFocusConversationWithRetryAsync(
                InstanceSessionManager.Instance,
                instance,
                candidate.ConversationKey,
                candidate.CustomerName,
                contactPhone: candidate.Phone);
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning(
                "ReviewAsk",
                $"Could not open the customer's chat: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            trigger.IsEnabled = true;
            Render();
        }
    }

    // ---- by branch ------------------------------------------------------------------------------------

    private void RenderBranches(
        IReadOnlyList<(MessengerInstance Instance, string Name, GoogleReviewSnapshotService.ReviewHealth Health,
            GoogleReviewSnapshotService.ProfileRating? Rating)> snapshots)
    {
        var rated = snapshots
            .Where(s => s.Rating is { Total: > 0 })
            .OrderByDescending(s => double.TryParse(
                s.Rating!.Value.Rating, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0)
            .ToList();

        if (rated.Count == 0)
        {
            BranchPanel.Visibility = Visibility.Collapsed;
            BranchPanel.Child = null;
            return;
        }

        var body = new StackPanel { Spacing = UmScale.Space.Sm };
        body.Children.Add(new TextBlock
        {
            Text = "By branch",
            FontSize = UmScale.Text.BodyStrong,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        var anyGains = rated.Any(s =>
            ReviewTrend.ReviewsGained(ReviewHistoryStore.Instance.GetHistory(s.Instance.Id), 30) is not null);

        body.Children.Add(new TextBlock
        {
            // The subtitle only claims what the rows actually show, so it changes once gains are real.
            Text = anyGains
                ? "Rating, lifetime reviews, and how many arrived recently."
                : "Rating and lifetime reviews. Change over time needs a second day of readings.",
            FontSize = UmScale.Text.Caption,
            Foreground = Brush("TextFillColorTertiaryBrush"),
            TextWrapping = TextWrapping.WrapWholeWords
        });

        var rank = 1;
        foreach (var snapshot in rated)
        {
            var line = new Grid();
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var n = new TextBlock
            {
                Text = rank.ToString(),
                FontSize = UmScale.Text.Caption,
                Foreground = Brush("TextFillColorTertiaryBrush"),
                Margin = new Thickness(0, 0, UmScale.Space.Sm, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var name = new TextBlock
            {
                Text = snapshot.Name,
                FontSize = UmScale.Text.Body,
                VerticalAlignment = VerticalAlignment.Center
            };
            var right = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = UmScale.Space.Sm,
                VerticalAlignment = VerticalAlignment.Center
            };
            right.Children.Add(new TextBlock
            {
                Text = $"{snapshot.Rating!.Value.Rating}  ·  {snapshot.Rating.Value.Total:N0}",
                FontSize = UmScale.Text.Body,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });

            if (ReviewTrend.ReviewsGained(ReviewHistoryStore.Instance.GetHistory(snapshot.Instance.Id), 30) is { } gain)
            {
                var delta = gain.To - gain.From;
                right.Children.Add(new TextBlock
                {
                    Text = delta > 0 ? $"+{delta}" : "no change",
                    FontSize = UmScale.Text.Caption,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = delta > 0 ? Brush("UmStatusSuccessBrush") : Brush("TextFillColorTertiaryBrush"),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            Grid.SetColumn(name, 1);
            Grid.SetColumn(right, 2);
            line.Children.Add(n);
            line.Children.Add(name);
            line.Children.Add(right);
            body.Children.Add(line);
            rank++;
        }

        BranchPanel.Child = body;
        BranchPanel.Visibility = Visibility.Visible;
    }

    // ---- selection + keyboard -------------------------------------------------------------------------

    private void ApplySelection(bool moveFocus = true)
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            var isSelected = i == _selected;
            _rows[i].Background = isSelected ? Brush("UmAccentWashBrush") : Brush("UmSurfaceBrush");
            _rows[i].BorderBrush = isSelected ? Brush("UmHairlineStrongBrush") : Brush("UmHairlineBrush");
        }

        if (_selected < 0 || _selected >= _rows.Count)
        {
            return;
        }

        if (moveFocus)
        {
            // Focus IS the selection. Without this the highlight moves and a screen reader says nothing.
            _rows[_selected].Focus(FocusState.Keyboard);
        }

        _rows[_selected].StartBringIntoView();
    }

    private void OnQueueKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_rows.Count == 0)
        {
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.Down:
            case VirtualKey.J:
                _selected = Math.Min(_selected + 1, _rows.Count - 1);
                ApplySelection();
                e.Handled = true;
                break;

            case VirtualKey.Up:
            case VirtualKey.K:
                _selected = Math.Max(_selected - 1, 0);
                ApplySelection();
                e.Handled = true;
                break;

            case VirtualKey.Home:
                _selected = 0;
                ApplySelection();
                e.Handled = true;
                break;

            case VirtualKey.End:
                _selected = _rows.Count - 1;
                ApplySelection();
                e.Handled = true;
                break;

            // Enter and Space are deliberately absent: each row is a Button, so it activates on both by
            // itself and raises Click. Handling them here as well would open the review twice.
        }
    }

    /// <summary>
    /// Asks the local model for a reply and shows it in the row for the owner to read and edit.
    /// </summary>
    /// <remarks>
    /// The end of this flow is deliberately manual: the draft goes to the clipboard and Google's reply box is
    /// opened, and the owner presses send. The app has no path that publishes anything, which is what makes
    /// an AI-written reply to an angry customer safe to offer at all.
    /// </remarks>
    private async Task DraftForAsync(QueuedReview review, Button trigger, Panel host)
    {
        trigger.IsEnabled = false;
        trigger.Content = "Drafting…";
        host.Children.Clear();

        try
        {
            var result = await ReviewReplyService.Instance.DraftAsync(review, review.AccountName);

            if (!result.HasDraft)
            {
                host.Children.Add(new TextBlock
                {
                    Text = result.Message,
                    FontSize = UmScale.Text.Caption,
                    Foreground = Brush("TextFillColorTertiaryBrush"),
                    TextWrapping = TextWrapping.WrapWholeWords
                });
                return;
            }

            var box = new StackPanel { Spacing = UmScale.Space.Sm };
            box.Children.Add(new TextBlock
            {
                Text = "SUGGESTED REPLY · ON THIS DEVICE · YOU SEND IT",
                FontSize = UmScale.Text.Caption,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = Brush("AccentFillColorDefaultBrush")
            });

            // Editable in place: the owner is expected to change it, and a read-only block invites pasting
            // it unchanged.
            var editor = new TextBox
            {
                Text = result.Text,
                FontSize = UmScale.Text.Body,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                Background = Brush("UmSurfaceBrush")
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(editor, "Suggested reply, editable");
            box.Children.Add(editor);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = UmScale.Space.Sm };
            var copyOpen = new Button
            {
                Content = "Copy & open in Google",
                FontSize = UmScale.Text.Caption,
                Style = Application.Current.Resources["AccentButtonStyle"] as Style
            };
            copyOpen.Click += async (_, _) =>
            {
                var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                package.SetText(editor.Text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                await OpenAsync(review);
            };
            buttons.Children.Add(copyOpen);

            var discard = new Button { Content = "Discard", FontSize = UmScale.Text.Caption };
            discard.Click += (_, _) => host.Children.Clear();
            buttons.Children.Add(discard);

            box.Children.Add(buttons);

            var frame = new Border
            {
                Background = Brush("UmAccentWashBrush"),
                BorderBrush = Brush("AccentFillColorDefaultBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(UmScale.Space.Md),
                Child = box
            };
            host.Children.Add(frame);
        }
        finally
        {
            trigger.IsEnabled = true;
            trigger.Content = "Draft a reply";
        }
    }

    private async Task OpenAsync(QueuedReview review)
    {
        try
        {
            await GoogleReviewSnapshotService.Instance.FocusReviewAsync(
                review.InstanceId, review.Reviewer, review.Index);
        }
        catch (Exception ex)
        {
            // Opening a review is a convenience on top of the queue; failing to navigate must not take the
            // page down or lose the owner's place in the list.
            AppLogger.LogWarning(
                "ReviewDesk",
                $"Could not open the selected review in Google: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // ---- small helpers --------------------------------------------------------------------------------

    private TextBlock Label(string text, HorizontalAlignment align = HorizontalAlignment.Left) => new()
    {
        Text = text.ToUpperInvariant(),
        FontSize = UmScale.Text.Caption,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Foreground = Brush("TextFillColorTertiaryBrush"),
        HorizontalAlignment = align
    };

    private static string RelativeAge(DateTimeOffset whenUtc)
    {
        var span = DateTimeOffset.UtcNow - whenUtc;
        if (span < TimeSpan.Zero)
        {
            span = TimeSpan.Zero;
        }

        return span.TotalMinutes switch
        {
            < 1 => "just now",
            < 60 => $"{(int)span.TotalMinutes} min ago",
            < 1440 => $"{(int)span.TotalHours}h ago",
            _ => $"{(int)span.TotalDays}d ago"
        };
    }

    private Brush Brush(string key) => ThemeBrushResolver.Resolve(this, key);
}
