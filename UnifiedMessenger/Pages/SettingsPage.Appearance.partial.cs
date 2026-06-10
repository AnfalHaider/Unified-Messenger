using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Pages;

public sealed partial class SettingsPage
{
    private void EnsureAppearanceComboBoxesInitialized()
    {
        if (ThemePreferenceBox.ItemsSource is null)
        {
            ThemePreferenceBox.ItemsSource = new[]
            {
                new ThemePreferenceOption(AppThemePreference.System, "Use system setting"),
                new ThemePreferenceOption(AppThemePreference.Light, "Light"),
                new ThemePreferenceOption(AppThemePreference.Dark, "Dark")
            };
            ThemePreferenceBox.DisplayMemberPath = nameof(ThemePreferenceOption.Label);
        }

        if (PanelDockBox.ItemsSource is null)
        {
            PanelDockBox.ItemsSource = new[]
            {
                new PanelDockOption(NotificationPanelDock.Right, "Right"),
                new PanelDockOption(NotificationPanelDock.Bottom, "Bottom")
            };
            PanelDockBox.DisplayMemberPath = nameof(PanelDockOption.Label);
        }
    }

    private async void ThemePreferenceBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressToggleEvents || ThemePreferenceBox.SelectedItem is not ThemePreferenceOption option)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.ThemePreference = option.Preference);
    }

    private async void PanelDockBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressToggleEvents || PanelDockBox.SelectedItem is not PanelDockOption option)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.PanelDock = option.Dock);

        _services.Navigation.RequestLayoutRefresh();
    }
}
