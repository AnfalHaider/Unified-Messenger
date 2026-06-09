using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class PersonalDashboardPresentationHelperTests
{
    [Fact]
    public void BuildViewState_FiltersActivityBySearchQuery()
    {
        var snapshot = new PersonalDashboardSnapshot
        {
            PersonalAccountCount = 1,
            EmptyReason = PersonalDashboardEmptyReason.HasData,
            RecentActivity =
            [
                new PersonalActivityItem
                {
                    Alert = NotificationAlert.Create("inst-1", "Family", "whatsapp", "Invoice due"),
                    Title = "Invoice due",
                    Body = "Pay today",
                    InstanceDisplayName = "Family",
                    RelativeTimeText = "1m ago",
                    IconGlyph = "\uE8BD",
                    AccentColorHex = "#25D366"
                },
                new PersonalActivityItem
                {
                    Alert = NotificationAlert.Create("inst-1", "Family", "whatsapp", "Hello"),
                    Title = "Hello",
                    Body = "Catch up later",
                    InstanceDisplayName = "Family",
                    RelativeTimeText = "2m ago",
                    IconGlyph = "\uE8BD",
                    AccentColorHex = "#25D366"
                }
            ]
        };

        var viewState = PersonalDashboardPresentationHelper.BuildViewState(snapshot, "invoice");

        Assert.Single(viewState.FilteredActivity);
        Assert.Equal("Invoice due", viewState.FilteredActivity[0].Title);
        Assert.True(viewState.ShowActivityList);
    }

    [Fact]
    public void BuildViewState_ShowsQuickActionWhenUnreadExists()
    {
        var snapshot = new PersonalDashboardSnapshot
        {
            MostUnreadInstanceId = "inst-1",
            MostUnreadCount = 3,
            InstanceTiles =
            [
                new PersonalInstanceTileDisplay
                {
                    InstanceId = "inst-1",
                    DisplayName = "Family WhatsApp",
                    PlatformLabel = "WhatsApp",
                    DetailLine = "3 unread",
                    ConnectionStatusLabel = "Connected",
                    ConnectionColorHex = "#107C10",
                    IconGlyph = "\uE8BD",
                    AccentColorHex = "#25D366",
                    UnreadCount = 3
                }
            ]
        };

        var viewState = PersonalDashboardPresentationHelper.BuildViewState(snapshot);

        Assert.True(viewState.QuickAction.IsVisible);
        Assert.Equal("inst-1", viewState.QuickAction.InstanceId);
        Assert.Contains("Family WhatsApp", viewState.QuickAction.Label);
    }

    [Fact]
    public void BuildViewState_ShowsInstanceTilesEmptyStateWhenNoAccounts()
    {
        var viewState = PersonalDashboardPresentationHelper.BuildViewState(new PersonalDashboardSnapshot
        {
            EmptyReason = PersonalDashboardEmptyReason.NoPersonalAccounts
        });

        Assert.True(viewState.ShowInstanceTilesEmptyState);
        Assert.True(viewState.ShowNoAccountsEmptyState);
        Assert.Contains("sidebar", viewState.InstanceTilesEmptyHint, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(PersonalDashboardEmptyReason.AllAccountsMuted, "\uE74F")]
    [InlineData(PersonalDashboardEmptyReason.NoPersonalAccounts, "\uE8FA")]
    public void ResolveActivityEmptyState_UsesContextualIcon(
        PersonalDashboardEmptyReason emptyReason,
        string expectedGlyph)
    {
        var emptyState = PersonalDashboardPresentationHelper.ResolveActivityEmptyState(emptyReason, hasSearchQuery: false);
        Assert.Equal(expectedGlyph, emptyState.IconGlyph);
    }
}
