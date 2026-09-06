namespace UnifiedMessenger.Tests;

/// <summary>
/// The Direct-inbox preview harvest (Increment 136).
///
/// <para>
/// Runs only after the owner clicks an Instagram customer, once the click-through has already navigated
/// that account to Direct. Never on a background cycle: the passive read on the feed stays passive, and an
/// account the owner is not opening is never navigated. That restriction is the entire safety argument, so
/// most of what is asserted here is about what the harvest refuses to do.
/// </para>
/// </summary>
public class InstagramInboxHarvestTests
{
    private static string Script() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", "instagram-adapter.js"));

    private static string HarvestBody()
    {
        var script = Script();
        var start = script.IndexOf("window.__umHarvestInstagramInbox", StringComparison.Ordinal);
        Assert.True(start >= 0, "The harvest must exist.");

        var end = script.IndexOf("window.__umReadInstagramThreads", start, StringComparison.Ordinal);
        return end > start ? script[start..end] : script[start..];
    }

    [Fact]
    public void ItRefusesToHarvestUnlessItIsOnTheInboxList()
    {
        // If something navigated into a conversation, this reports nothing rather than scraping a thread
        // the owner did not ask to open.
        Assert.Contains("!onInbox() || insideThread()", HarvestBody(), StringComparison.Ordinal);
    }

    [Fact]
    public void ItReadsThreadAnchorsWithoutFollowingThem()
    {
        var body = HarvestBody();

        // A row is identified by its own thread anchor. Reading an href is not navigating to it — and the
        // harvest must never click, because a click is what turns a read into a "Seen".
        Assert.Contains("getAttribute('href')", body, StringComparison.Ordinal);
        Assert.DoesNotContain(".click(", body, StringComparison.Ordinal);
        Assert.DoesNotContain("location.assign", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ATimestampIsNotMistakenForAPreview()
    {
        var body = HarvestBody();

        // Instagram renders "Raja sent a video." and "· 5h" as separate runs, so a naive "line 1" read
        // returns the age on every row whose preview is empty — putting "· 5h" in the queue as if the
        // customer had said it.
        Assert.Contains("(m|h|d|w|min|hour|day|week)", body, StringComparison.Ordinal);
    }

    [Fact]
    public void HarvestedTextIsCutWithoutSplittingASurrogatePair()
    {
        var body = HarvestBody();

        // A cut through an emoji leaves a lone surrogate and System.Text.Json then throws on that
        // property — which once silently dropped a real conversation from every scan.
        //
        // Asserted on the two fields that carry CUSTOMER text. A blanket ban on slicing was the first
        // version and it failed on the catch block's error-message truncation — which is our own string,
        // cannot contain a customer's emoji, and is exactly the kind of collateral that makes people
        // weaken a guard rather than satisfy it.
        Assert.Contains("name: safeTruncate(lines[0]", body, StringComparison.Ordinal);
        Assert.Contains("preview: safeTruncate(preview", body, StringComparison.Ordinal);
    }

    [Fact]
    public void TheHarvestIsNotWiredIntoAnyBackgroundScan()
    {
        // The whole safety argument in one assertion. OversightAlertMonitor is the background cycle; if
        // the harvest ever appears there, every Instagram account gets navigated on a timer and the
        // passive read stops being passive.
        var monitor = File.ReadAllText(Path.Combine(
            WcagContrast.RepoRoot(), "UnifiedMessenger", "Services", "Oversight", "OversightAlertMonitor.cs"));

        Assert.DoesNotContain("HarvestInboxPreviews", monitor, StringComparison.Ordinal);
    }

    [Fact]
    public void CoverageUpgradesOnlyForAnAccountThatActuallyHasPreviews()
    {
        // Instagram's channel-level answer is "no message text". An account whose Direct list has been
        // read carries them, and continuing to label that account with the channel default while its rows
        // visibly show message text is the stale disclosure that teaches people to ignore every other one.
        var coverage = File.ReadAllText(Path.Combine(
            WcagContrast.RepoRoot(), "UnifiedMessenger", "Services", "Oversight", "ChannelCoverage.cs"));

        Assert.Contains("HasPreviews", coverage, StringComparison.Ordinal);

        // Only ever upgrades: a channel that has never supplied previews cannot be promoted by an empty
        // snapshot, and nothing here may downgrade a fully-measured channel.
        Assert.Contains("level == ChannelCoverageLevel.NoMessageText", coverage, StringComparison.Ordinal);
    }
}
