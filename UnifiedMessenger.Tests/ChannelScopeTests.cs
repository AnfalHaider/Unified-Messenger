using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// The Analytics page and the business report draw their figures from the WhatsApp IndexedDB pipeline and
/// presented them as covering every account. For an owner with Google Business accounts connected that is
/// already false — "Share of message volume across your accounts" was rendered from a subset — and it gets
/// worse, invisibly, the moment a second measured channel lands.
///
/// These pin the scope statement itself, and that one implementation feeds both surfaces.
/// </summary>
public class ChannelScopeTests
{
    private static MessengerInstance Account(string platform, string name) =>
        new() { Id = $"{platform}-{name}", DisplayName = name, Platform = platform };

    [Fact]
    public void AnEmptySetGetsNoScopeLine()
    {
        // The caller's no-accounts empty state says this better than a scope line over nothing.
        Assert.Equal(string.Empty, ChannelScope.Describe([]));
        Assert.Equal(string.Empty, ChannelScope.Describe(null));
    }

    [Fact]
    public void AllMeasuredSaysSoRatherThanStayingSilent()
    {
        // Saying "covers all" on good days is what makes the excluded case legible when it appears. A line
        // that only shows up when something is wrong teaches the reader to ignore its absence.
        var line = ChannelScope.Describe([Account("whatsapp", "A"), Account("whatsappbusiness", "B")]);

        Assert.Equal("Covers all 2 accounts.", line);
    }

    [Fact]
    public void ExcludedChannelsAreNamedAndCounted()
    {
        // The exact case the owner has today: WhatsApp accounts charted, Google Business accounts silently
        // dropped by the pipeline gate.
        var line = ChannelScope.Describe(
        [
            Account("whatsapp", "DHA-2"),
            Account("whatsapp", "F-11"),
            Account("googlebusiness", "DHA-2 reviews"),
            Account("googlebusiness", "F-11 reviews"),
            Account("googlebusiness", "Men DHA-2 reviews")
        ]);

        Assert.Equal("Covers 2 of 5 accounts — 3 Google Business not measured here.", line);
    }

    [Fact]
    public void SeveralExcludedChannelsReadAsASentence()
    {
        var line = ChannelScope.Describe(
        [
            Account("whatsapp", "A"),
            Account("googlebusiness", "G1"),
            Account("googlebusiness", "G2"),
            Account("discord", "D1")
        ]);

        Assert.Equal("Covers 1 of 4 accounts — 2 Google Business and 1 Discord not measured here.", line);
    }

    [Fact]
    public void NothingMeasuredStillStatesTheScope()
    {
        // "Covers 0 of 2" is an honest and useful sentence. Suppressing it would leave a page of zeros with
        // no explanation, which is the "never show 0 where the truth is unknown" rule inverted.
        var line = ChannelScope.Describe([Account("googlebusiness", "G1"), Account("generic", "X")]);

        Assert.StartsWith("Covers 0 of 2 accounts —", line);
    }

    [Fact]
    public void TheScopeLineIsDerivedNotHardcodedToWhatsApp()
    {
        // A scope line that says "WhatsApp" from a constant would keep saying it after a second channel
        // starts contributing. Nothing in the output may name a channel that is actually covered.
        var line = ChannelScope.Describe([Account("whatsapp", "A"), Account("googlebusiness", "G")]);

        Assert.DoesNotContain("WhatsApp", line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnUnknownPlatformCountsAsCoveredBecauseThePipelineTreatsItAsWhatsApp()
    {
        // Pinned deliberately, and it is NOT the answer I expected when writing this file.
        //
        // PlatformDefinition.CapabilitiesFor runs the id through NormalizePlatformId, which falls back to
        // "whatsapp" for anything unrecognised — so an account with a corrupt or unknown platform id
        // resolves to the WhatsApp capability set and IS scanned by the WhatsApp pipeline. Counting it as
        // excluded would make the scope line describe an app that does not exist: it would promise the
        // account is not measured while the scanner measures it.
        //
        // The scope line's job is to say what the app actually does. If the fallback ever changes, this
        // test fails and the sentence gets revisited with it.
        var line = ChannelScope.Describe([Account("whatsapp", "A"), Account("not-a-real-platform", "?")]);

        Assert.Equal("Covers all 2 accounts.", line);
    }

    [Fact]
    public void TheReportCarriesTheSameScopeLineIntoItsMarkdown()
    {
        // A saved .md outlives the screen that explained it. An exported document with no scope becomes a
        // wrong document the moment it is forwarded.
        var input = Inputs(scope: "Covers 2 of 5 accounts — 3 Google Business not measured here.");

        var markdown = BusinessReport.Build(input).Markdown;

        Assert.Contains("Covers 2 of 5 accounts — 3 Google Business not measured here.", markdown);
    }

    [Fact]
    public void TheReportsPerAccountTableNamesTheChannel()
    {
        // Without the column, a "—" in the reply-time cell reads as broken rather than as not-measurable.
        var input = Inputs(
            scope: "Covers all 1 accounts.",
            accounts: [new AccountReportLine("DHA-2", 40, 12, 5, 2, "WhatsApp")]);

        var markdown = BusinessReport.Build(input).Markdown;

        Assert.Contains("| Account | Channel | Messages | Median reply | Waiting now |", markdown);
        Assert.Contains("| DHA-2 | WhatsApp |", markdown);
    }

    [Fact]
    public void AReportWithNoScopeLineStillBuilds()
    {
        // ChannelScopeLine defaults to empty for every existing caller and test; an absent scope must not
        // emit a stray blank emphasis line.
        var markdown = BusinessReport.Build(Inputs(scope: "")).Markdown;

        Assert.DoesNotContain("__", markdown);
        Assert.Contains("# Business report", markdown);
    }

    private static ReportInputs Inputs(string scope, IReadOnlyList<AccountReportLine>? accounts = null) =>
        new(
            PeriodLabel: "Aug 22 – Aug 29, 2026",
            MessagesThisWeek: 40,
            MessagesLastWeek: 30,
            MedianFrtThisWeekMinutes: 12,
            FrtSamplesThisWeek: 5,
            MedianFrtLastWeekMinutes: 14,
            FrtSamplesLastWeek: 4,
            SlaMetPercent: 60,
            SlaThresholdMinutes: 15,
            AnsweredThisWeek: 5,
            AwaitingNow: 2,
            BusiestDay: "Friday",
            BusiestHour: "5 PM",
            Accounts: accounts ?? [new AccountReportLine("DHA-2", 40, 12, 5, 2, "WhatsApp")],
            ChannelScopeLine: scope);
}
