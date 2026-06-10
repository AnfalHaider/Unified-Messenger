using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Presenters;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Pages;

public sealed partial class SettingsPage
{
    private void RefreshArchivedAccounts()
    {
        if (_registry is null)
        {
            _viewModel.ArchivedAccounts.Clear();
            ArchivedAccountsList.ItemsSource = null;
            _viewModel.ShowNoArchivedAccounts = true;
            NoArchivedAccountsText.Visibility = Visibility.Visible;
            return;
        }

        var rows = SettingsArchivedAccountsPresenter.BuildRows(_registry.ArchivedInstances);
        _viewModel.ArchivedAccounts.Clear();
        foreach (var row in rows)
        {
            _viewModel.ArchivedAccounts.Add(row);
        }

        ArchivedAccountsList.ItemsSource = _viewModel.ArchivedAccounts;
        _viewModel.ShowNoArchivedAccounts = rows.Count == 0;
        NoArchivedAccountsText.Visibility = _viewModel.ShowNoArchivedAccounts
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void RestoreAccountButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string instanceId } &&
            ShellNavigationService.IsValidInstanceId(instanceId))
        {
            _services.Navigation.RequestArchivedInstanceRestore(instanceId);
        }
    }

    private async void PermanentDeleteAccountButton_Click(object sender, RoutedEventArgs e)
    {
        if (_registry is null ||
            sender is not Button { Tag: string instanceId } ||
            !ShellNavigationService.IsValidInstanceId(instanceId))
        {
            return;
        }

        var row = _viewModel.ArchivedAccounts
            .FirstOrDefault(account =>
                account.InstanceId.Equals(instanceId, StringComparison.OrdinalIgnoreCase));

        var confirm = new ContentDialog
        {
            Title = "Delete account permanently?",
            Content = SettingsPageHelper.BuildPermanentDeleteConfirmation(row?.DisplayName),
            PrimaryButtonText = "Delete permanently",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            var instance = InstanceDeletionService.ResolveInstance(_registry, instanceId);
            if (instance is null)
            {
                await ShowMessageDialogAsync("Delete failed", "Account not found.");
                return;
            }

            await InstanceDeletionService.DeleteAsync(
                _services,
                instance,
                DeleteInstanceChoice.PermanentDelete);

            RefreshArchivedAccounts();
            RefreshStoragePaths();
            _services.Navigation.RequestInstanceRegistryRefresh();
        }
        catch (Exception ex)
        {
            await ShowMessageDialogAsync("Delete failed", ex.Message);
        }
    }
}
