using UnifiedMessenger.Models;
using UnifiedMessenger.Presenters;
using UnifiedMessenger.Services;
using UnifiedMessenger.ViewModels;

namespace UnifiedMessenger.Tests;

public class Wave9PersonalSettingsTests
{
    [Fact]
    public void PersonalOverviewViewModel_ApplyViewState_ShowsNoAccountsEmptyState()
    {
        var viewModel = new PersonalOverviewViewModel();
        var viewState = PersonalSnapshotPresenter.BuildViewState(new PersonalDashboardSnapshot
        {
            EmptyReason = PersonalDashboardEmptyReason.NoPersonalAccounts
        });

        viewModel.ApplyViewState(viewState);

        Assert.True(viewModel.ShowNoAccountsEmptyState);
        Assert.False(viewModel.ShowToolbar);
        Assert.False(viewModel.ShowContent);
        Assert.Empty(viewModel.ActivityItems);
        Assert.Empty(viewModel.TileItems);
    }

    [Fact]
    public void PersonalOverviewSearchPresenter_FiltersPersonalAccounts()
    {
        var instances = new[]
        {
            new MessengerInstance
            {
                Id = "p-1",
                DisplayName = "Family WhatsApp",
                Platform = "whatsapp",
                Category = WorkspaceCategory.Personal
            }
        };

        var suggestions = PersonalOverviewSearchPresenter.BuildSuggestions(instances, [], "family");

        Assert.Single(suggestions);
        Assert.Equal("p-1", suggestions[0].InstanceId);
        Assert.Equal("Family WhatsApp", suggestions[0].Label);
    }

    [Fact]
    public void SettingsNavigationHelper_BuildsSectionNavInInformationArchitectureOrder()
    {
        var items = SettingsNavigationHelper.BuildSectionNavItems();

        Assert.Equal(12, items.Count);
        Assert.Equal(SettingsNavigationHelper.NotificationsSectionKey, items[0].Key);
        Assert.Equal(SettingsNavigationHelper.PlatformModulesSectionKey, items[3].Key);
        Assert.Equal(SettingsNavigationHelper.KeyboardShortcutsSectionKey, items[6].Key);
        Assert.Equal(SettingsNavigationHelper.AboutSectionKey, items[^1].Key);
        Assert.Equal("Settings › Local AI", SettingsNavigationHelper.BuildBreadcrumb("Local AI"));
    }

    [Fact]
    public void SettingsArchivedAccountsPresenter_BuildsRichRowsWithProfileLine()
    {
        var rows = SettingsArchivedAccountsPresenter.BuildRows([
            new MessengerInstance
            {
                Id = "arch-1",
                DisplayName = "Sales WhatsApp",
                Platform = "whatsapp",
                ProfileName = "sales-wa"
            }
        ]);

        Assert.Single(rows);
        Assert.Equal("arch-1", rows[0].InstanceId);
        Assert.Equal("WhatsApp", rows[0].PlatformLabel);
        Assert.Contains("sales-wa", rows[0].ProfileLine, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("\uE8BD", rows[0].IconGlyph);
    }

    [Fact]
    public void SettingsImportExportPresenter_BuildsPreExportAndImportCopy()
    {
        var exportSummary = SettingsImportExportPresenter.BuildExportSummary(
            [new MessengerInstance { Id = "a" }],
            [new MessengerInstance { Id = "b" }],
            @"C:\data\instances.json");

        var exportDialog = SettingsImportExportPresenter.BuildPreExportDialogContent(exportSummary);
        var importDialog = SettingsImportExportPresenter.BuildImportDialogContent(
            new SettingsImportSummary(2, 1, @"C:\import\instances.json"),
            createBackup: true);

        Assert.Contains("1 active", exportDialog, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1 archived", exportDialog, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("backup", importDialog, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2 active", importDialog, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SettingsPageHelper_BuildPermanentDeleteConfirmation_UsesDisplayName()
    {
        var message = SettingsPageHelper.BuildPermanentDeleteConfirmation("Sales WhatsApp");

        Assert.Contains("Sales WhatsApp", message, StringComparison.Ordinal);
        Assert.Contains("cannot be undone", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LocalAISettingsViewModel_ModelManagerVisibilityFollowsEnableLocalAi()
    {
        var viewModel = new LocalAISettingsViewModel
        {
            EnableLocalAi = false,
            ShowModelManager = true
        };

        Assert.False(viewModel.IsModelManagerVisible);

        viewModel.EnableLocalAi = true;
        Assert.True(viewModel.IsModelManagerVisible);
    }
}
