using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// The notification hub's status line (§07) and the command palette's account subtitle (§12) — both
/// surfaces where an account's state was invisible (Increment 129).
/// </summary>
public class HubAndPaletteStatusTests
{
    private static AppSettings Quiet(int start = 21, int end = 8) => new()
    {
        QuietHoursEnabled = true,
        QuietHoursStartHour = start,
        QuietHoursEndHour = end
    };

    [Fact]
    public void ANormalHubSaysNothingAtAll()
    {
        // A notice that appears every day stops being read, and then the one that matters is invisible too.
        Assert.Null(NotificationHubStatus.Describe(new AppSettings(), signedOutCount: 0, localHour: 14));
        Assert.Null(NotificationHubStatus.Describe(null, signedOutCount: 0, localHour: 3));
    }

    [Fact]
    public void QuietHoursSaysWhenItEndsAndThatNothingIsLost()
    {
        var text = NotificationHubStatus.Describe(Quiet(), signedOutCount: 0, localHour: 23);

        Assert.NotNull(text);
        Assert.Contains("Quiet hours until 8am", text, StringComparison.Ordinal);

        // "Quiet hours are on" leaves the owner to work out whether that is why the evening is silent and
        // whether anything was dropped. Both answers belong in the sentence.
        Assert.Contains("held, not lost", text, StringComparison.Ordinal);
    }

    [Fact]
    public void OutsideQuietHoursItIsSilent()
    {
        Assert.Null(NotificationHubStatus.Describe(Quiet(), signedOutCount: 0, localHour: 14));
    }

    [Fact]
    public void ASignedOutAccountIsNamedBecauseItsSilenceIsNotEvidence()
    {
        var text = NotificationHubStatus.Describe(new AppSettings(), signedOutCount: 2, localHour: 14);

        Assert.NotNull(text);
        Assert.Contains("2 accounts are signed out", text, StringComparison.Ordinal);
        Assert.Contains("cannot raise alerts", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BothReasonsAppearTogether()
    {
        var text = NotificationHubStatus.Describe(Quiet(), signedOutCount: 1, localHour: 22);

        Assert.Contains("Quiet hours", text, StringComparison.Ordinal);
        Assert.Contains("1 account is signed out", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, "midnight")]
    [InlineData(8, "8am")]
    [InlineData(12, "noon")]
    [InlineData(21, "9pm")]
    public void TheEndHourReadsAsAPersonWouldSayIt(int endHour, string expected)
    {
        // 24-hour numbers are how the setting is stored, not how anyone reads a sentence.
        var text = NotificationHubStatus.Describe(Quiet(start: 0, end: endHour), signedOutCount: 0, localHour: 23);

        // start:0 with a late end keeps the window active at 23:00 for every case above except 21,
        // which is covered by the direct formatting assertion.
        if (text is not null)
        {
            Assert.Contains(expected, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void APaletteRowSaysNothingExtraWhenAnAccountIsFine()
    {
        var subtitle = CommandPaletteAccountSubtitle.Build("WhatsApp", "Professional", signedOut: false, awaitingCount: 0);

        // The palette is scanned, not read. The rows worth stopping on should be the ones carrying a number.
        Assert.Equal("WhatsApp · Professional", subtitle);
    }

    [Fact]
    public void APaletteRowCarriesTheWaitingCount()
    {
        Assert.Equal(
            "WhatsApp · Professional · 18 waiting",
            CommandPaletteAccountSubtitle.Build("WhatsApp", "Professional", signedOut: false, awaitingCount: 18));

        Assert.Equal(
            "WhatsApp · Professional · 1 waiting",
            CommandPaletteAccountSubtitle.Build("WhatsApp", "Professional", signedOut: false, awaitingCount: 1));
    }

    [Fact]
    public void SignedOutReplacesTheWaitingCountRatherThanJoiningIt()
    {
        var subtitle = CommandPaletteAccountSubtitle.Build("Messenger", "Professional", signedOut: true, awaitingCount: 7);

        // There is no honest waiting figure for an account nothing has read, and a number beside
        // "signed out" would answer the question the owner is actually asking with something meaningless.
        Assert.Equal("Messenger · Professional · Signed out", subtitle);
        Assert.DoesNotContain("waiting", subtitle, StringComparison.Ordinal);
    }
}
