using System.Runtime.InteropServices.WindowsRuntime;
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
    private BusinessReportResult _report;
    private readonly IReadOnlyList<MessengerInstance> _instances;
    private readonly FrameworkElement _captureSurface;
    private readonly StackPanel _contentStack;

    public WeeklyReportDialog(
        ReportInputs inputs,
        BusinessReportResult report,
        IReadOnlyList<MessengerInstance> instances,
        string? aiSummary = null)
    {
        _report = report;
        _instances = instances;

        Title = "Business report";
        CloseButtonText = "Close";
        PrimaryButtonText = "Save report (.md)";
        SecondaryButtonText = "Export data (.csv)";
        DefaultButton = ContentDialogButton.Close;

        var body = new StackPanel { Spacing = 10, MinWidth = 460 };

        // Period selector — rebuilds the report for the chosen range. Outside the capture surface so it
        // isn't baked into the PNG export.
        var rangeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        rangeRow.Children.Add(new TextBlock { Text = "Period", VerticalAlignment = VerticalAlignment.Center, FontSize = UmScale.Text.Body, Foreground = Res("TextFillColorSecondaryBrush") });
        var rangeBox = new ComboBox { MinWidth = 180 };
        foreach (var r in DashboardReportHelper.Ranges)
        {
            rangeBox.Items.Add(new ComboBoxItem { Content = r.Label, Tag = r.Days });
        }

        rangeBox.SelectedIndex = 0;
        rangeBox.SelectionChanged += OnRangeChanged; // subscribe AFTER setting the default so it doesn't fire now
        rangeRow.Children.Add(rangeBox);
        body.Children.Add(rangeRow);

        // Everything renderable lives inside one solid-background surface so it can be exported as a PNG.
        _contentStack = new StackPanel { Spacing = 10 };
        _captureSurface = new Border
        {
            Background = Res2("SolidBackgroundFillColorBaseBrush"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Child = _contentStack
        };

        body.Children.Add(new ScrollViewer
        {
            MaxHeight = 360,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _captureSurface
        });

        // "Save as image" (ContentDialog only allows 3 buttons, so this lives in the body).
        var imageButton = new Button { Content = "Save as image (.png)", HorizontalAlignment = HorizontalAlignment.Left };
        imageButton.Click += OnSaveImage;
        body.Children.Add(imageButton);

        Content = body;

        PrimaryButtonClick += OnSaveMarkdown;
        SecondaryButtonClick += OnExportCsv;

        PopulateContent(inputs, report, aiSummary);
    }

    private void OnRangeChanged(object sender, SelectionChangedEventArgs e)
    {
        if ((sender as ComboBox)?.SelectedItem is not ComboBoxItem { Tag: int days })
        {
            return;
        }

        // Rebuild for the new period. The AI-narrated headline is initial-only (avoids a fresh Ollama call per
        // change); range changes show the deterministic summary.
        var inputs = DashboardReportHelper.GatherInputs(_instances, days);
        _report = BusinessReport.Build(inputs);
        PopulateContent(inputs, _report, null);
    }

    /// <summary>Fills the capturable surface with the period label, headline, ranked insights, and trend.</summary>
    private void PopulateContent(ReportInputs inputs, BusinessReportResult report, string? aiSummary) =>
        Populate(_contentStack, inputs, report, aiSummary, _instances);

    /// <summary>
    /// Fills <paramref name="target"/> with the period label, headline, ranked insights and trend chart.
    /// Shared with <c>ReportsPage</c> so the dialog and the page can never drift apart.
    /// </summary>
    internal static void Populate(
        StackPanel target,
        ReportInputs inputs,
        BusinessReportResult report,
        string? aiSummary,
        IReadOnlyList<MessengerInstance> instances)
    {
        var _contentStack = target;
        _contentStack.Children.Clear();

        _contentStack.Children.Add(new TextBlock
        {
            Text = inputs.PeriodLabel,
            FontSize = UmScale.Text.Body,
            Foreground = Res("TextFillColorTertiaryBrush")
        });

        // Headline (AI-narrated when available, with a ✦ AI badge; else the deterministic summary). Grid (not a
        // horizontal StackPanel) so the headline TextBlock has a bounded width and WRAPS instead of clipping.
        var headlineRow = new Grid { ColumnSpacing = 6 };
        headlineRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headlineRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        if (!string.IsNullOrWhiteSpace(aiSummary))
        {
            var badge = new TextBlock { Text = "✦ AI", FontSize = UmScale.Text.Caption, FontWeight = FontWeights.SemiBold, Foreground = Res("UmStatusInfoBrush"), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) };
            Grid.SetColumn(badge, 0);
            headlineRow.Children.Add(badge);
        }

        var headline = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(aiSummary) ? report.Summary : aiSummary,
            FontSize = UmScale.Text.BodyStrong,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        Grid.SetColumn(headline, 1);
        headlineRow.Children.Add(headline);
        _contentStack.Children.Add(headlineRow);

        if (report.Insights.Count == 0)
        {
            _contentStack.Children.Add(new TextBlock
            {
                Text = "Nothing notable in this period — activity looks steady.",
                Foreground = Res("TextFillColorSecondaryBrush"),
                TextWrapping = TextWrapping.WrapWholeWords
            });
        }
        else
        {
            foreach (var insight in report.Insights)
            {
                _contentStack.Children.Add(BuildInsightRow(insight));
            }
        }

        // Response-time trend — median first reply per day over the recent window.
        var trendChart = BuildResponseTrend(ResponseTimeTracker.Instance.GetDailyMedians(instances, 7));
        if (trendChart is not null)
        {
            _contentStack.Children.Add(trendChart);
        }
    }

    private static FrameworkElement BuildInsightRow(BusinessInsight insight)
    {
        var (glyph, brushKey) = insight.Severity switch
        {
            InsightSeverity.Warn => ("", "UmStatusWarningBrush"),
            InsightSeverity.Good => ("", "UmStatusSuccessBrush"),
            _ => ("", "UmStatusInfoBrush"),
        };

        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icon = new FontIcon { Glyph = glyph, FontSize = UmScale.Icon.Md, Foreground = Res(brushKey), VerticalAlignment = VerticalAlignment.Top };
        icon.Margin = new Thickness(0, 2, 0, 0);
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var text = new StackPanel { Spacing = 1 };
        text.Children.Add(new TextBlock { Text = insight.Title, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.WrapWholeWords });
        text.Children.Add(new TextBlock
        {
            Text = insight.Detail,
            FontSize = UmScale.Text.Body,
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
    private static FrameworkElement? BuildResponseTrend(IReadOnlyList<ResponseTimeTracker.DailyResponsePoint> points)
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
                    FontSize = UmScale.Text.Caption,
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
            else
            {
                // A day with no replies measured used to contribute an EMPTY column, which is pixel-for-pixel
                // what a zero-height bar looks like — so "we have no data for Tuesday" and "we replied to
                // nobody on Tuesday" rendered identically. Reply history restarted on 2026-08-28, so six of
                // these seven days have no data at all and the chart read as a catastrophe.
                col.Children.Add(new TextBlock
                {
                    Text = "–",
                    FontSize = UmScale.Text.Caption,
                    Foreground = Res("TextFillColorTertiaryBrush"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 2)
                });
                ToolTipService.SetToolTip(col, $"No replies measured on {p.Label} — not zero, unmeasured.");
            }

            Grid.SetColumn(col, i);
            chart.Children.Add(col);

            var lbl = new TextBlock { Text = p.Label, FontSize = UmScale.Text.Caption, Foreground = Res("TextFillColorTertiaryBrush"), HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetColumn(lbl, i);
            axis.Children.Add(lbl);
        }

        // Say how much of the window was actually measured. "last 7 days" over one measured day is a true
        // statement about the axis and a false impression of the evidence.
        var measured = points.Count(p => p.Count > 0);
        var heading = measured == points.Count
            ? $"Median first reply, last {points.Count} days"
            : $"Median first reply · {measured} of the last {points.Count} days measured";

        var wrap = new StackPanel { Spacing = 2 };
        wrap.Children.Add(new TextBlock { Text = heading, FontSize = UmScale.Text.Body, FontWeight = FontWeights.SemiBold });
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
        catch (Exception ex)
        {
            // The dialog stays open either way, so the owner can retry somewhere writable rather than
            // being left to assume the file saved.
            AppLogger.LogWarning("Report.Save", $"{ex.GetType().Name}: {ex.Message}");
            await ApplicationServiceProvider.Current.Dialog
                .ShowErrorAsync("Couldn't save the report", UserFacingError.Describe("Report.Save", ex))
                .ConfigureAwait(true);
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

    private async void OnSaveImage(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        try
        {
            if (button is not null)
            {
                button.IsEnabled = false;
            }

            var file = await PickSaveFileAsync("Weekly report", "PNG image", ".png");
            if (file is null)
            {
                return;
            }

            // Render the report surface off-screen at its current layout size.
            var rtb = new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap();
            await rtb.RenderAsync(_captureSurface);
            var pixels = await rtb.GetPixelsAsync();

            using var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
            var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
                Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
            var dpi = _captureSurface.XamlRoot?.RasterizationScale ?? 1.0;
            encoder.SetPixelData(
                Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                (uint)rtb.PixelWidth,
                (uint)rtb.PixelHeight,
                96 * dpi,
                96 * dpi,
                pixels.ToArray());
            await encoder.FlushAsync();
        }
        catch
        {
            // best-effort — export failures are non-fatal
        }
        finally
        {
            if (button is not null)
            {
                button.IsEnabled = true;
            }
        }
    }

    internal static async Task<string?> PickSavePathAsync(string suggestedName, string typeName, string extension)
    {
        var file = await PickSaveFileAsync(suggestedName, typeName, extension);
        return file?.Path;
    }

    private static async Task<Windows.Storage.StorageFile?> PickSaveFileAsync(string suggestedName, string typeName, string extension)
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

        return await picker.PickSaveFileAsync();
    }

    // Both went straight to Application.Current.Resources, which resolves the APP-default theme rather than
    // this dialog's — the systemic cause of the white-in-dark surfaces across this app.
    // Static because Populate is: the report body is built by the same code for this dialog and for the
    // Reports page. The no-element overload resolves against the window root, whose ActualTheme is the
    // applied theme, and everything built here is parented into that same window.
    private static SolidColorBrush Res(string key) =>
        Services.ThemeBrushResolver.Resolve(key) as SolidColorBrush
            ?? new SolidColorBrush(Microsoft.UI.Colors.Gray);

    private static Brush Res2(string key) => Services.ThemeBrushResolver.Resolve(key);
}
