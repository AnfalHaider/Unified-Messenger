using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class UnifiedMessengerDashboardPresentationHelperSlaTests
{
    [Fact]
    public void FormatSlaCountdown_ShowsRemainingTimeBeforeBreach()
    {
        var text = UnifiedMessengerDashboardPresentationHelper.FormatSlaCountdown(10, 15);
        Assert.Contains("to SLA", text);
        Assert.DoesNotContain("breach", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormatSlaCountdown_ShowsBreachWhenPastThreshold()
    {
        var text = UnifiedMessengerDashboardPresentationHelper.FormatSlaCountdown(20, 15);
        Assert.Contains("breach", text, StringComparison.OrdinalIgnoreCase);
    }
}
