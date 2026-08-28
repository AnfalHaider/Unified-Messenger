using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// The branch filter behind the Reports and Analytics scope selectors.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BranchWorkspaceHelper.FilterByBranchKey"/> shipped with the branch model and had
/// <b>no caller anywhere in the app</b> until v4.99.71 — so it also had no coverage. These are the cases
/// that matter now that two screens and two file exports depend on it.
/// </para>
/// <para>
/// The dangerous one is <see cref="AKeyThatMatchesNothingReturnsNothing"/>. A filter that falls back to
/// "everything" when it recognises no branch would put the whole business on screen under one branch's
/// name, and write that to the .md and .csv exports — a wrong document that reads as a right one.
/// </para>
/// </remarks>
public class BranchFilterTests
{
    private static MessengerInstance Account(string id, string displayName, string? branchKey = null) =>
        new() { Id = id, DisplayName = displayName, BranchKey = branchKey };

    private static readonly MessengerInstance[] Accounts =
    [
        Account("a", "Depilex DHA-2 WhatsApp", "DHA-2"),
        Account("b", "Google Depilex DHA-2", "DHA-2"),
        Account("c", "Depilex F-11 WhatsApp", "F-11"),
        Account("d", "Depilex Men DHA-2 WhatsApp", "Men-DHA-2")
    ];

    [Fact]
    public void NoSelectionMeansEveryAccount()
    {
        Assert.Equal(4, BranchWorkspaceHelper.FilterByBranchKey(Accounts, null).Count());
        Assert.Equal(4, BranchWorkspaceHelper.FilterByBranchKey(Accounts, string.Empty).Count());
        Assert.Equal(4, BranchWorkspaceHelper.FilterByBranchKey(Accounts, "   ").Count());
    }

    [Fact]
    public void ABranchReturnsOnlyItsOwnAccounts()
    {
        var dha2 = BranchWorkspaceHelper.FilterByBranchKey(Accounts, "DHA-2").ToList();

        Assert.Equal(2, dha2.Count);
        Assert.All(dha2, a => Assert.Equal("DHA-2", a.BranchKey));

        // "Men-DHA-2" must not be swept in by "DHA-2" — the branch keys here genuinely overlap as substrings,
        // which is why the comparison is equality and not Contains.
        Assert.DoesNotContain(dha2, a => a.Id == "d");
    }

    [Fact]
    public void AKeyThatMatchesNothingReturnsNothing()
    {
        // NOT "everything". A filter that falls open on an unrecognised branch would show the whole
        // business under one branch's heading and export it that way.
        Assert.Empty(BranchWorkspaceHelper.FilterByBranchKey(Accounts, "Karachi"));
    }

    [Fact]
    public void MatchingIsCaseInsensitiveAndIgnoresSurroundingSpace()
    {
        Assert.Equal(2, BranchWorkspaceHelper.FilterByBranchKey(Accounts, "dha-2").Count());
        Assert.Equal(2, BranchWorkspaceHelper.FilterByBranchKey(Accounts, "  DHA-2  ").Count());
    }

    [Fact]
    public void AnAccountWithNoBranchKeySetStillResolvesFromItsName()
    {
        // BranchKey is optional; ResolveBranchKey falls back to BranchNameResolver over the display name,
        // so an owner who never opened "Set location" still gets a usable filter rather than one empty
        // group holding everything.
        var unset = Account("e", "Depilex F-11 WhatsApp");
        var resolved = BranchWorkspaceHelper.ResolveBranchKey(unset);

        Assert.False(string.IsNullOrWhiteSpace(resolved));
        Assert.Single(BranchWorkspaceHelper.FilterByBranchKey([unset], resolved));
    }

    [Fact]
    public void TheReportSaysWhichBranchItCovers()
    {
        // The label is what reaches the Markdown export — BusinessReport writes
        // "# Business report — {PeriodLabel}". A single-branch report saved to a file that does not name the
        // branch stops being a report and becomes a wrong document the moment it leaves the app.
        var scoped = DashboardReportHelper.GatherInputs([], periodDays: 7, scopeLabel: "F-11");
        var all = DashboardReportHelper.GatherInputs([], periodDays: 7);

        Assert.Contains("F-11", scoped.PeriodLabel, StringComparison.Ordinal);
        Assert.DoesNotContain("·", all.PeriodLabel, StringComparison.Ordinal);
    }
}
