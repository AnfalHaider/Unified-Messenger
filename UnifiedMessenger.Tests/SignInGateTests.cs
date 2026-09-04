using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// The sign-in gate (A12): the single answer to "may this account be scraped?" and "may its figures be
/// shown?".
///
/// <para>
/// The defect being guarded is a read that <i>succeeds</i> and returns nothing. A signed-out client was
/// scraped, found zero conversations, and every consumer downstream read that as "caught up" — the scan
/// never failed, so <c>AccountReadHealth</c> never warned. Zero is a measurement; an account nothing has
/// read has not been measured, and the two must not render the same.
/// </para>
/// </summary>
public class SignInGateTests
{
    private static string NewId() => $"gate-{Guid.NewGuid():N}";

    private static string SignedOut()
    {
        var id = NewId();
        InstanceConnectionStatusService.Instance.SetLoggedOut(id, "Sign-in screen");
        return id;
    }

    private static string Connected()
    {
        var id = NewId();
        InstanceConnectionStatusService.Instance.SetConnected(id, "Signed in");
        return id;
    }

    [Fact]
    public void ASignedOutAccountIsGatedForBothScanningAndDisplay()
    {
        var id = SignedOut();

        Assert.True(SignInGate.IsSignedOut(id));
        Assert.False(SignInGate.MayScan(id));
        Assert.False(SignInGate.MayShowFigures(id));
    }

    [Fact]
    public void AConnectedAccountIsNotGated()
    {
        var id = Connected();

        Assert.False(SignInGate.IsSignedOut(id));
        Assert.True(SignInGate.MayScan(id));
        Assert.True(SignInGate.MayShowFigures(id));
    }

    [Theory]
    [InlineData(InstanceConnectionStatus.Initializing)]
    [InlineData(InstanceConnectionStatus.Error)]
    public void OnlyLoggedOutCounts_NotABootingPageAndNotANetworkFailure(InstanceConnectionStatus status)
    {
        var id = NewId();
        if (status == InstanceConnectionStatus.Initializing)
        {
            InstanceConnectionStatusService.Instance.SetInitializing(id);
        }
        else
        {
            InstanceConnectionStatusService.Instance.SetError(id, "ERR_INTERNET_DISCONNECTED");
        }

        // Neither is evidence about credentials. Treating them as signed out would send the owner to log
        // into an account whose session is perfectly fine.
        Assert.False(SignInGate.IsSignedOut(id));
        Assert.True(SignInGate.MayScan(id));
    }

    [Fact]
    public void ScanningAndDisplayNeverDisagree()
    {
        // They are the same condition by construction, and this test exists to make a future divergence a
        // deliberate edit. Gating the scan but not the display leaves the last figures read before the
        // session expired on screen, ageing silently — worse than showing nothing, because a stale figure
        // still reads as a measurement.
        foreach (var id in new[] { SignedOut(), Connected(), NewId() })
        {
            Assert.Equal(SignInGate.MayScan(id), SignInGate.MayShowFigures(id));
        }
    }

    [Fact]
    public void AnUnknownAccountIsNotTreatedAsSignedOut()
    {
        // Nothing has reported on it yet. Assuming the worst here would blank a card during startup, before
        // the first handshake lands, on every launch.
        Assert.False(SignInGate.IsSignedOut(NewId()));
        Assert.False(SignInGate.IsSignedOut(null));
        Assert.False(SignInGate.IsSignedOut("   "));
    }

    [Fact]
    public void DescribeSignedOut_IsNullWhenEverythingIsSignedIn()
    {
        var instances = new[] { Instance(Connected(), "whatsapp") };

        Assert.Null(SignInGate.DescribeSignedOut(instances));
        Assert.Null(SignInGate.DescribeSignedOut([]));
        Assert.Null(SignInGate.DescribeSignedOut(null));
    }

    [Fact]
    public void DescribeSignedOut_NamesTheChannelAndSaysWhatIsMissing()
    {
        var instances = new[]
        {
            Instance(Connected(), "whatsapp"),
            Instance(SignedOut(), "messenger")
        };

        var text = SignInGate.DescribeSignedOut(instances);

        Assert.NotNull(text);
        Assert.Contains("1 account is signed out", text, StringComparison.Ordinal);
        Assert.Contains("Messenger", text, StringComparison.Ordinal);
        Assert.Contains("contributes nothing", text, StringComparison.Ordinal);

        // Never implies the owner did something wrong — a session expiring is the platform's decision.
        Assert.DoesNotContain("you forgot", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("failed", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DescribeSignedOut_CountsAndListsSeveralChannels()
    {
        var instances = new[]
        {
            Instance(SignedOut(), "messenger"),
            Instance(SignedOut(), "instagram"),
            Instance(Connected(), "whatsapp")
        };

        var text = SignInGate.DescribeSignedOut(instances);

        Assert.NotNull(text);
        Assert.Contains("2 accounts are signed out", text, StringComparison.Ordinal);
        Assert.Contains("Instagram", text, StringComparison.Ordinal);
        Assert.Contains("Messenger", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CountSignedOut_CountsOnlyTheSignedOutOnes()
    {
        var instances = new[]
        {
            Instance(SignedOut(), "messenger"),
            Instance(Connected(), "whatsapp"),
            Instance(SignedOut(), "instagram")
        };

        Assert.Equal(2, SignInGate.CountSignedOut(instances));
        Assert.Equal(0, SignInGate.CountSignedOut([]));
        Assert.Equal(0, SignInGate.CountSignedOut(null));
    }

    private static MessengerInstance Instance(string id, string platform) => new()
    {
        Id = id,
        DisplayName = $"Account {id}",
        Platform = platform
    };
}
