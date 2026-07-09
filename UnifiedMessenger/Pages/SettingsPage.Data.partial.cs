using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Presenters;
using UnifiedMessenger.Services;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace UnifiedMessenger.Pages;

public sealed partial class SettingsPage
{
    private void UpdateImportExportPanelVisibility(bool isVisible)
    {
        _viewModel.ShowImportExportPanel = isVisible;
        ImportExportPanel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void ClearAnalyticsButton_Click(object sender, RoutedEventArgs e)
    {
        var confirm = new ContentDialog
        {
            Title = "Clear operational data?",
            Content = "This permanently removes message analytics and saved thread/triage state used by the Operations Command Center.",
            PrimaryButtonText = "Clear",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        await OperationalDataService.ClearAllAsync();
        _services.Navigation.RequestDashboardRefresh();
    }

    private async void BackupDataButton_Click(object sender, RoutedEventArgs e)
    {
        var entryCount = LocalBackupService.Instance.CountBackupEntries();
        if (entryCount == 0)
        {
            await ShowMessageDialogAsync("Nothing to back up", "No local data was found to back up yet.");
            return;
        }

        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = $"UnifiedMessenger-backup-{DateTime.Now:yyyy-MM-dd}",
            FileTypeChoices = { { "Backup archive", [".zip"] } }
        };

        InitializePicker(picker);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            var written = await LocalBackupService.Instance.CreateBackupAsync(file.Path);
            await ShowMessageDialogAsync("Backup complete", $"Saved {written} file(s) to {file.Path}");
        }
        catch (Exception ex)
        {
            await ShowMessageDialogAsync("Backup failed", ex.Message);
        }
    }

    private async void RestoreDataButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            FileTypeFilter = { ".zip" }
        };

        InitializePicker(picker);

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        if (!LocalBackupService.Instance.IsRecognisedBackup(file.Path))
        {
            await ShowMessageDialogAsync("Restore failed", "This file isn't a recognised Unified Messenger backup.");
            return;
        }

        var confirm = new ContentDialog
        {
            Title = "Restore from backup?",
            Content = "This replaces your current settings, accounts, analytics and custom icons with the contents of the backup. Sign-in sessions are untouched. The app will need to restart.",
            PrimaryButtonText = "Restore",
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
            var restored = await LocalBackupService.Instance.RestoreAsync(file.Path);
            await ShowMessageDialogAsync(
                "Restore complete",
                $"Restored {restored} file(s). Please restart Unified Messenger to load the restored data.");
        }
        catch (Exception ex)
        {
            await ShowMessageDialogAsync("Restore failed", ex.Message);
        }
    }

    private async void ExportInstancesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_registry is null)
        {
            return;
        }

        var summary = SettingsImportExportPresenter.BuildExportSummary(
            _registry.Instances,
            _registry.ArchivedInstances,
            _registry.StorePath);

        var preExportDialog = new ContentDialog
        {
            Title = "Export instances?",
            Content = SettingsImportExportPresenter.BuildPreExportDialogContent(summary),
            PrimaryButtonText = "Choose file",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await preExportDialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = "instances",
            FileTypeChoices = { { "Instances JSON", [".json"] } }
        };

        InitializePicker(picker);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            await _registry.ExportInstancesAsync(file.Path);
            await ShowMessageDialogAsync("Export complete", $"Saved to {file.Path}");
        }
        catch (Exception ex)
        {
            await ShowMessageDialogAsync("Export failed", ex.Message);
        }
    }

    private async void ImportInstancesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_registry is null)
        {
            return;
        }

        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            FileTypeFilter = { ".json" }
        };

        InitializePicker(picker);

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        SettingsImportSummary importSummary;
        try
        {
            await using var stream = File.OpenRead(file.Path);
            var imported = await System.Text.Json.JsonSerializer
                .DeserializeAsync<InstanceStore>(stream)
                .ConfigureAwait(true)
                ?? throw new InvalidDataException("Import file is empty or invalid.");

            importSummary = SettingsImportExportPresenter.BuildImportSummary(file.Path, imported);
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException)
        {
            await ShowMessageDialogAsync("Import failed", "Import file is not valid JSON.");
            return;
        }

        var confirm = new ContentDialog
        {
            Title = "Import instances?",
            Content = SettingsImportExportPresenter.BuildImportDialogContent(importSummary),
            PrimaryButtonText = "Import",
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
            var result = await _registry.ImportInstancesAsync(file.Path);
            RefreshArchivedAccounts();
            RefreshStoragePaths();
            _services.Navigation.RequestInstanceRegistryRefresh();
            await ShowMessageDialogAsync(
                "Import complete",
                SettingsPageHelper.BuildImportSuccessMessage(result.ActiveCount, result.ArchivedCount));
        }
        catch (Exception ex)
        {
            await ShowMessageDialogAsync("Import failed", ex.Message);
        }
    }
}
