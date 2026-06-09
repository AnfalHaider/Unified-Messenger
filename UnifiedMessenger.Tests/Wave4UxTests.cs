using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class Wave4UxTests
{
    [Fact]
    public void PersonalTileMatches_FiltersByDisplayNamePlatformOrDetail()
    {
        Assert.True(DashboardPageHelper.PersonalTileMatches(
            "Family WhatsApp",
            "WhatsApp",
            "3 unread",
            "family"));

        Assert.False(DashboardPageHelper.PersonalTileMatches(
            "Family WhatsApp",
            "WhatsApp",
            "3 unread",
            "telegram"));
    }

    [Fact]
    public void BuildViewState_FiltersInstanceTilesBySearchQuery()
    {
        var snapshot = new PersonalDashboardSnapshot
        {
            PersonalAccountCount = 2,
            EmptyReason = PersonalDashboardEmptyReason.HasData,
            InstanceTiles =
            [
                new PersonalInstanceTileDisplay
                {
                    InstanceId = "a",
                    DisplayName = "Family WhatsApp",
                    PlatformLabel = "WhatsApp",
                    DetailLine = "Connected",
                    ConnectionStatusLabel = "Connected",
                    ConnectionColorHex = "#107C10",
                    IconGlyph = "\uE8BD",
                    AccentColorHex = "#25D366"
                },
                new PersonalInstanceTileDisplay
                {
                    InstanceId = "b",
                    DisplayName = "Work Telegram",
                    PlatformLabel = "Telegram",
                    DetailLine = "Connected",
                    ConnectionStatusLabel = "Connected",
                    ConnectionColorHex = "#107C10",
                    IconGlyph = "\uE8BD",
                    AccentColorHex = "#0088CC"
                }
            ]
        };

        var viewState = PersonalDashboardPresentationHelper.BuildViewState(snapshot, "telegram");

        Assert.Single(viewState.InstanceTiles);
        Assert.Equal("Work Telegram", viewState.InstanceTiles[0].DisplayName);
        Assert.False(viewState.ShowInstanceTilesEmptyState);
    }

    [Fact]
    public void BuildViewState_ShowsSearchEmptyStateWhenNoTilesMatch()
    {
        var snapshot = new PersonalDashboardSnapshot
        {
            PersonalAccountCount = 1,
            EmptyReason = PersonalDashboardEmptyReason.HasData,
            InstanceTiles =
            [
                new PersonalInstanceTileDisplay
                {
                    InstanceId = "a",
                    DisplayName = "Family WhatsApp",
                    PlatformLabel = "WhatsApp",
                    DetailLine = "Connected",
                    ConnectionStatusLabel = "Connected",
                    ConnectionColorHex = "#107C10",
                    IconGlyph = "\uE8BD",
                    AccentColorHex = "#25D366"
                }
            ]
        };

        var viewState = PersonalDashboardPresentationHelper.BuildViewState(snapshot, "telegram");

        Assert.Empty(viewState.InstanceTiles);
        Assert.True(viewState.ShowInstanceTilesEmptyState);
        Assert.Equal("No personal accounts match your search.", viewState.InstanceTilesEmptyHint);
    }

    [Fact]
    public void ResolveSelectionKey_UsesSettingsKeyWhenRequested()
    {
        Assert.Equal(
            WorkspaceSidebarHelper.SettingsSelectionKey,
            WorkspaceSidebarHelper.ResolveSelectionKey(false, null, settingsSelected: true));
    }

    [Fact]
    public void ResolvePersonalEmptyHint_PointsToSidebarAddInstance()
    {
        var hint = DashboardPageHelper.ResolvePersonalEmptyHint(
            PersonalDashboardEmptyReason.NoPersonalAccounts);

        Assert.Contains("Add Instance", hint, StringComparison.OrdinalIgnoreCase);
    }
}
