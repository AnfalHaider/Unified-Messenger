using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.ViewModels;

namespace UnifiedMessenger.Tests;

public class Wave10CrossCuttingTests
{
    [Fact]
    public void NotificationAlert_Create_PreservesConversationTarget()
    {
        var alert = NotificationAlert.Create(
            "inst-1",
            "Sales WhatsApp",
            "whatsapp",
            "Invoice due",
            body: "Please review",
            conversationKey: "120363@s.whatsapp.net",
            customerName: "Alex");

        Assert.True(alert.HasConversationTarget);
        Assert.Equal("120363@s.whatsapp.net", alert.ConversationKey);
        Assert.Equal("Alex", alert.CustomerName);
    }

    [Fact]
    public void NotificationNavigationHelper_OpenAlert_UsesConversationTarget()
    {
        var navigation = ShellNavigationService.CreateForTests();
        InstanceNavigationRequest? request = null;
        navigation.InstanceNavigationRequested += (_, item) => request = item;

        NotificationNavigationHelper.OpenAlert(
            navigation,
            NotificationAlert.Create(
                "inst-1",
                "Sales",
                "whatsapp",
                "Ping",
                conversationKey: "thread-1",
                customerName: "Sam"));

        Assert.NotNull(request);
        Assert.Equal("inst-1", request!.InstanceId);
        Assert.Equal("thread-1", request.ConversationKey);
        Assert.Equal("Sam", request.CustomerName);
    }

    [Fact]
    public void ShellNavigationService_OpenInstance_MatchesRequestInstanceBehavior()
    {
        var navigation = ShellNavigationService.CreateForTests();
        InstanceNavigationRequest? request = null;
        navigation.InstanceNavigationRequested += (_, item) => request = item;

        navigation.OpenInstance("inst-occ", "lead-9", "Jordan");

        Assert.NotNull(request);
        Assert.Equal("inst-occ", request!.InstanceId);
        Assert.Equal("lead-9", request.ConversationKey);
        Assert.True(request.HasConversationTarget);
    }

    [Fact]
    public void AppNotificationService_TryParseActivationArguments_ReadsConversationFields()
    {
        var raw = "instanceId=inst-1&conversationKey=lead-9&customerName=Jordan";

        Assert.True(AppNotificationService.TryParseActivationArguments(raw, out var activation));
        Assert.Equal("inst-1", activation.InstanceId);
        Assert.Equal("lead-9", activation.ConversationKey);
        Assert.Equal("Jordan", activation.CustomerName);
    }

    [Fact]
    public void WeeklyActivityChartViewModel_ApplySeries_BuildsBarCollection()
    {
        var viewModel = new WeeklyActivityChartViewModel();
        viewModel.ApplySeries([
            new DailyActivityPoint { Label = "Mon", Sent = 2, Received = 4 },
            new DailyActivityPoint { Label = "Tue", Sent = 1, Received = 1 }
        ]);

        Assert.Equal(2, viewModel.Bars.Count);
        Assert.False(viewModel.ShowEmptyHint);
        Assert.Contains("3 sent", viewModel.SummaryText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Mon", viewModel.Bars[0].Label);
    }

    [Fact]
    public void SentimentActivityChartViewModel_ApplySeries_ShowsEmptyWhenNoData()
    {
        var viewModel = new SentimentActivityChartViewModel();
        viewModel.ApplySeries(MessageTriageDashboardSnapshot.Empty);

        Assert.Empty(viewModel.Bars);
        Assert.True(viewModel.ShowEmptyHint);
    }

    [Fact]
    public void AccessibilityTabOrderHelper_DefinesPrimarySurfaceOrder()
    {
        Assert.True(AccessibilityTabOrderHelper.DashboardTabs < AccessibilityTabOrderHelper.OccRefreshButton);
        Assert.True(AccessibilityTabOrderHelper.SettingsSectionNav < AccessibilityTabOrderHelper.SettingsContent);
    }
}
