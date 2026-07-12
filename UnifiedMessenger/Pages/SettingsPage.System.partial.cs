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

}
