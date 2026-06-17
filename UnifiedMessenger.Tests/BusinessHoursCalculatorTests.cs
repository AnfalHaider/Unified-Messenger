using System.Linq;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using Xunit;

namespace UnifiedMessenger.Tests;

public class BusinessHoursCalculatorTests
{
    private static DateTimeOffset Local(int year, int month, int day, int hour, int minute)
    {
        var dt = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(dt, TimeZoneInfo.Local.GetUtcOffset(dt));
    }

    [Fact]
    public void Disabled_ReturnsRawElapsedMinutes()
    {
        var start = Local(2026, 6, 15, 17, 0);
        var end = Local(2026, 6, 15, 17, 30);
        var hours = new BusinessHours { Enabled = false };

        Assert.Equal(30, BusinessHoursCalculator.ElapsedBusinessMinutes(start, end, hours), 1);
    }

    [Fact]
    public void Null_ReturnsRawElapsedMinutes()
    {
        var start = Local(2026, 6, 15, 0, 0);
        var end = Local(2026, 6, 15, 1, 0);

        Assert.Equal(60, BusinessHoursCalculator.ElapsedBusinessMinutes(start, end, null), 1);
    }

    [Fact]
    public void PausesOutsideHours_AcrossTwoDays()
    {
        var start = Local(2026, 6, 15, 17, 0);
        var end = Local(2026, 6, 16, 10, 0);
        var hours = new BusinessHours
        {
            Enabled = true,
            OpenMinutes = 9 * 60,
            CloseMinutes = 18 * 60,
            WorkingDays = [0, 1, 2, 3, 4, 5, 6]
        };

        Assert.Equal(120, BusinessHoursCalculator.ElapsedBusinessMinutes(start, end, hours), 1);
    }

    [Fact]
    public void EntirelyOutsideHours_CountsZero()
    {
        var start = Local(2026, 6, 15, 19, 0);
        var end = Local(2026, 6, 15, 23, 0);
        var hours = new BusinessHours
        {
            Enabled = true,
            OpenMinutes = 9 * 60,
            CloseMinutes = 18 * 60,
            WorkingDays = [0, 1, 2, 3, 4, 5, 6]
        };

        Assert.Equal(0, BusinessHoursCalculator.ElapsedBusinessMinutes(start, end, hours), 1);
    }

    [Fact]
    public void NonWorkingDay_IsSkipped()
    {
        var start = Local(2026, 6, 15, 10, 0);
        var end = Local(2026, 6, 15, 12, 0);
        var dayOfWeek = (int)start.LocalDateTime.DayOfWeek;
        var hours = new BusinessHours
        {
            Enabled = true,
            OpenMinutes = 9 * 60,
            CloseMinutes = 18 * 60,
            WorkingDays = Enumerable.Range(0, 7).Where(d => d != dayOfWeek).ToList()
        };

        Assert.Equal(0, BusinessHoursCalculator.ElapsedBusinessMinutes(start, end, hours), 1);
    }
}
