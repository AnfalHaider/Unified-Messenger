using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Pages;

public sealed partial class SettingsPage
{
    private void EnsureMetricsControlsInitialized()
    {
        if (WhatsAppBackfillModeBox.ItemsSource is null)
        {
            WhatsAppBackfillModeBox.ItemsSource = new[]
            {
                new WhatsAppBackfillModeOption(WhatsAppBackfillMode.Unread, "Unread only"),
                new WhatsAppBackfillModeOption(WhatsAppBackfillMode.Recent, "Recent (last N days)"),
                new WhatsAppBackfillModeOption(WhatsAppBackfillMode.All, "All chats (capped)")
            };
            WhatsAppBackfillModeBox.DisplayMemberPath = nameof(WhatsAppBackfillModeOption.Label);
        }
    }

    private async void EnableStartupBackfillToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.EnableStartupBackfill = EnableStartupBackfillToggle.IsOn);
    }

    private async void WhatsAppBackfillModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressToggleEvents || WhatsAppBackfillModeBox.SelectedItem is not WhatsAppBackfillModeOption option)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.WhatsAppBackfillMode = option.Mode);
    }

    private async void WhatsAppBackfillMaxChatsBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressToggleEvents || double.IsNaN(args.NewValue))
        {
            return;
        }

        var value = (int)Math.Clamp(Math.Round(args.NewValue), 5, 100);
        if (SettingsPageHelper.RequiresNumberBoxCorrection(value, args.NewValue))
        {
            sender.Value = value;
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.WhatsAppBackfillMaxChats = value);
    }

    private async void WhatsAppBackfillRecentDaysBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressToggleEvents || double.IsNaN(args.NewValue))
        {
            return;
        }

        var value = (int)Math.Clamp(Math.Round(args.NewValue), 1, 30);
        if (SettingsPageHelper.RequiresNumberBoxCorrection(value, args.NewValue))
        {
            sender.Value = value;
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.WhatsAppBackfillRecentDays = value);
    }

    private async void EnableDeepBackfillToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.EnableDeepBackfill = EnableDeepBackfillToggle.IsOn);
    }

    private async void OccCompactCardDensityToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.OccCompactCardDensity = OccCompactCardDensityToggle.IsOn);
    }
}
