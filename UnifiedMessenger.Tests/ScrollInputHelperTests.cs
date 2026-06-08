using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class ScrollInputHelperTests
{
    [Theory]
    [InlineData(0, 100, 120, 0)]
    [InlineData(100, 100, 20, 80)]
    [InlineData(100, 100, -40, 100)]
    public void ComputeBubbledOffset_ClampsWithinScrollableRange(
        double currentOffset,
        double scrollableHeight,
        int wheelDelta,
        double expected)
    {
        Assert.Equal(
            expected,
            ScrollInputHelper.ComputeBubbledOffset(currentOffset, scrollableHeight, wheelDelta));
    }
}
