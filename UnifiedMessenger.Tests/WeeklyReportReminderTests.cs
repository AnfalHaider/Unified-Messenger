using System;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using Xunit;

namespace UnifiedMessenger.Tests;

public class WeeklyReportReminderTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsDue_False_WhenDisabled()
    {
        var settings = new AppSettings
        {
            WeeklyReportReminderEnabled = false,
            WeeklyReportLastShownUtc = Now - TimeSpan.FromDays(30)
        };

        Assert.False(WeeklyReportReminder.IsDue(settings, Now));
    }

    [Fact]
    public void IsDue_False_WhenNoBaselineYet()
    {
        var settings = new AppSettings { WeeklyReportReminderEnabled = true, WeeklyReportLastShownUtc = null };

        Assert.False(WeeklyReportReminder.IsDue(settings, Now));
        Assert.True(WeeklyReportReminder.NeedsBaseline(settings));
    }

    [Fact]
    public void IsDue_False_WithinTheWeek()
    {
        var settings = new AppSettings
        {
            WeeklyReportReminderEnabled = true,
            WeeklyReportLastShownUtc = Now - TimeSpan.FromDays(6)
        };

        Assert.False(WeeklyReportReminder.IsDue(settings, Now));
    }

    [Fact]
    public void IsDue_True_AfterAWeek()
    {
        var settings = new AppSettings
        {
            WeeklyReportReminderEnabled = true,
            WeeklyReportLastShownUtc = Now - TimeSpan.FromDays(7)
        };

        Assert.True(WeeklyReportReminder.IsDue(settings, Now));
        Assert.False(WeeklyReportReminder.NeedsBaseline(settings));
    }

    [Fact]
    public void NeedsBaseline_False_WhenDisabled()
    {
        var settings = new AppSettings { WeeklyReportReminderEnabled = false, WeeklyReportLastShownUtc = null };

        Assert.False(WeeklyReportReminder.NeedsBaseline(settings));
    }
}
