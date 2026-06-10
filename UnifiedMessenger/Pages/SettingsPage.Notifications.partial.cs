using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Pages;

public sealed partial class SettingsPage
{
    private void EnsureNotificationsComboBoxesInitialized()
    {
        if (PanelAutoOpenBox.ItemsSource is null)
        {
            PanelAutoOpenBox.ItemsSource = new[]
            {
                new PanelAutoOpenOption(
                    NotificationPanelAutoOpenMode.UnfocusedOnly,
                    "When app is unfocused or in background"),
                new PanelAutoOpenOption(NotificationPanelAutoOpenMode.Always, "Always on new alerts"),
                new PanelAutoOpenOption(NotificationPanelAutoOpenMode.Never, "Never auto-open")
            };
            PanelAutoOpenBox.DisplayMemberPath = nameof(PanelAutoOpenOption.Label);
        }

        if (ToastSoundBox.ItemsSource is null)
        {
            ToastSoundBox.ItemsSource = new[]
            {
                new ToastSoundOption(ToastSoundPreference.Default, "Default"),
                new ToastSoundOption(ToastSoundPreference.Silent, "Silent")
            };
            ToastSoundBox.DisplayMemberPath = nameof(ToastSoundOption.Label);
        }
    }

    private async void BackgroundToastsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.EnableBackgroundToasts = BackgroundToastsToggle.IsOn);
    }

    private async void TaskbarBadgeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.ShowTaskbarBadge = TaskbarBadgeToggle.IsOn);

        _ = TaskbarBadgeService.Instance.SyncBadgeAsync(_services.NotificationHub.TotalUnreadCount);
    }

    private async void PanelAutoOpenBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressToggleEvents || PanelAutoOpenBox.SelectedItem is not PanelAutoOpenOption option)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.PanelAutoOpen = option.Mode);
    }

    private async void ToastSoundBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressToggleEvents || ToastSoundBox.SelectedItem is not ToastSoundOption option)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.ToastSound = option.Preference);
    }

    private async void IncludeMutedBadgesToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.IncludeMutedChatBadges = IncludeMutedBadgesToggle.IsOn);

        await _services.SessionManager.BroadcastAdapterSettingsAsync();
    }

    private async void ToastGroupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.ToastGroupByInstance = ToastGroupToggle.IsOn);
    }

    private async void ToastBrandingToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.ToastUsePlatformBranding = ToastBrandingToggle.IsOn);
    }

    private async void ClearNotificationsButton_Click(object sender, RoutedEventArgs e)
    {
        var confirm = new ContentDialog
        {
            Title = "Clear notification history?",
            Content = "This removes all stored notification alerts. Unread badges on sidebar icons will reset.",
            PrimaryButtonText = "Clear",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _services.NotificationHub.ClearAlerts();
    }
}
