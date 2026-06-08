using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class DashboardPageHelperTests
{
    [Theory]
    [InlineData(0, 0, "Add an account")]
    [InlineData(2, 3, "2 professional and 3 personal")]
    [InlineData(1, 0, "1 professional account connected")]
    [InlineData(0, 2, "2 personal accounts connected")]
    public void BuildWelcomeSubtitle_FormatsConnectedAccountSummary(
        int professionalCount,
        int personalCount,
        string expectedFragment)
    {
        Assert.Contains(expectedFragment, DashboardPageHelper.BuildWelcomeSubtitle(professionalCount, personalCount));
    }

    [Fact]
    public void BuildInstanceStatusLine_IncludesVisibilityUnreadAndHealth()
    {
        var line = DashboardPageHelper.BuildInstanceStatusLine(new InstanceResourceTile
        {
            InstanceId = "inst-1",
            DisplayName = "Work",
            Platform = "whatsapp",
            AccentColor = "#25D366",
            IconGlyph = "\uE8BD",
            IsVisible = true,
            MemoryTier = "High",
            UnreadCount = 2,
            HealthState = AdapterHealthState.Healthy
        });

        Assert.Contains("Visible", line);
        Assert.Contains("High", line);
        Assert.Contains("2 unread", line);
        Assert.Contains("Healthy", line);
    }

    [Fact]
    public void ActivityMatches_SearchesTitleBodyAndInstanceName()
    {
        Assert.True(DashboardPageHelper.ActivityMatches("Invoice", "Body", "Sales", "invoice"));
        Assert.False(DashboardPageHelper.ActivityMatches("Hello", "Body", "Sales", "invoice"));
    }

    [Fact]
    public void FilterPersonalSearchMatches_ReturnsMatchingPersonalAccounts()
    {
        var matches = DashboardPageHelper.FilterPersonalSearchMatches([
            new MessengerInstance { Id = "inst-1", DisplayName = "Sales WhatsApp", Platform = "whatsapp" },
            new MessengerInstance { Id = "inst-2", DisplayName = "Family Telegram", Platform = "telegram" }
        ], "sales");

        Assert.Single(matches);
        Assert.Equal("inst-1", matches[0].InstanceId);
    }

    [Fact]
    public void FilterPersonalSearchMatches_IncludesAlertTitleAndBodyMatches()
    {
        var instances = new List<MessengerInstance>
        {
            new() { Id = "inst-1", DisplayName = "Family WhatsApp", Platform = "whatsapp", AccentColor = "#25D366" }
        };

        var alerts = new[]
        {
            NotificationAlert.Create("inst-1", "Family WhatsApp", "whatsapp", "Invoice due", "Please pay by Friday")
        };

        var matches = DashboardPageHelper.FilterPersonalSearchMatches(instances, "invoice", alerts);

        Assert.Single(matches);
        Assert.Equal("inst-1", matches[0].InstanceId);
        Assert.Equal("Invoice due", matches[0].Label);
    }

    [Theory]
    [InlineData(PersonalDashboardEmptyReason.NoPersonalAccounts, false, "Add a personal account")]
    [InlineData(PersonalDashboardEmptyReason.AllAccountsMuted, false, "muted for all accounts")]
    [InlineData(PersonalDashboardEmptyReason.NoRecentActivity, true, "matches your search")]
    public void ResolvePersonalActivityEmptyMessage_UsesContextualCopy(
        PersonalDashboardEmptyReason emptyReason,
        bool hasSearchQuery,
        string expectedFragment)
    {
        var message = DashboardPageHelper.ResolvePersonalActivityEmptyMessage(emptyReason, hasSearchQuery);
        Assert.Contains(expectedFragment, message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(InstanceConnectionStatus.Connected, "Connected")]
    [InlineData(InstanceConnectionStatus.LoggedOut, "Logged out")]
    [InlineData(InstanceConnectionStatus.Error, "Error")]
    [InlineData(InstanceConnectionStatus.Initializing, "Connecting")]
    public void FormatConnectionPillLabel_UsesFriendlyLabels(
        InstanceConnectionStatus status,
        string expected)
    {
        Assert.Equal(expected, DashboardPageHelper.FormatConnectionPillLabel(status));
    }

    [Fact]
    public void BuildPersonalTileDetailLine_IncludesConnectionDetailWhenConnected()
    {
        var line = DashboardPageHelper.BuildPersonalTileDetailLine(
            new InstanceResourceTile
            {
                InstanceId = "inst-1",
                DisplayName = "Personal",
                Platform = "whatsapp",
                AccentColor = "#25D366",
                IconGlyph = "\uE8BD",
                UnreadCount = 2,
                HealthState = AdapterHealthState.Healthy
            },
            InstanceConnectionStatus.Connected,
            notificationsMuted: false,
            connectionDetail: "WhatsApp Web");

        Assert.Contains("2 unread", line);
        Assert.Contains("Monitoring active", line);
        Assert.Contains("WhatsApp Web", line);
    }

    [Fact]
    public void FormatPersonalQuickActionLabel_PluralizesUnreadCount()
    {
        Assert.Equal(
            "Open Family WhatsApp (3 unread)",
            DashboardPageHelper.FormatPersonalQuickActionLabel("Family WhatsApp", 3));
    }

    [Theory]
    [InlineData(1, "1 unreplied review")]
    [InlineData(4, "4 unreplied reviews")]
    public void FormatUnrepliedReviewCount_PluralizesLabel(int count, string expected)
    {
        Assert.Equal(expected, DashboardPageHelper.FormatUnrepliedReviewCount(count));
    }

    [Fact]
    public void BuildProfessionalDisplay_ReplacesPlaceholdersWhenMetricsExist()
    {
        var snapshot = new ProfessionalAnalyticsSnapshot
        {
            SentCount = 4,
            ReceivedCount = 9,
            AverageReplyTimeDisplay = "12 min",
            SlaBreaches = 1,
            ResponseRateDisplay = "80%",
            PeakHourDisplay = "3 PM",
            DailyTrendDisplay = "+2 vs yesterday",
            HasReplyMetrics = true,
            HasMessageVolume = true,
            WeeklyActivity =
            [
                new DailyActivityPoint { Label = "Today", Sent = 1, Received = 2 }
            ]
        };

        var display = DashboardPageHelper.BuildProfessionalDisplay(snapshot);

        Assert.Equal("12 min", display.AverageReplyTime);
        Assert.Equal("1", display.SlaBreaches);
        Assert.Equal("4", display.SentCount);
        Assert.Equal("9", display.ReceivedCount);
    }

    [Fact]
    public void BuildProfessionalDisplay_KeepsPlaceholdersWhenNoMetrics()
    {
        var snapshot = new ProfessionalAnalyticsSnapshot();

        var display = DashboardPageHelper.BuildProfessionalDisplay(snapshot);

        Assert.Equal("—", display.AverageReplyTime);
        Assert.Equal("—", display.SlaBreaches);
        Assert.Equal("—", display.SentCount);
        Assert.Equal("—", display.ResponseRate);
    }
}
