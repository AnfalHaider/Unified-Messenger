using System;
using System.IO;
using UnifiedMessenger.Services;
using Xunit;

namespace UnifiedMessenger.Tests;

public class ContactHistoryStoreTests
{
    private static string TempStore() =>
        Path.Combine(Path.GetTempPath(), "um-contact-tests", Path.GetRandomFileName() + ".json");

    private static readonly DateTimeOffset Now = new(2026, 7, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WeekStart = Now.AddDays(-7);

    [Fact]
    public void GetInsight_ClassifiesNewVsReturning()
    {
        var store = new ContactHistoryStore(TempStore());

        // Returning: first seen 20 days ago, active again this week.
        store.Observe("inst-1", "1111@c.us", "1111", Now.AddDays(-20));
        store.Observe("inst-1", "1111@c.us", "1111", Now.AddDays(-1));

        // New: first (and only) seen this week.
        store.Observe("inst-1", "2222@c.us", "2222", Now.AddDays(-2));

        // Known before but NOT active this week — excluded from both counts.
        store.Observe("inst-1", "3333@c.us", "3333", Now.AddDays(-30));

        var insight = store.GetInsight(["inst-1"], WeekStart, Now);

        Assert.True(insight.HasEnoughHistory); // earliest first-seen is 30 days ago
        Assert.Equal(1, insight.NewCount);
        Assert.Equal(1, insight.ReturningCount);
        Assert.Equal(2, insight.ActiveThisWeek);
        Assert.Equal(50, insight.ReturningRatePercent);
    }

    [Fact]
    public void GetInsight_NotEnoughHistory_WhenYoungerThanAWeek()
    {
        var store = new ContactHistoryStore(TempStore());
        store.Observe("inst-1", "1111@c.us", "1111", Now.AddDays(-2)); // earliest only 2 days ago

        var insight = store.GetInsight(["inst-1"], WeekStart, Now);

        Assert.False(insight.HasEnoughHistory);
    }

    [Fact]
    public void Observe_PhoneIdentity_DedupesSavedAndUnsaved()
    {
        var store = new ContactHistoryStore(TempStore());
        // Same phone via two different JIDs (e.g. @lid then @c.us) must be ONE contact.
        store.Observe("inst-1", "999@lid", "923105325598", Now.AddDays(-20));
        store.Observe("inst-1", "923105325598@c.us", "923105325598", Now.AddDays(-1));

        var insight = store.GetInsight(["inst-1"], WeekStart, Now);

        Assert.Equal(1, insight.ActiveThisWeek);   // one contact, not two
        Assert.Equal(0, insight.NewCount);          // first seen 20 days ago → returning
        Assert.Equal(1, insight.ReturningCount);
    }

    [Theory]
    [InlineData("12036300000@g.us", "", null)]        // group
    [InlineData("status@broadcast", "", null)]        // status
    [InlineData("0@broadcast", "", null)]             // broadcast
    [InlineData("111@newsletter", "", null)]          // channel
    [InlineData("923105325598@c.us", "923105325598", "p:923105325598")] // phone wins
    [InlineData("abc@lid", "", "k:abc@lid")]          // no phone → JID key
    public void ResolveContactKey_FiltersGroupsAndPrefersPhone(string jid, string phone, string? expected)
    {
        Assert.Equal(expected, ContactHistoryStore.ResolveContactKey(jid, phone));
    }
}
