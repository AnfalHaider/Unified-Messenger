using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// The quiet-hours cell of the state matrix, which the handoff listed as never exercised.
///
/// <para>
/// <c>QuietHoursTests</c> already covers <c>IsQuiet</c> in isolation — wrap past midnight, same-day
/// windows, zero-length windows, disabled. What had no coverage is the part that actually matters and
/// that the code comment in <c>OversightAlertMonitor</c> claims: quiet hours must <b>suppress the toast
/// without consuming the alert edge</b>, so the alert still fires once quiet hours end. Get that wrong
/// and an overnight backlog is silently swallowed — the owner is never told, and because the edge was
/// consumed they are never told later either.
/// </para>
/// <para>
/// The monitor's loop needs live instances and a UI dispatcher, so these compose the two pure pieces it
/// composes — <see cref="QuietHours.IsQuiet"/> and <see cref="OversightAlertMonitor.Evaluate"/> — in the
/// same order the loop does, with the suppression check <b>before</b> Evaluate.
/// </para>
/// </summary>
public class QuietHoursInteractionTests
{
    private static AppSettings Overnight() => new()
    {
        QuietHoursEnabled = true,
        QuietHoursStartHour = 21,
        QuietHoursEndHour = 8
    };

    /// <summary>
    /// One tick of the monitor's decision, in the monitor's order: skip while quiet, otherwise evaluate.
    /// Returns whether a toast fires and the carried alert state.
    /// </summary>
    private static (bool Fire, bool Alerted) Tick(AppSettings settings, int localHour, int awaiting, int threshold, bool alerted)
    {
        if (QuietHours.IsQuiet(settings, localHour))
        {
            return (false, alerted); // `continue` — state carried untouched
        }

        return OversightAlertMonitor.Evaluate(awaiting, threshold, alerted);
    }

    [Fact]
    public void ABacklogCrossingTheThresholdOvernightIsAnnouncedWhenQuietHoursEnd()
    {
        // The behaviour the monitor's comment promises. 22:00 and 03:00 stay silent; 09:00 fires once.
        var settings = Overnight();
        var alerted = false;

        foreach (var hour in new[] { 22, 23, 0, 3, 6 })
        {
            var (fire, next) = Tick(settings, hour, awaiting: 40, threshold: 25, alerted);
            Assert.False(fire, $"a toast fired at {hour}:00, inside quiet hours");
            alerted = next;
        }

        var (morningFire, morningAlerted) = Tick(settings, 9, awaiting: 40, threshold: 25, alerted);

        Assert.True(morningFire, "the overnight backlog was never announced once quiet hours ended");
        Assert.True(morningAlerted);
    }

    [Fact]
    public void QuietHoursDoNotConsumeTheEdge()
    {
        // The specific failure the ordering guards against: evaluating first and then discarding the toast
        // would set alerted = true while quiet, so the morning tick would see "already alerted" and stay
        // silent forever.
        var settings = Overnight();

        var (_, afterQuietTick) = Tick(settings, 23, awaiting: 40, threshold: 25, alerted: false);

        Assert.False(afterQuietTick, "the alert edge was consumed during quiet hours");
    }

    [Fact]
    public void OnceAnnouncedItDoesNotRepeatEveryTick()
    {
        var settings = Overnight();

        var (first, alerted) = Tick(settings, 9, 40, 25, false);
        var (second, stillAlerted) = Tick(settings, 10, 40, 25, alerted);
        var (third, _) = Tick(settings, 11, 45, 25, stillAlerted);

        Assert.True(first);
        Assert.False(second);
        Assert.False(third);
    }

    [Fact]
    public void FallingBackBelowTheThresholdRearmsTheAlert()
    {
        // Cleared the backlog, then it builds again — the owner should be told the second time too.
        var settings = Overnight();

        var (_, alerted) = Tick(settings, 9, 40, 25, false);
        var (_, afterClear) = Tick(settings, 10, 3, 25, alerted);
        var (again, _) = Tick(settings, 11, 40, 25, afterClear);

        Assert.False(afterClear);
        Assert.True(again, "a second backlog after clearing was never announced");
    }

    [Fact]
    public void QuietHoursOffMeansTheHourNeverSuppresses()
    {
        var settings = new AppSettings { QuietHoursEnabled = false, QuietHoursStartHour = 21, QuietHoursEndHour = 8 };

        var (fire, _) = Tick(settings, 3, 40, 25, false);

        Assert.True(fire);
    }

    [Fact]
    public void AThresholdOfZeroIsHandledByTheCallerNotByEvaluate()
    {
        // The monitor returns early on threshold <= 0 ("alerts disabled"), before quiet hours are even
        // consulted. Evaluate itself would fire on any count, which is why that guard has to stay where
        // it is — recorded here so nobody moves it.
        var (fire, _) = OversightAlertMonitor.Evaluate(awaiting: 0, threshold: 0, alerted: false);

        Assert.True(fire);
    }

    [Fact]
    public void AWindowThatWrapsMidnightIsQuietOnBothSidesOfIt()
    {
        // Sanity on the composition rather than on IsQuiet alone: the same settings object suppresses at
        // 23:00 and at 02:00, which is the case a naive start<end comparison gets wrong.
        var settings = Overnight();

        Assert.False(Tick(settings, 23, 40, 25, false).Fire);
        Assert.False(Tick(settings, 2, 40, 25, false).Fire);
        Assert.True(Tick(settings, 12, 40, 25, false).Fire);
    }
}
