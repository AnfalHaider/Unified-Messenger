using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Google renders a review's age as prose — "2 days ago", "a week ago" — and the scrape captured it as a
/// literal string.
///
/// <para>
/// A string cannot answer the three questions that turn a count into a queue: which review has been waiting
/// longest, is this one past our reply target, and did our reply time improve this month. Google exposes no
/// absolute date on the manager page, so parsing its own words is the only route without the API.
/// </para>
/// </summary>
public class ReviewAgeTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("2 days ago", 2)]
    [InlineData("6 days ago", 6)]
    [InlineData("a day ago", 1)]
    [InlineData("1 day ago", 1)]
    [InlineData("3 weeks ago", 21)]
    [InlineData("a week ago", 7)]
    [InlineData("2 months ago", 60)]
    [InlineData("a month ago", 30)]
    [InlineData("a year ago", 365)]
    public void GooglesWordingBecomesADuration(string age, int expectedDays) =>
        Assert.Equal(expectedDays, (int)ReviewAge.Parse(age)!.Value.TotalDays);

    [Theory]
    [InlineData("an hour ago", 1)]
    [InlineData("5 hours ago", 5)]
    public void HoursAreKeptAsHours(string age, int expectedHours) =>
        Assert.Equal(expectedHours, (int)ReviewAge.Parse(age)!.Value.TotalHours);

    [Theory]
    [InlineData("Just now")]
    [InlineData("Today")]
    public void TheMostRecentWordingsReadAsZero(string age) =>
        Assert.Equal(TimeSpan.Zero, ReviewAge.Parse(age));

    [Fact]
    public void YesterdayIsADay() =>
        Assert.Equal(1, (int)ReviewAge.Parse("Yesterday")!.Value.TotalDays);

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("Local Guide")]
    [InlineData("edited")]
    public void UnrecognisedTextReturnsNullRatherThanAGuess(string? age) =>
        Assert.Null(ReviewAge.Parse(age));

    [Fact]
    public void AnUnknownAgeSortsLastNotFirst()
    {
        // The direction matters. An unparsed age is missing information, and letting it lead the queue
        // would push a genuinely old review down the list on the strength of a string we failed to read.
        var known = ReviewAge.SortKey("6 days ago");
        var unknown = ReviewAge.SortKey("something Google changed");

        Assert.True(known > unknown);
    }

    [Fact]
    public void TheOldestReviewSortsFirst()
    {
        var ages = new[] { "2 days ago", "3 weeks ago", "an hour ago", "a month ago" };

        var ordered = ages.OrderByDescending(ReviewAge.SortKey).ToArray();

        Assert.Equal(["a month ago", "3 weeks ago", "2 days ago", "an hour ago"], ordered);
    }

    [Fact]
    public void ApproximateTimestampsRunBackwardsFromNow()
    {
        var left = ReviewAge.ApproximateLeftAtUtc("2 days ago", Now);

        Assert.Equal(new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero), left);
    }

    [Theory]
    [InlineData("2 days ago", "2d")]
    [InlineData("3 weeks ago", "3w")]
    [InlineData("5 hours ago", "5h")]
    [InlineData("2 months ago", "2mo")]
    [InlineData("a year ago", "1y")]
    public void TheShortLabelIsCompactEnoughForAChip(string age, string expected) =>
        Assert.Equal(expected, ReviewAge.ShortLabel(age));

    [Fact]
    public void AnUnparsedAgeStillShowsGooglesOwnWords()
    {
        // Better to render whatever Google said than a blank chip — the owner can read it even when we
        // cannot.
        Assert.Equal("sometime last spring", ReviewAge.ShortLabel("sometime last spring"));
    }
}
