using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Stale data presented as current is the most dangerous state a dashboard has: it is indistinguishable
/// from good news, and the owner acts on it.
///
/// <para>
/// Only the command center and the review panel said when their numbers were captured. Analytics and
/// Reports rendered message counts, charts, response times and a written business report with no stamp at
/// all — so a scrape that failed three hours ago looked exactly like one that succeeded thirty seconds ago.
/// </para>
/// </summary>
public class DataFreshnessTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NeverCapturedSaysSoAndOffersTheFix()
    {
        var verdict = DataFreshness.Describe(null, Now);

        Assert.False(verdict.HasData);
        Assert.True(verdict.IsStale);
        Assert.Contains("Re-sync", verdict.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(10, "just now")]
    [InlineData(90, "1 minute ago")]
    [InlineData(600, "10 minutes ago")]
    [InlineData(5400, "1 hour ago")]
    [InlineData(18000, "5 hours ago")]
    [InlineData(129600, "yesterday")]
    [InlineData(432000, "5 days ago")]
    public void TheAgeIsPhrasedTheWayAPersonWouldSayIt(int secondsAgo, string expected)
    {
        var verdict = DataFreshness.Describe(Now.AddSeconds(-secondsAgo), Now);

        Assert.Contains(expected, verdict.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void FreshDataIsStampedButNotFlagged()
    {
        var verdict = DataFreshness.Describe(Now.AddMinutes(-2), Now);

        Assert.False(verdict.IsStale);
        Assert.True(verdict.HasData);
        Assert.DoesNotContain("Re-sync", verdict.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OldDataIsFlaggedAndSaysWhatToDo()
    {
        // Past the threshold the poll has been failing rather than lagging, which is a different message:
        // the figure is not slightly behind, it is unreliable.
        var verdict = DataFreshness.Describe(Now - DataFreshness.StaleAfter, Now);

        Assert.True(verdict.IsStale);
        Assert.Contains("Re-sync", verdict.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheThresholdIsWellClearOfTheBackgroundPoll()
    {
        // The oversight poll runs every 25–90 seconds. A threshold near that would flag normal operation
        // constantly, and an owner who sees a warning on every visit stops reading warnings.
        Assert.True(DataFreshness.StaleAfter >= TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void ACaptureInTheFutureNeverReadsAsNegativeTime()
    {
        // A clock change, or a snapshot written by a machine running slightly ahead. "Updated in 3 minutes"
        // reads as a bug and undermines every other number on the page.
        var verdict = DataFreshness.Describe(Now.AddMinutes(5), Now);

        Assert.Contains("just now", verdict.Text, StringComparison.Ordinal);
        Assert.False(verdict.IsStale);
        Assert.DoesNotContain("-", verdict.Text, StringComparison.Ordinal);
    }
}

/// <summary>
/// The save dialog the owner asked for. WebView2's own flow dropped every file into a folder they never
/// chose and — for an unpackaged host — cannot easily find.
/// </summary>
public class DownloadLocationTests
{
    [Theory]
    [InlineData(".jpg", "Image")]
    [InlineData(".JPEG", "Image")]
    [InlineData(".mp4", "Video")]
    [InlineData(".opus", "Audio")]
    [InlineData(".pdf", "PDF document")]
    [InlineData(".docx", "Word document")]
    [InlineData(".vcf", "Contact card")]
    public void TheFileTypeIsNamedInWordsNotAsAnExtension(string extension, string expected)
    {
        // WhatsApp's media arrives as these. A picker offering ".opus files" tells the owner nothing about
        // what they are about to save.
        Assert.Equal(expected, DownloadLocationPrompt.DescribeExtension(extension));
    }

    [Fact]
    public void AnUnknownExtensionStillGetsAReadableLabel()
    {
        Assert.Equal("XYZ file", DownloadLocationPrompt.DescribeExtension(".xyz"));
    }
}
