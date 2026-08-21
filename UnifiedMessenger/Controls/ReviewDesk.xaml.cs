using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using Windows.System;

namespace UnifiedMessenger.Controls;

/// <summary>
/// One answer-this-first queue of unanswered Google reviews across every location.
/// </summary>
/// <remarks>
/// <para>
/// <b>What it adds over the health panel.</b> That panel reports per account — three salons, three lists,
/// and no way to see that the angriest unanswered review in the business is the one-star at the bottom of
/// the second one. This merges them and ranks them worst-first (see <see cref="ReviewQueue"/>), so the
/// review at the top is the one costing the most money.
/// </para>
/// <para>
/// <b>Keyboard.</b> Up/Down move the selection, Enter opens the selected review in Google. Replying to
/// reviews is a sit-down-and-work-through-them job, and reaching for the mouse for every row is what makes
/// people stop doing it.
/// </para>
/// </remarks>
public sealed partial class ReviewDesk : UserControl
{
    private ApplicationServices? _services;
    private IReadOnlyList<QueuedReview> _queue = [];
    private int _selected;

    /// <summary>Row containers in queue order, so selection can move without rebuilding.</summary>
    private readonly List<Button> _rows = [];

    /// <summary>
    /// How many rows the desk shows. The scrape itself only reports the first several pending reviews per
    /// account, so this is a display bound rather than a promise about the total — the footer states the
    /// remainder explicitly rather than letting the list imply it is everything.
    /// </summary>
    private const int MaxRows = 12;

    public ReviewDesk()
    {
        InitializeComponent();
        QueueHost.KeyDown += OnQueueKeyDown;
        ActualThemeChanged += (_, _) => Render();
    }

    public void ConfigureServices(ApplicationServices services) => _services = services;

    /// <summary>Fired when the owner opens a review, so the host can refresh other surfaces.</summary>
    public event EventHandler? ReviewOpened;

    public void Render()
    {
        if (_services is null)
        {
            return;
        }

        var accounts = GoogleAccounts().ToList();
        _queue = ReviewQueue.Build(accounts.Select(instance => (
            instance.Id,
            string.IsNullOrWhiteSpace(instance.DisplayName) ? "Google Business" : instance.DisplayName,
            (GoogleReviewSnapshotService.ReviewHealth?)GoogleReviewSnapshotService.Instance.Get(instance.Id))));

        var anyRead = accounts.Any(instance =>
            GoogleReviewSnapshotService.Instance.Get(instance.Id) is { HasData: true });

        DeskSummary.Text = ReviewQueue.Summarise(_queue, anyRead);

        QueueHost.Children.Clear();
        _rows.Clear();

        if (_queue.Count == 0)
        {
            // The summary above already states which kind of empty this is, so the body only speaks when it
            // has something the summary cannot say — no account connected at all.
            if (accounts.Count == 0)
            {
                QueueHost.Children.Add(BuildEmptyState());
            }

            return;
        }

        _selected = Math.Clamp(_selected, 0, Math.Min(_queue.Count, MaxRows) - 1);

        for (var i = 0; i < _queue.Count && i < MaxRows; i++)
        {
            var row = BuildRow(_queue[i], i);
            _rows.Add(row);
            QueueHost.Children.Add(row);
        }

        if (_queue.Count > MaxRows)
        {
            QueueHost.Children.Add(new TextBlock
            {
                Text = $"+ {_queue.Count - MaxRows} more waiting, further down the list",
                FontSize = UmScale.Text.Caption,
                Foreground = Brush("TextFillColorTertiaryBrush"),
                Margin = new Thickness(UmScale.Space.Sm, UmScale.Space.Xs, 0, 0)
            });
        }

        ApplySelection();
    }

    private IEnumerable<MessengerInstance> GoogleAccounts() =>
        _services is null
            ? []
            : _services.Registry.Instances.Where(instance =>
                string.Equals(
                    PlatformDefinition.NormalizePlatformId(instance.Platform),
                    "googlebusiness",
                    StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The only empty case the summary line cannot describe: there is no Google account at all.
    /// </summary>
    /// <remarks>
    /// "Nothing waiting", "not read yet" and "no account connected" are three different situations and the
    /// desk must never blur them — a scrape that has silently stopped working would otherwise report itself
    /// as a clean queue. The first two are stated by <see cref="ReviewQueue.Summarise"/>; this is the third.
    /// </remarks>
    private UIElement BuildEmptyState() => new TextBlock
    {
        Text = "No Google Business account is connected.",
        FontSize = UmScale.Text.Body,
        Foreground = Brush("TextFillColorSecondaryBrush"),
        TextWrapping = TextWrapping.WrapWholeWords
    };

    private Button BuildRow(QueuedReview review, int position)
    {
        var content = new StackPanel { Spacing = UmScale.Space.Xs };

        var head = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = UmScale.Space.Sm
        };

        head.Children.Add(new TextBlock
        {
            Text = review.Stars is >= 1 and <= 5
                ? new string('★', review.Stars) + new string('☆', 5 - review.Stars)
                : "☆☆☆☆☆",
            FontSize = UmScale.Text.Caption,
            Foreground = UrgencyBrush(review.Urgency),
            VerticalAlignment = VerticalAlignment.Center
        });

        head.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(review.Reviewer) ? "Reviewer" : review.Reviewer,
            FontSize = UmScale.Text.Body,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });

