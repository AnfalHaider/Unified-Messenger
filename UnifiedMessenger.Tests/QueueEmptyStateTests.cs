using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// The all-clear (mockup §14, Increment 130).
///
/// <para>
/// "All caught up" is the most consequential sentence in the product — it is the one the owner acts on by
/// closing the app and going home. An empty queue over a set that includes an account nothing is reading
/// is therefore the most expensive false calm available, and it read exactly the same as a genuine
/// all-clear.
/// </para>
/// </summary>
public class QueueEmptyStateTests
{
    [Fact]
    public void AGenuineAllClearIsStatedWithoutHedging()
    {
        var text = QueueEmptyState.Describe(scopeLabel: null, signedOutCount: 0);

        // Over-qualifying a true all-clear is its own failure: an owner who cannot trust the good news
        // stops reading the line entirely.
        Assert.Equal("All caught up — no customers are waiting on a reply.", text);
    }

    [Fact]
    public void ASignedOutAccountBoundsTheClaimWithoutWithholdingIt()
    {
        var text = QueueEmptyState.Describe(scopeLabel: null, signedOutCount: 1);

        // Appended, not substituted: the claim about the accounts that ARE being read is still true and
        // still worth stating. The owner needs it bounded, not withheld.
        Assert.StartsWith("All caught up", text, StringComparison.Ordinal);
        Assert.Contains("1 signed-out account is not included", text, StringComparison.Ordinal);
        Assert.Contains("nothing has been read from it", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SeveralSignedOutAccountsReadAsASentence()
    {
        var text = QueueEmptyState.Describe(scopeLabel: null, signedOutCount: 3);

        Assert.Contains("3 signed-out accounts are not included", text, StringComparison.Ordinal);
        Assert.Contains("from them", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AScopedQueueKeepsItsOwnLabelAndStillQualifies()
    {
        var text = QueueEmptyState.Describe("Depilex DHA-2", signedOutCount: 2);

        Assert.StartsWith("Depilex DHA-2 is all caught up", text, StringComparison.Ordinal);
        Assert.Contains("2 signed-out accounts", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AScopedAllClearWithNothingSignedOutStaysShort()
    {
        Assert.Equal(
            "Depilex DHA-2 is all caught up — no customers waiting.",
            QueueEmptyState.Describe("Depilex DHA-2", signedOutCount: 0));
    }
}
