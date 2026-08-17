using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Dialogs;

/// <summary>
/// Tells the owner, once, that their settings could not be read and the app is running on defaults.
///
/// <para>
/// The wording lives in <see cref="SettingsRecoveryNotice"/> so the copy contract is unit-testable —
/// what it must say, and more importantly what it must never claim. This type is only the presentation:
/// a modal at startup, with an optional "Show me the file" that reveals the preserved copy in Explorer.
/// </para>
/// <para>
/// Modal rather than a banner on purpose. One of the settings that silently reverts is whether updates
/// install without asking, so this is a consent-relevant event; a dismissible strip in the corner of a
/// dashboard is exactly the thing a busy owner scrolls past.
/// </para>
/// </summary>
internal static class SettingsRecoveryDialog
{
    public static async Task ShowAsync(XamlRoot xamlRoot, string? backupPath)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);

        var canReveal = SettingsRecoveryNotice.CanRevealBackup(backupPath);

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = SettingsRecoveryNotice.Title,
            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 360,
                Content = new TextBlock
                {
                    Text = SettingsRecoveryNotice.BuildMessage(backupPath),
                    TextWrapping = TextWrapping.WrapWholeWords,
                    IsTextSelectionEnabled = true,
                    MaxWidth = 420
                }
            },
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close
        };

        if (canReveal)
        {
            dialog.PrimaryButtonText = "Show me the file";
            dialog.DefaultButton = ContentDialogButton.Close;
        }

        var result = await dialog.ShowManagedAsync();

        // Logged after the await, not before, so the record distinguishes "the notice was shown and the
        // owner dismissed it" from "the call returned without ever displaying". During the live test the
        // pre-call line appeared and the dialog never reached the screen, and there was no way to tell
        // which of those had happened.
        AppLogger.LogInfo("Settings.Recovery", $"Notice closed with '{result}' (reveal offered: {canReveal}).");

        if (result == ContentDialogResult.Primary && canReveal)
        {
            RevealInExplorer(backupPath!);
        }
    }

    private static void RevealInExplorer(string path)
    {
        try
        {
            // /select, opens the folder with the file highlighted rather than trying to open a .bak,
            // which has no handler and would raise Windows' "how do you want to open this?" picker.
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path.Trim()}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            // Failing to open a folder must not turn an informational notice into an error.
            AppLogger.LogWarning("Settings.Recovery", $"Could not reveal the preserved file: {ex.Message}");
        }
    }
}
