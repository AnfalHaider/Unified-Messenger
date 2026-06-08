using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class BranchWorkspacePillBarTests
{
    [Fact]
    public void FormatBranchPillLabel_TrimsWhitespace()
    {
        Assert.Equal("DHA-2", BranchWorkspaceHelper.FormatBranchPillLabel(" DHA-2 "));
    }

    [Fact]
    public void FormatBranchPillBadge_ShowsOpenCountOnlyWhenPositive()
    {
        Assert.Equal("3", BranchWorkspaceHelper.FormatBranchPillBadge(new BranchWorkspaceHelper.BranchTabCounts(3, 1)));
        Assert.Equal(string.Empty, BranchWorkspaceHelper.FormatBranchPillBadge(new BranchWorkspaceHelper.BranchTabCounts(0, 2)));
    }

    [Fact]
    public void FormatBranchPillTooltip_MatchesTabHeaderFormat()
    {
        var counts = new BranchWorkspaceHelper.BranchTabCounts(2, 1);
        Assert.Equal(
            BranchWorkspaceHelper.FormatBranchTabHeader("DHA-2", counts),
            BranchWorkspaceHelper.FormatBranchPillTooltip("DHA-2", counts));
    }
}
