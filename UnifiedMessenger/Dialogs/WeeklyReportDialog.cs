using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Dialogs;

/// <summary>
/// Shows the plain-language weekly business report (summary + ranked insights + per-account table) with
/// options to save it as Markdown or export the raw analytics as CSV. All local — nothing leaves the machine.
/// </summary>
public sealed class WeeklyReportDialog : ContentDialog
{
    private readonly BusinessReportResult _report;
    private readonly IReadOnlyList<MessengerInstance> _instances;

    public WeeklyReportDialog(
        ReportInputs inputs,
        BusinessReportResult report,
        IReadOnlyList<MessengerInstance> instances,
        string? aiSummary = null)
    {
        _report = report;
        _instances = instances;

        Title = "Weekly report";
        CloseButtonText = "Close";
        PrimaryButtonText = "Save report (.md)";
        SecondaryButtonText = "Export data (.csv)";
        DefaultButton = ContentDialogButton.Close;

        var body = new StackPanel { Spacing = 12, MinWidth = 460 };

        // Period + headline summary (AI-narrated when available, else the deterministic summary).
        body.Children.Add(new TextBlock
        {
            Text = inputs.PeriodLabel,
            FontSize = 12,
            Foreground = Res("TextFillColorTertiaryBrush")
        });
        body.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(aiSummary) ? report.Summary : aiSummary,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.WrapWholeWords
        });

        // Ranked insight rows.
        var insightHost = new StackPanel { Spacing = 8 };
        if (report.Insights.Count == 0)
        {
            insightHost.Children.Add(new TextBlock
            {
                Text = "Nothing notable this week — activity looks steady.",
                Foreground = Res("TextFillColorSecondaryBrush"),
                TextWrapping = TextWrapping.WrapWholeWords
            });
        }
        else
        {
            foreach (var insight in report.Insights)
            {
                insightHost.Children.Add(BuildInsightRow(insight));
            }
        }

        // Response-time trend (last 7 days) — median first reply per day, if any replies were measured.
        var trend = ResponseTimeTracker.Instance.GetDailyMedians(instances, 7);
        var trendChart = BuildResponseTrend(trend);
        if (trendChart is not null)
        {
            insightHost.Children.Add(trendChart);
        }

        body.Children.Add(new ScrollViewer
        {
            MaxHeight = 360,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = insightHost
        });

        Content = body;

        PrimaryButtonClick += OnSaveMarkdown;
        SecondaryButtonClick += OnExportCsv;
    }

    private FrameworkElement BuildInsightRow(BusinessInsight insight)
    {
        var (glyph, brushKey) = insight.Severity switch
        {
            InsightSeverity.Warn => ("", "SystemFillColorCautionBrush"),
            InsightSeverity.Good => ("", "SystemFillColorSuccessBrush"),
            _ => ("", "SystemFillColorAttentionBrush"),
        };

        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icon = new FontIcon { Glyph = glyph, FontSize = 15, Foreground = Res(brushKey), VerticalAlignment = VerticalAlignment.Top };
        icon.Margin = new Thickness(0, 2, 0, 0);
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var text = new StackPanel { Spacing = 1 };
        text.Children.Add(new TextBlock { Text = insight.Title, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.WrapWholeWords });
        text.Children.Add(new TextBlock
        {
            Text = insight.Detail,
            FontSize = 12,
            Foreground = Res("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.WrapWholeWords
        });
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        return new Border
        {
            Background = Res2("CardBackgroundFillColorSecondaryBrush"),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(11, 9, 11, 9),
            Child = grid
        };
    }

    /// <summary>A small bar chart of median first-reply time per day (last 7 days). Null when no replies measured.</summary>
    private FrameworkElement? BuildResponseTrend(IReadOnlyList<ResponseTimeTracker.DailyResponsePoint> points)
    {
        if (points.Count == 0 || points.All(p => p.Count == 0))
        {
            return null;
        }

        var max = Math.Max(1.0, points.Max(p => p.MedianMinutes));
        var chart = new Grid { Height = 90, ColumnSpacing = 6 };
        var axis = new Grid { ColumnSpacing = 6, Margin = new Thickness(0, 3, 0, 0) };
        for (var i = 0; i < points.Count; i++)
        {
            chart.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            axis.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var p = points[i];
            var col = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            if (p.Count > 0)
            {
                col.Children.Add(new TextBlock
                {
                    Text = BusinessReport.FormatMinutes(p.MedianMinutes),
                    FontSize = 9,
                    Foreground = Res("TextFillColorTertiaryBrush"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 2)
                });
                col.Children.Add(new Border
                {
                    Height = Math.Max(3, p.MedianMinutes / max * 60),
                    Background = Res("AccentFillColorDefaultBrush"),
                    CornerRadius = new CornerRadius(3, 3, 0, 0),
                    Margin = new Thickness(3, 0, 3, 0)
                });
            }

            Grid.SetColumn(col, i);
            chart.Children.Add(col);

            var lbl = new TextBlock { Text = p.Label, FontSize = 10, Foreground = Res("TextFillColorTertiaryBrush"), HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetColumn(lbl, i);
            axis.Children.Add(lbl);
        }

        var wrap = new StackPanel { Spacing = 2 };
        wrap.Children.Add(new TextBlock { Text = "Median first reply, last 7 days", FontSize = 12, FontWeight = FontWeights.SemiBold });
        wrap.Children.Add(chart);
        wrap.Children.Add(axis);

        return new Border
        {
            Background = Res2("CardBackgroundFillColorSecondaryBrush"),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(11, 9, 11, 9),
            Child = wrap
        };
    }

    private async void OnSaveMarkdown(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            var path = await PickSavePathAsync("Business report", "Markdown", ".md");
            if (path is not null)
            {
                await File.WriteAllTextAsync(path, _report.Markdown);
            }
        }
        catch
        {
            // best-effort; a failed save shouldn't crash the dialog.
        }
        finally
        {
            deferral.Complete();
        }

        args.Cancel = true; // keep the dialog open after saving
    }

    private async void OnExportCsv(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            var path = await PickSavePathAsync("Message analytics", "CSV", ".csv");
            if (path is not null)
            {
                await MessageAnalyticsService.Instance.ExportCsvAsync(_instances, path);
            }
        }
        catch
        {
            // best-effort
        }
        finally
        {
            deferral.Complete();
        }

        args.Cancel = true;
    }

    private static async Task<string?> PickSavePathAsync(string suggestedName, string typeName, string extension)
    {
        if (App.CurrentWindow is null)
        {
            return null;
        }

        var picker = new Windows.Storage.Pickers.FileSavePicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedName
        };
        picker.FileTypeChoices.Add(typeName, [extension]);

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    private static SolidColorBrush Res(string key) =>
        Application.Current.Resources.TryGetValue(key, out var v) && v is SolidColorBrush b
            ? b
            : new SolidColorBrush(Microsoft.UI.Colors.Gray);

    private static Brush Res2(string key) =>
        Application.Current.Resources.TryGetValue(key, out var v) && v is Brush b
            ? b
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
}
