using UnifiedMessenger.Services;
using Xunit;

namespace UnifiedMessenger.Tests;

public class OversightAlertMonitorTests
{
    [Fact]
    public void Evaluate_FiresOnceWhenCrossingThreshold()
    {
        // Below threshold → no alert, stays un-alerted.
        var (fire1, alerted1) = OversightAlertMonitor.Evaluate(4, 5, alerted: false);
        Assert.False(fire1);
        Assert.False(alerted1);

        // Crosses up → fires, now marked alerted.
        var (fire2, alerted2) = OversightAlertMonitor.Evaluate(7, 5, alerted: false);
        Assert.True(fire2);
        Assert.True(alerted2);

        // Still above but already alerted → no repeat.
        var (fire3, alerted3) = OversightAlertMonitor.Evaluate(9, 5, alerted: true);
        Assert.False(fire3);
        Assert.True(alerted3);

        // Drops below → resets so a later crossing alerts again.
        var (fire4, alerted4) = OversightAlertMonitor.Evaluate(2, 5, alerted: true);
        Assert.False(fire4);
        Assert.False(alerted4);
    }
}
