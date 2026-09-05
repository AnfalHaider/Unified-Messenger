using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// The Analytics / business-report scope line, and the second way an account contributes nothing
/// (Increment 127).
///
/// <para>
/// <c>ChannelScope</c> named channels that cannot be measured and stopped there. A WhatsApp account sitting
/// on its QR screen is in the measurable channel set, so it counted as covered while supplying no messages
/// — "Covers all 8 accounts" printed over a chart built from seven. That is the same one-noun-two-populations
/// defect the excluded clause was written to fix, reached by the other door.
/// </para>
/// </summary>
public class ChannelScopeSignedOutTests
{
    private static string NewId() => $"scope-{Guid.NewGuid():N}";

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

        return new MessengerInstance { Id = id, DisplayName = $"Account {platform}", Platform = platform };
    }

    [Fact]
    public void EverythingSignedInAndMeasurableSaysSoPlainly()
    {
        var a = Account("whatsapp", out _);
        var b = Account("whatsapp", out _);

        Assert.Equal("Covers all 2 accounts.", ChannelScope.Describe([a, b]));
    }

    [Fact]
    public void ASignedOutAccountIsNoLongerCountedAsCovered()
    {
        var live = Account("whatsapp", out _);
        var out1 = Account("whatsapp", out _, signedOut: true);

        var text = ChannelScope.Describe([live, out1]);

        Assert.Contains("Covers 1 of 2 accounts", text, StringComparison.Ordinal);
        Assert.Contains("1 signed out", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Covers all", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnmeasurableChannelAndASignedOutAccountAreBothNamed()
    {
        var live = Account("whatsapp", out _);
        var google = Account("googlebusiness", out _);
        var out1 = Account("whatsapp", out _, signedOut: true);

        var text = ChannelScope.Describe([live, google, out1]);

        Assert.Contains("Covers 1 of 3 accounts", text, StringComparison.Ordinal);
        Assert.Contains("Google Business", text, StringComparison.Ordinal);
        Assert.Contains("1 signed out", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAccountThatIsBothUnmeasurableAndSignedOutIsCountedOnce()
    {
        var live = Account("whatsapp", out _);
        var google = Account("googlebusiness", out _, signedOut: true);

        var text = ChannelScope.Describe([live, google]);

        // The channel reason is the more fundamental one — signing in would not make Google Business
        // supply message analytics — so it is named there and not double-counted as signed out.
        Assert.Contains("Covers 1 of 2 accounts", text, StringComparison.Ordinal);
        Assert.DoesNotContain("signed out", text, StringComparison.Ordinal);
    }

    [Fact]
    public void InstagramIsNamedAsUnmeasuredHereEvenThoughItIsMeasuredElsewhere()
    {
        var wa = Account("whatsapp", out _);
        var ig = Account("instagram", out _);

        // Instagram feeds the needs-a-reply queue but supplies no per-message analytics, so on THIS page
        // it genuinely contributes nothing — and the line has to say so rather than inherit the
        // dashboard's answer.
        var text = ChannelScope.Describe([wa, ig]);

        Assert.Contains("Covers 1 of 2 accounts", text, StringComparison.Ordinal);
        Assert.Contains("Instagram", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLineNeverBlamesTheOwner()
    {
        var live = Account("whatsapp", out _);
        var out1 = Account("whatsapp", out _, signedOut: true);

        var text = ChannelScope.Describe([live, out1]);

        // A session expiring is the platform's decision. This is a figure read before acting, not a
        // telling-off.
        Assert.DoesNotContain("failed", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("you ", text, StringComparison.OrdinalIgnoreCase);
    }
}
