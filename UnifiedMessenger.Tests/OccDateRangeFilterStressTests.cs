using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Stage 4 sabotage probe: rapid OccDateRangeFilterState switching (50 ms, 10 cycles).
/// UI debounce (300 ms) lives in OperationsCommandCenter — this validates filter state integrity.
/// </summary>
public class OccDateRangeFilterStressTests
{
    [Fact]
    public async Task RapidFilterChanges_10CyclesAt50Ms_ProducesConsistentFinalRange()
    {
        var filter = OccDateRangeFilterState.CreateForTests();
        var changeCount = 0;
        filter.Changed += (_, _) => Interlocked.Increment(ref changeCount);

        for (var cycle = 0; cycle < 10; cycle++)
        {
            var now = DateTimeOffset.Now;
            filter.FromUtc = now.AddDays(-7);
            filter.ToUtc = now;
            await Task.Delay(50);

            filter.FromUtc = now.AddDays(-30);
            filter.ToUtc = now;
            await Task.Delay(50);

            filter.ResetToDefaultWindow();
            await Task.Delay(50);
        }

        Assert.True(filter.HasActiveFilter);
        Assert.NotNull(filter.FromUtc);
        Assert.NotNull(filter.ToUtc);

        var spanDays = (filter.ToUtc!.Value.Date - filter.FromUtc!.Value.Date).Days + 1;
        Assert.Equal(OccDateRangeFilterState.DefaultWindowDays, spanDays);
        Assert.True(changeCount >= 10, $"Expected multiple Changed events, got {changeCount}");
    }

    [Fact]
    public async Task RapidFromUtcOnlyChanges_DoesNotThrowOrDeadlock()
    {
        var filter = OccDateRangeFilterState.CreateForTests();
        filter.ResetToDefaultWindow();

        for (var i = 0; i < 100; i++)
        {
            filter.FromUtc = DateTimeOffset.Now.AddDays(-i);
        }

        await Task.Delay(100);

        Assert.True(filter.HasActiveFilter);
        Assert.NotNull(filter.FromUtc);
    }
}
