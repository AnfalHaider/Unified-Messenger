using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// The read strip under an open account's client (mockup §09, Increment 128).
///
/// <para>
/// It answers "is this account actually being read?", which had no on-screen answer anywhere in the app.
/// Every failure mode this stream has fixed — a signed-out account, a broken selector, a scan that never
/// ran — is invisible from inside the client it affects.
/// </para>
/// </summary>
public class AccountReadStripTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    private static string NewId() => $"strip-{Guid.NewGuid():N}";

    private static MessengerInstance Account(string platform, out string id, bool signedOut = false)
    {
        id = NewId();
        if (signedOut)
        {
            InstanceConnectionStatusService.Instance.SetLoggedOut(id, "Sign-in screen");
        }
        else
        {
            InstanceConnectionStatusService.Instance.SetConnected(id, "Signed in");
        }

        return new MessengerInstance { Id = id, DisplayName = "Account", Platform = platform };
    }

    [Fact]
    public void AReadingAccountSaysWhatItReadAndWhen()
    {
        var account = Account("whatsapp", out _);

        var status = AccountReadStrip.Describe(account, 42, 5, Now.AddMinutes(-3), Now);

        Assert.NotNull(status);
        Assert.Equal(AccountReadStrip.ReadState.Reading, status!.Value.State);
        Assert.Contains("42 conversations", status.Value.Text, StringComparison.Ordinal);
        Assert.Contains("5 waiting", status.Value.Text, StringComparison.Ordinal);
        Assert.Contains("3 min ago", status.Value.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void SignedOutBeatsEverythingElse()
    {
        var account = Account("whatsapp", out var id, signedOut: true);
        AccountReadHealth.RecordFailure(id, "whatever");

        var status = AccountReadStrip.Describe(account, 0, 0, null, Now);

        // Ordered worst-first because each earlier state makes the later ones meaningless: an account
        // nobody is signed into has not "failed to read" — there is nothing to read.
        Assert.Equal(AccountReadStrip.ReadState.SignedOut, status!.Value.State);
        Assert.Contains("Sign in here", status.Value.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AFailedReadIsNotDressedUpAsQuiet()
    {
        var account = Account("whatsapp", out var id);
        AccountReadHealth.RecordFailure(id, "scan blocked");

        var status = AccountReadStrip.Describe(account, 0, 0, Now.AddHours(-2), Now);

        Assert.Equal(AccountReadStrip.ReadState.ReadFailed, status!.Value.State);
        Assert.Contains("out of date", status.Value.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void NeverReadIsDistinctFromReadZero()
    {
        var account = Account("whatsapp", out _);

        var never = AccountReadStrip.Describe(account, 0, 0, null, Now);
        var readZero = AccountReadStrip.Describe(account, 0, 0, Now.AddMinutes(-1), Now);

        // Zero conversations after a real read is a measurement. No read at all is not, and the two must
        // not produce the same sentence.
        Assert.Equal(AccountReadStrip.ReadState.NeverRead, never!.Value.State);
        Assert.Equal(AccountReadStrip.ReadState.Reading, readZero!.Value.State);
    }

    [Fact]
    public void GoogleBusinessGetsNoStripAtAll()
    {
        var account = Account("googlebusiness", out _);

        // A Google tab is not failing to be read — reviews are read on their own surface. A strip saying
        // "not measured" under a reviews page would read as a fault where there is none.
        Assert.Null(AccountReadStrip.Describe(account, 0, 0, null, Now));
    }

    [Fact]
    public void AChannelNothingReadsSaysSoWithoutImplyingAFault()
    {
        var account = Account("messenger", out _);

        var status = AccountReadStrip.Describe(account, 0, 0, null, Now);

        Assert.Equal(AccountReadStrip.ReadState.NotMeasured, status!.Value.State);
        Assert.Contains("Nothing reads this channel yet", status.Value.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void InstagramSaysMessageTextIsNeverCopiedOut()
    {
        var account = Account("instagram", out _);

        var status = AccountReadStrip.Describe(account, 15, 4, Now.AddSeconds(-30), Now);

        // The strip sits inside a window showing a customer's messages, which is exactly where an owner
        // wonders what the app is taking — so it answers there, beside the evidence.
        Assert.Equal(AccountReadStrip.ReadState.Reading, status!.Value.State);
        Assert.Contains("never copied out of this client", status.Value.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AFutureTimestampReadsAsJustNowRatherThanAsNegativeAge()
    {
        var account = Account("whatsapp", out _);

        // A clock change, not a fresh read. "just now" is the honest reading of "we cannot tell how old
        // this is" and never claims a stale figure is new.
        var status = AccountReadStrip.Describe(account, 1, 0, Now.AddMinutes(5), Now);

        Assert.Contains("just now", status!.Value.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(30, "just now")]
    [InlineData(600, "10 min ago")]
    [InlineData(7200, "2h ago")]
    [InlineData(172800, "2d ago")]
    public void AgeIsPhrasedAtTheRightGrain(int secondsAgo, string expected)
    {
        var account = Account("whatsapp", out _);

        var status = AccountReadStrip.Describe(account, 1, 0, Now.AddSeconds(-secondsAgo), Now);

        Assert.Contains(expected, status!.Value.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryStateHasAPipColourFromTheAuditedPalette()
    {
        foreach (var state in Enum.GetValues<AccountReadStrip.ReadState>())
        {
            var key = AccountReadStrip.PipBrushKey(state);

            // Colour and wording come from the same switch, so they cannot disagree — and the key must be
            // one of the app's own tokens rather than a Windows system brush.
            Assert.StartsWith("Um", key, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NoInstanceMeansNoStrip()
    {
        Assert.Null(AccountReadStrip.Describe(null, 0, 0, null, Now));
    }
}
