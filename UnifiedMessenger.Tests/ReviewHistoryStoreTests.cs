using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// The daily review history — the store that makes every trend tile possible.
/// </summary>
/// <remarks>
/// Nothing about reviews survived a restart before this: the snapshot service holds a dictionary that dies
/// with the process. These cover the two ways a history store can lie — recording a failed scrape as a zero,
/// and averaging a rating across a different set of locations from one day to the next.
/// </remarks>
public class ReviewHistoryStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(), $"um-review-history-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    private ReviewHistoryStore NewStore() => new(_path);

    [Fact]
    public void ARecordedReadingCanBeReadBack()
    {
        var store = NewStore();
        store.Record("a", rating: 4.6, lifetimeTotal: 992, unanswered: 9, answered: 141);

        var history = store.GetHistory("a");
        Assert.Single(history);
        Assert.Equal(4.6, history[0].Rating);
        Assert.Equal(992, history[0].LifetimeTotal);
        Assert.Equal(9, history[0].Unanswered);
    }

    [Fact]
    public void AMissingFigureKeepsTheDaysExistingValueRatherThanZeroingIt()
    {
        // The reviews scrape and the rating scrape run on different schedules and fail independently, so on
        // most days one writes without the other. A null must mean "not read", never "zero" — a day stored
        // as rating 0.0 would render as a collapse rather than the missed reading it was.
        var store = NewStore();
        store.Record("a", rating: 4.6, lifetimeTotal: 992, unanswered: null, answered: null);
        store.Record("a", rating: null, lifetimeTotal: null, unanswered: 9, answered: 141);

        var day = Assert.Single(store.GetHistory("a"));
        Assert.Equal(4.6, day.Rating);
        Assert.Equal(992, day.LifetimeTotal);
        Assert.Equal(9, day.Unanswered);
        Assert.Equal(141, day.Answered);
    }

    [Fact]
    public void RepeatedReadingsOnOneDayCollapseToOne()
    {
        // The background pass runs every 30 minutes. Keeping each one would be ~48 rows a day all describing
        // the same slow-moving state.
        var store = NewStore();
        store.Record("a", 4.6, 992, 9, 141);
        store.Record("a", 4.6, 993, 8, 143);

        var day = Assert.Single(store.GetHistory("a"));
        Assert.Equal(993, day.LifetimeTotal);
        Assert.Equal(8, day.Unanswered);
    }

    [Fact]
    public void AnUnknownAccountHasNoHistoryRatherThanThrowing()
    {
        var store = NewStore();
        Assert.Empty(store.GetHistory("nobody"));
        Assert.Empty(store.GetHistory(""));
    }

    // ---- combining locations ---------------------------------------------------------------------------

    [Fact]
    public void TheCombinedTotalIsTheSumAcrossLocations()
    {
        var store = NewStore();
        store.Record("a", 4.6, 992, 9, 141);
        store.Record("b", 4.7, 435, 3, 47);
        store.Record("c", 4.6, 244, 0, 50);

        var day = Assert.Single(store.GetCombinedHistory(["a", "b", "c"]));
        Assert.Equal(1671, day.LifetimeTotal);
        Assert.Equal(12, day.Unanswered);
    }

    [Fact]
    public void TheCombinedRatingIsWeightedByEachLocationsReviewCount()
    {
        // An unweighted mean would let a 244-review location move the business figure as much as a
        // 992-review one, and would disagree with the hero, which weights.
        var store = NewStore();
        store.Record("a", 4.6, 992, 0, 0);
        store.Record("b", 4.7, 435, 0, 0);
        store.Record("c", 4.6, 244, 0, 0);

        var day = Assert.Single(store.GetCombinedHistory(["a", "b", "c"]));
        var expected = ((4.6 * 992) + (4.7 * 435) + (4.6 * 244)) / 1671;
        Assert.Equal(expected, day.Rating!.Value, 6);
    }

    [Fact]
    public void ADayMissingOneLocationsRatingGetsNoCombinedRating()
    {
        // Otherwise the series silently switches between a three-location average and a two-location one,
        // and the change between those two days reads as a real movement in the business.
        var store = NewStore();
        store.Record("a", 4.6, 992, 0, 0);
        store.Record("b", null, 435, 0, 0);

        var day = Assert.Single(store.GetCombinedHistory(["a", "b"]));
        Assert.Null(day.Rating);
        Assert.Equal(1427, day.LifetimeTotal);
    }

    [Fact]
    public void DaysOfHistoryCountsDistinctDays()
    {
        var store = NewStore();
        store.Record("a", 4.6, 992, 9, 141);
        store.Record("b", 4.7, 435, 3, 47);

        // Both readings are today, so this is one day of history however many accounts reported.
        Assert.Equal(1, store.DaysOfHistory(["a", "b"]));
    }

    // ---- persistence -----------------------------------------------------------------------------------

    [Fact]
    public async Task ReadingsSurviveALoadFromDisk()
    {
        var store = NewStore();
        store.Record("a", 4.6, 992, 9, 141);
        await store.FlushAsync();

        var reloaded = NewStore();
        await reloaded.LoadAsync();

        var day = Assert.Single(reloaded.GetHistory("a"));
        Assert.Equal(992, day.LifetimeTotal);
        Assert.Equal(4.6, day.Rating);
    }

    [Fact]
    public async Task LoadingWithNoFileIsNotAnError()
    {
        var store = NewStore();
        await store.LoadAsync();
        Assert.Empty(store.GetHistory("a"));
    }
}
