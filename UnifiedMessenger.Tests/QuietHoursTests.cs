using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class QuietHoursTests
{
    private static AppSettings On(int start, int end) =>
        new() { QuietHoursEnabled = true, QuietHoursStartHour = start, QuietHoursEndHour = end };

    [Fact]
    public void Disabled_IsNeverQuiet()
    {
        var settings = new AppSettings { QuietHoursEnabled = false, QuietHoursStartHour = 21, QuietHoursEndHour = 8 };
        Assert.False(QuietHours.IsQuiet(settings, 2));
    }

    [Theory]
    [InlineData(22, true)]   // inside overnight window 21→8
    [InlineData(2, true)]
    [InlineData(7, true)]
    [InlineData(8, false)]   // end is exclusive
    [InlineData(12, false)]
    [InlineData(20, false)]
    [InlineData(21, true)]   // start is inclusive
    public void OvernightWindow_WrapsPastMidnight(int hour, bool expected)
    {
        Assert.Equal(expected, QuietHours.IsQuiet(On(21, 8), hour));
    }

    [Theory]
    [InlineData(13, true)]   // same-day window 12→14
    [InlineData(11, false)]
    [InlineData(14, false)]
    public void SameDayWindow_Works(int hour, bool expected)
    {
        Assert.Equal(expected, QuietHours.IsQuiet(On(12, 14), hour));
    }

    [Fact]
    public void ZeroLengthWindow_IsNeverQuiet()
    {
        Assert.False(QuietHours.IsQuiet(On(9, 9), 9));
    }
}
