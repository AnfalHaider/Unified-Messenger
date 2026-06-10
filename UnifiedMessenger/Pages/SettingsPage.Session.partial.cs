using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Pages;

public sealed partial class SettingsPage
{
    private void EnsureSessionComboBoxesInitialized()
    {
        if (StartupWarmModeBox.ItemsSource is null)
        {
            StartupWarmModeBox.ItemsSource = new[]
            {
                new StartupWarmModeOption(StartupWarmMode.WarmAll, "Warm all"),
                new StartupWarmModeOption(StartupWarmMode.VisibleOnly, "Visible only"),
                new StartupWarmModeOption(StartupWarmMode.Lazy, "Lazy")
            };
            StartupWarmModeBox.DisplayMemberPath = nameof(StartupWarmModeOption.Label);
        }
    }

    private async void StartupWarmModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressToggleEvents || StartupWarmModeBox.SelectedItem is not StartupWarmModeOption option)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.StartupWarmMode = option.Mode);
    }

    private async void MaxConcurrentWebViewsBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressToggleEvents || double.IsNaN(args.NewValue))
        {
            return;
        }

        var value = SettingsPageHelper.NormalizeMaxConcurrentWebViews(args.NewValue);
        if (SettingsPageHelper.RequiresNumberBoxCorrection(value, args.NewValue))
        {
            sender.Value = value;
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.MaxConcurrentWebViews = value);
    }

    private async void RefreshAllWebViewsButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshAllWebViewsButton.IsEnabled = false;
        var originalContent = RefreshAllWebViewsButton.Content;
        RefreshAllWebViewsButton.Content = "Refreshing…";
        try
        {
            await _services.SessionManager.ReloadAllSessionsAsync();
        }
        catch (Exception ex)
        {
            await ShowMessageDialogAsync("WebView refresh failed", ex.Message);
        }
        finally
        {
            RefreshAllWebViewsButton.IsEnabled = true;
            RefreshAllWebViewsButton.Content = originalContent;
        }
    }

    private async void EnableLazyWebViewLoadingToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.EnableLazyWebViewLoading = EnableLazyWebViewLoadingToggle.IsOn);
    }

    private async void EnablePerInstanceSleepUnloadToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.EnablePerInstanceSleepUnload = EnablePerInstanceSleepUnloadToggle.IsOn);
    }

    private async void EnableEditInstanceMetadataToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.EnableEditInstanceMetadata = EnableEditInstanceMetadataToggle.IsOn);
    }

    private async void EnableImportExportInstancesToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.EnableImportExportInstances = EnableImportExportInstancesToggle.IsOn);

        UpdateImportExportPanelVisibility(EnableImportExportInstancesToggle.IsOn);
    }

    private async void EnableInstanceNotesTagsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.EnableInstanceNotesTags = EnableInstanceNotesTagsToggle.IsOn);
    }
}
