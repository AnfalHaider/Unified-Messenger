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

    private async void UseStoreBridgeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.UseStoreBridge = UseStoreBridgeToggle.IsOn);

        RefreshStoreBridgeHealth();
    }

    /// <summary>
    /// Shows which WhatsApp reader is actually live. The fast reader falls back silently on purpose — a
    /// WhatsApp change should cost previews, not metrics — so this line is the only place the degradation
    /// becomes visible.
    /// </summary>
    private void RefreshStoreBridgeHealth()
    {
        StoreBridgeHealthText.Text = UseStoreBridgeToggle.IsOn
            ? StoreBridgeHealth.Describe()
            : "Turned off — using the saved-copy reader, so previews are limited to the chats WhatsApp has on screen.";

        // Independent of the toggle above: whichever reader is live, the app still has to find things on
        // WhatsApp's page, and this is the only place a customer can see that it still can.
        SelectorHealthText.Text = SelectorHealth.Describe();

        // And independent of BOTH: a signed-out account reports perfect selector health, because nothing
        // is failing — it is simply not being read. Neither line above can say that, so an account whose
        // session expired looks healthy on this screen while contributing nothing to any figure.
        SignInHealthText.Text =
            SignInGate.DescribeSignedOut(_services.Registry.Instances)
            ?? "Every account is signed in.";
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

        if (await confirm.ShowManagedAsync() != ContentDialogResult.Primary)
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
            await ShowMessageDialogAsync("Backup failed", UserFacingError.Describe("Settings.Backup", ex));
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

        try
        {
            if (!LocalBackupService.Instance.IsRecognisedBackup(file.Path))
            {
                await ShowMessageDialogAsync("Restore failed", "This file isn't a recognised Unified Messenger backup.");
                return;
            }
        }
        catch (Exception ex)
        {
            // Distinct from "not a backup" on purpose: a locked or unreadable file is the owner's genuine
            // backup, and telling them it is not one invites them to delete it.
            await ShowMessageDialogAsync("Couldn't open that file", UserFacingError.Describe("Settings.OpenFile", ex));
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

        if (await confirm.ShowManagedAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            var restored = await LocalBackupService.Instance.RestoreAsync(file.Path);

            // The restore replaced the files on disk, but every store is still live in memory holding the
            // pre-restore state — and the shutdown flush would write that straight back over all ten of
            // them. Asking the owner to restart was not enough: closing the app normally is exactly what
            // triggered the overwrite, so "Restore complete" was followed by silently getting the old data
            // back. Suppress the flush, then close ourselves so there is no window in which a normal exit
            // can undo the restore.
            ApplicationLifecycleService.SuppressPersistentStateFlush();

            await ShowMessageDialogAsync(
                "Restore complete",
                $"Restored {restored} file(s). Unified Messenger will now close — reopen it to load the restored data.");

            // Same exit path the updater uses after staging its installer (GitHubUpdateService).
            // Deliberately not relaunching: the single-instance mutex is still held by this process, so a
            // relaunch here would race it and exit silently with no window.
            DispatcherQueue.TryEnqueue(Application.Current.Exit);
        }
        catch (Exception ex)
        {
            await ShowMessageDialogAsync("Restore failed", UserFacingError.Describe("Settings.Restore", ex));
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

        if (await preExportDialog.ShowManagedAsync() != ContentDialogResult.Primary)
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
            await ShowMessageDialogAsync("Export failed", UserFacingError.Describe("Settings.Export", ex));
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

        if (await confirm.ShowManagedAsync() != ContentDialogResult.Primary)
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
            await ShowMessageDialogAsync("Import failed", UserFacingError.Describe("Settings.Import", ex));
        }
    }
}