        // The location matters here in a way it never did on the per-account card: the whole point of one
        // merged queue is that consecutive rows can belong to different salons.
        head.Children.Add(new TextBlock
        {
            Text = review.AccountName,
            FontSize = UmScale.Text.Caption,
            Foreground = Brush("TextFillColorTertiaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });

        var age = ReviewAge.ShortLabel(review.Age);
        if (!string.IsNullOrWhiteSpace(age))
        {
            head.Children.Add(new TextBlock
            {
                Text = "· waiting " + age,
                FontSize = UmScale.Text.Caption,
                Foreground = Brush("TextFillColorTertiaryBrush"),
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        content.Children.Add(head);

        if (!string.IsNullOrWhiteSpace(review.Text))
        {
            content.Children.Add(new TextBlock
            {
                Text = review.Text,
                FontSize = UmScale.Text.Caption,
                Foreground = Brush("TextFillColorSecondaryBrush"),
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        // A coloured left rail rather than colour on the text: severity has to survive being read by
        // someone who cannot distinguish the hues, so the star glyphs and the wording carry it too
        // (WCAG 1.4.1 — colour is never the only channel).
        var rail = new Border
        {
            Width = UmScale.Space.Xs,
            CornerRadius = new CornerRadius(2),
            Background = UrgencyBrush(review.Urgency),
            Margin = new Thickness(0, 0, UmScale.Space.Sm, 0)
        };

        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(rail, 0);
        Grid.SetColumn(content, 1);
        layout.Children.Add(rail);
        layout.Children.Add(content);

        // A Button, not a Border. Selection used to be nothing but a background colour on a Border, which
        // means someone using a screen reader pressing Down heard silence — the row is what changed, and a
        // Border cannot take focus or announce itself. As a Button each row is focusable, so moving the
        // selection moves focus, which is both the visual highlight and the thing that gets read out. It
        // also gets Enter and Space activation for free rather than hand-rolled.
        var row = new Button
        {
            Padding = new Thickness(UmScale.Space.Sm),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Tag = position,
            Content = layout
        };

        row.Click += (_, _) =>
        {
            _selected = position;
            ApplySelection();
            OpenSelected();
        };

        // Keep the highlight following real focus, however focus arrived — Tab, a screen reader's own
        // navigation, or the arrow keys below.
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

    private Brush UrgencyBrush(ReviewUrgency urgency) => urgency switch
    {
        ReviewUrgency.Critical => Brush("UmStatusDangerBrush"),
        ReviewUrgency.Elevated => Brush("UmStatusWarningBrush"),
        ReviewUrgency.Unrated => Brush("UmStatusMutedBrush"),
        _ => Brush("UmStatusSuccessBrush")
    };

    /// <param name="moveFocus">
    /// False when this is reacting to focus that has already moved, which would otherwise recurse.
    /// </param>
    private void ApplySelection(bool moveFocus = true)
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            var isSelected = i == _selected;
            _rows[i].Background = isSelected
                ? Brush("UmAccentWashBrush")
                : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            _rows[i].BorderBrush = isSelected
                ? Brush("UmHairlineStrongBrush")
                : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
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
                _selected = Math.Min(_selected + 1, _rows.Count - 1);
                ApplySelection();
                e.Handled = true;
                break;

            case VirtualKey.Up:
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

    private async void OpenSelected()
    {
        if (_selected < 0 || _selected >= _queue.Count)
        {
            return;
        }

        var review = _queue[_selected];
        try
        {
            await GoogleReviewSnapshotService.Instance.FocusReviewAsync(
                review.InstanceId, review.Reviewer, review.Index);
            ReviewOpened?.Invoke(this, EventArgs.Empty);
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

    private Brush Brush(string key) => ThemeBrushResolver.Resolve(this, key);
}
