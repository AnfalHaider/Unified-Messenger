using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Pages;

public sealed partial class SettingsPage
{
    private void RefreshBranchOperationalCatalog(AppSettings settings)
    {
        _viewModel.BranchOperationalCatalogRows.Clear();
        foreach (var row in SettingsPageHelper.BuildBranchOperationalCatalogRows(settings.BranchOperationalCatalog))
        {
            _viewModel.BranchOperationalCatalogRows.Add(row);
        }

        BranchOperationalCatalogList.ItemsSource = _viewModel.BranchOperationalCatalogRows;
    }

    private void RefreshPlatformModules(AppSettings settings)
    {
        _viewModel.PlatformModuleRows.Clear();
        foreach (var row in PlatformModuleSettingsHelper.BuildToggleRows(settings))
        {
            _viewModel.PlatformModuleRows.Add(row);
        }

        PlatformModulesList.ItemsSource = _viewModel.PlatformModuleRows;
    }

    private async void PlatformModuleToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents ||
            sender is not ToggleSwitch { Tag: string platformId, IsOn: var isEnabled })
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            PlatformModuleSettingsHelper.SetPlatformEnabled(settings, platformId, isEnabled));
    }

    private async void BranchCatalogField_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents ||
            sender is not TextBox { Tag: string branchKey })
        {
            return;
        }

        var row = _viewModel.BranchOperationalCatalogRows
            .FirstOrDefault(entry => entry.BranchKey.Equals(branchKey, StringComparison.OrdinalIgnoreCase));
        if (row is null)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
        {
            var index = settings.BranchOperationalCatalog.FindIndex(profile =>
                profile.BranchKey.Equals(branchKey, StringComparison.OrdinalIgnoreCase));
            var updated = SettingsPageHelper.ToBranchOperationalProfile(row);

            if (index >= 0)
            {
                settings.BranchOperationalCatalog[index] = updated;
            }
            else
            {
                settings.BranchOperationalCatalog.Add(updated);
            }
        });
    }

    private async void SlaThresholdBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressToggleEvents || double.IsNaN(args.NewValue))
        {
            return;
        }

        var minutes = SettingsPageHelper.NormalizeSlaThresholdMinutes(args.NewValue);
        if (SettingsPageHelper.RequiresNumberBoxCorrection(minutes, args.NewValue))
        {
            sender.Value = minutes;
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.SlaThresholdMinutes = minutes);

        _services.Navigation.RequestDashboardRefresh();
    }

    private async void DashboardUrgencyThresholdBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressToggleEvents || double.IsNaN(args.NewValue))
        {
            return;
        }

        var value = (int)Math.Round(args.NewValue, MidpointRounding.AwayFromZero);
        await _services.AppSettings.UpdateAsync(settings =>
            settings.DashboardUrgencyThreshold = value);
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

    private async void ShowHeuristicExecutiveInsightsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.ShowHeuristicExecutiveInsights = ShowHeuristicExecutiveInsightsToggle.IsOn);
    }
}
