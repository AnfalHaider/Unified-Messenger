using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Dialogs;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Pages;

public sealed partial class SettingsPage
{
    private async void RunInBackgroundOnCloseToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.RunInBackgroundOnClose = RunInBackgroundOnCloseToggle.IsOn);
    }

    private async void LaunchAtStartupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        try
        {
            StartupTaskService.SetRegistered(LaunchAtStartupToggle.IsOn);
            await _services.AppSettings.UpdateAsync(settings =>
                settings.LaunchAtStartup = LaunchAtStartupToggle.IsOn);
        }
        catch (Exception ex)
        {
            _suppressToggleEvents = true;
            LaunchAtStartupToggle.IsOn = StartupTaskService.IsRegisteredForCurrentExecutable();
            _suppressToggleEvents = false;
            await ShowMessageDialogAsync("Startup registration failed", ex.Message);
        }
    }

    private async void PromptPinToTaskbarToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.PromptPinToTaskbar = PromptPinToTaskbarToggle.IsOn);
    }

    private async void EnableAutoUpdateToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.EnableAutoUpdate = EnableAutoUpdateToggle.IsOn);
    }

    private async void PromptBeforeAutoUpdateToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.PromptBeforeAutoUpdate = PromptBeforeAutoUpdateToggle.IsOn);
    }

    private async void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await _services.GitHubUpdate.CheckForUpdatesManualAsync();

        // When an update is available, offer to install it — not just report the version.
        if (result.Status == UpdateCheckStatus.UpdateAvailable &&
            !string.IsNullOrWhiteSpace(result.DownloadUrl) &&
            result.LatestVersion is not null)
        {
            var prompt = new AutoUpdateDialog(
                result.CurrentVersion?.ToString() ?? "this version",
                result.LatestVersion.ToString())
            {
                XamlRoot = XamlRoot
            };

            if (await prompt.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            try
            {
                // Downloads, verifies SHA-256, launches the installer, and exits the app.
                await _services.GitHubUpdate.ApplyUpdateAsync(result);
            }
            catch (Exception ex)
            {
                await new ContentDialog
                {
                    Title = "Update failed",
                    CloseButtonText = "OK",
                    XamlRoot = XamlRoot,
                    Content = new TextBlock
                    {
                        Text = $"Could not install the update: {ex.Message}",
                        TextWrapping = TextWrapping.WrapWholeWords
                    }
                }.ShowAsync();
            }

            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Update check",
            CloseButtonText = "OK",
            XamlRoot = XamlRoot
        };

        dialog.Content = new TextBlock
        {
            Text = SettingsPageHelper.BuildUpdateCheckMessage(result),
            TextWrapping = TextWrapping.WrapWholeWords
        };

        await dialog.ShowAsync();
    }
}
