using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Dialogs;

/// <summary>
/// An intermediate per-account "L1" view opened from a dashboard card — the account's own caught-up %,
/// awaiting backlog, First Response Time / SLA / answered-today, a 7-day activity sparkline, and the list of
/// customers waiting (each a click-through) — before dropping into the raw WhatsApp WebView. All local data.
/// </summary>
public sealed class AccountDetailDialog : ContentDialog
{
    private readonly ApplicationServices _services;
    private readonly MessengerInstance _instance;

    public AccountDetailDialog(ApplicationServices services, MessengerInstance instance)
    {
        _services = services;
        _instance = instance;

        Title = instance.DisplayName;
        CloseButtonText = "Close";
        PrimaryButtonText = "Open WhatsApp";
        DefaultButton = ContentDialogButton.Primary;
        PrimaryButtonClick += (_, _) => _services.Navigation.OpenInstance(instance.Id, null, null);

        Content = Build();
    }

    private FrameworkElement Build()
    {
        var sla = AppSettingsService.Instance.Settings.SlaThresholdMinutes;
        var weekStart = DateTimeOffset.Now.AddDays(-7);
        var response = ResponseTimeTracker.Instance.GetStats([_instance], weekStart, null, sla);
        var awaiting = OversightChatSnapshotService.Instance.GetAwaiting(_instance.Id);

        OversightChatSnapshotService.Instance.TryGetWindowed(_instance.Id, null, out var active, out var caughtUp);
        var caughtPct = active > 0 ? (int)Math.Round(100.0 * caughtUp / active) : 100;

        var body = new StackPanel { Spacing = 14, MinWidth = 440 };

        // Metric tiles.
        var metrics = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
        for (var c = 0; c < 3; c++)
        {
            metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        metrics.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        metrics.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        void AddTile(int col, int row, string label, string value, string brushKey)
        {
            var tile = Tile(label, value, brushKey);
            Grid.SetColumn(tile, col);
            Grid.SetRow(tile, row);
            metrics.Children.Add(tile);
        }

        AddTile(0, 0, "Caught up", active > 0 ? $"{caughtPct}%" : "—", StatusKey(caughtPct));
        AddTile(1, 0, "Awaiting", awaiting.Count.ToString(), awaiting.Count > 0 ? "SystemFillColorCriticalBrush" : "SystemFillColorSuccessBrush");
        AddTile(2, 0, "Answered today", response.AnsweredToday.ToString(), "SystemFillColorSuccessBrush");
        AddTile(0, 1, "Reply time", response.HasData ? BusinessReport.FormatMinutes(response.MedianMinutes) : "—", "TextFillColorPrimaryBrush");
        AddTile(1, 1, "SLA met", response.HasData ? $"{response.SlaCompliancePercent}%" : "—", StatusKey(response.SlaCompliancePercent));
        AddTile(2, 1, "Replies", response.SampleCount.ToString(), "TextFillColorSecondaryBrush");
        body.Children.Add(metrics);

        // Waiting customers (top few), click-through.
        if (awaiting.Count > 0)
        {
            body.Children.Add(new TextBlock { Text = "Waiting on a reply", FontWeight = FontWeights.SemiBold, FontSize = 13 });
            var list = new StackPanel { Spacing = 4 };
            foreach (var chat in awaiting.Take(8))
            {
                list.Children.Add(BuildWaitingRow(chat));
            }

            if (awaiting.Count > 8)
            {
                list.Children.Add(new TextBlock
                {
                    Text = $"+ {awaiting.Count - 8} more",
                    FontSize = 11,
                    Foreground = Res("TextFillColorTertiaryBrush"),
                    Margin = new Thickness(2, 2, 0, 0)
                });
            }

            body.Children.Add(new ScrollViewer { MaxHeight = 200, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = list });
        }
        else
        {
            body.Children.Add(new TextBlock
            {
                Text = "No customers are waiting on a reply right now.",
                Foreground = Res("TextFillColorSecondaryBrush")
            });
        }

        return body;
    }

    private FrameworkElement BuildWaitingRow(OversightChatSnapshotService.ChatEntry chat)
    {
        var (name, preview) = OversightThreadEnricher.Enrich(_instance.Id, chat);
        var display = string.IsNullOrWhiteSpace(name) ? "Customer" : name;

        var text = new StackPanel { Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = display, FontSize = 13, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        if (!string.IsNullOrWhiteSpace(preview))
        {
            text.Children.Add(new TextBlock { Text = preview, FontSize = 11, Foreground = Res("TextFillColorSecondaryBrush"), TextTrimming = TextTrimming.CharacterEllipsis });
        }

        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = Res2("CardBackgroundFillColorSecondaryBrush"),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 7, 10, 7),
            Content = text
        };
        var key = chat.ConversationKey;
        var customer = chat.CustomerName;
        var phone = chat.ContactPhone;
        button.Click += (_, _) =>
        {
            Hide();
            _services.Navigation.OpenInstance(_instance.Id, key, customer, phone);
        };
        return button;
    }

    private FrameworkElement Tile(string label, string value, string valueBrushKey)
    {
        var stack = new StackPanel { Spacing = 1 };
        stack.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = Res("TextFillColorSecondaryBrush") });
        stack.Children.Add(new TextBlock { Text = value, FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = Res(valueBrushKey) });
        return new Border
        {
            Background = Res2("CardBackgroundFillColorSecondaryBrush"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 9, 12, 9),
            Child = stack
        };
    }

    private static string StatusKey(int percent) =>
        percent >= 90 ? "SystemFillColorSuccessBrush" : percent >= 70 ? "SystemFillColorCautionBrush" : "SystemFillColorCriticalBrush";

    private static SolidColorBrush Res(string key) =>
        Application.Current.Resources.TryGetValue(key, out var v) && v is SolidColorBrush b ? b : new SolidColorBrush(Microsoft.UI.Colors.Gray);

    private static Brush Res2(string key) =>
        Application.Current.Resources.TryGetValue(key, out var v) && v is Brush b ? b : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
}
