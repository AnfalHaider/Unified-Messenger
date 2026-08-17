using Microsoft.Web.WebView2.Core;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace UnifiedMessenger.Services;

/// <summary>
/// Lets the owner choose where a received file is saved, using the operating system's own save dialog.
///
/// <para>
/// <b>Why this replaces WebView2's own flow.</b> Leaving <c>Handled = false</c> gave the built-in download
/// button and dropped every file into the browser's default folder — which for an unpackaged WebView2 is a
/// path the owner never chose and cannot easily find. A salon owner saving a customer's reference photo
/// wants it in a folder they picked, named something they will recognise.
/// </para>
/// <para>
/// <b>The deferral is the whole trick.</b> <c>DownloadStarting</c> is a synchronous event: the moment the
/// handler returns, WebView2 acts on whatever <see cref="CoreWebView2DownloadStartingEventArgs.ResultFilePath"/>
/// says. A file picker is asynchronous, so without <c>GetDeferral()</c> the download would already have
/// started to the default location before the owner had finished choosing. The deferral holds the event open
/// until the picker closes, and must be completed on every path — including cancellation and failure — or
/// the WebView stalls with a download that never resolves.
/// </para>
/// </summary>
public static class DownloadLocationPrompt
{
    /// <summary>
    /// Attaches the prompt to a WebView. Safe to call repeatedly; the previous handler is removed first.
    /// </summary>
    public static void Attach(CoreWebView2 coreWebView)
    {
        ArgumentNullException.ThrowIfNull(coreWebView);

        coreWebView.DownloadStarting -= OnDownloadStarting;
        coreWebView.DownloadStarting += OnDownloadStarting;
    }

    private static void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs args)
    {
        var settings = AppSettingsService.Instance.Settings;

        if (!settings.AskWhereToSaveDownloads)
        {
            // Straight to the configured folder, keeping WebView2's own progress UI. Only the directory is
            // overridden — the filename the server or page suggested is preserved.
            var folder = ResolveDefaultFolder(settings.DownloadFolder);
            if (folder is not null)
            {
                args.ResultFilePath = Path.Combine(folder, Path.GetFileName(args.ResultFilePath));
            }

            args.Handled = false;
            return;
        }

        // Suppress the built-in flyout — the owner is about to be asked instead.
        args.Handled = true;

        _ = PromptAsync(args, settings.DownloadFolder);
    }

    private static async Task PromptAsync(
        CoreWebView2DownloadStartingEventArgs args,
        string? lastFolder)
    {
        // Taken here and named with var deliberately: the concrete deferral type lives in whichever
        // WebView2 assembly the Windows App SDK resolves, and spelling it out couples this file to that
        // version for no benefit.
        var deferral = args.GetDeferral();

        try
        {
            var suggested = Path.GetFileName(args.ResultFilePath);
            var picker = new FileSavePicker
            {
                SuggestedFileName = string.IsNullOrWhiteSpace(suggested) ? "download" : suggested,
                SuggestedStartLocation = PickerLocationId.Downloads
            };

            // The picker refuses to open with no file-type choices, and it must offer the type the file
            // actually is — otherwise it silently appends the wrong extension to a customer's photo.
            var extension = Path.GetExtension(suggested);
            if (string.IsNullOrWhiteSpace(extension))
            {
                picker.FileTypeChoices.Add("All files", ["."]);
            }
            else
            {
                picker.FileTypeChoices.Add(DescribeExtension(extension), [extension]);
                picker.FileTypeChoices.Add("All files", ["."]);
            }

            if (App.CurrentWindow is { } window)
            {
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
            }

            var file = await picker.PickSaveFileAsync();

            if (file is null)
            {
                // Cancelling the dialog cancels the download. Anything else would save a file to a location
                // the owner just declined to choose.
                args.Cancel = true;
                AppLogger.LogInfo("Download", "The owner cancelled the save dialog, so the download was cancelled.");
                return;
            }

            args.ResultFilePath = file.Path;
            RememberFolder(file.Path, lastFolder);
            AppLogger.LogInfo("Download", $"Saving '{Path.GetFileName(file.Path)}' to the chosen folder.");
        }
        catch (Exception ex)
        {
            // A picker that cannot open must not leave the download hanging, and must not silently drop the
            // file somewhere the owner did not ask for either. Cancel and say why.
            args.Cancel = true;
            AppLogger.LogWarning(
                "Download",
                $"Could not show the save dialog, so the download was cancelled: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            // On every path. An uncompleted deferral leaves the WebView waiting on a download that never
            // resolves, which looks to the owner like the app has frozen mid-save.
            deferral.Complete();
        }
    }

    /// <summary>
    /// Remembers the folder so the next save starts where the last one ended — the behaviour every browser
    /// has, and the difference between one click and re-navigating a folder tree per file.
    /// </summary>
    private static void RememberFolder(string chosenPath, string? current)
    {
        var folder = Path.GetDirectoryName(chosenPath);
        if (string.IsNullOrWhiteSpace(folder) ||
            string.Equals(folder, current, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _ = AppSettingsService.Instance.UpdateAsync(s => s.DownloadFolder = folder);
    }

    /// <summary>The configured folder if it still exists, otherwise null so WebView2 uses its own default.</summary>
    private static string? ResolveDefaultFolder(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        try
        {
            // A folder that has been moved, renamed or was on a disconnected drive must not silently fail
            // the download — fall back rather than throw from an event handler.
            return Directory.Exists(configured) ? configured : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>A readable file-type name for the picker, so it does not read ".jpg files".</summary>
    internal static string DescribeExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".heic" => "Image",
        ".mp4" or ".mov" or ".webm" or ".3gp" or ".avi" => "Video",
        ".mp3" or ".ogg" or ".opus" or ".m4a" or ".wav" or ".aac" => "Audio",
        ".pdf" => "PDF document",
        ".doc" or ".docx" => "Word document",
        ".xls" or ".xlsx" or ".csv" => "Spreadsheet",
        ".ppt" or ".pptx" => "Presentation",
        ".zip" or ".rar" or ".7z" => "Archive",
        ".vcf" => "Contact card",
        ".txt" => "Text file",
        _ => $"{extension.TrimStart('.').ToUpperInvariant()} file"
    };
}
