using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class ActiveWorkspaceContextTests
{
    [Fact]
    public void SetActiveInstance_TracksVisibleWorkspace()
    {
        ActiveWorkspaceContext.SetActiveInstance("inst-a");

        Assert.True(ActiveWorkspaceContext.IsInstanceActive("inst-a"));
        Assert.False(ActiveWorkspaceContext.IsInstanceActive("inst-b"));
        Assert.False(ActiveWorkspaceContext.IsDashboardVisible);
    }

    [Fact]
    public void SetDashboardVisible_ClearsActiveInstance()
    {
        ActiveWorkspaceContext.SetActiveInstance("inst-a");
        ActiveWorkspaceContext.SetDashboardVisible();

        Assert.Null(ActiveWorkspaceContext.CurrentInstanceId);
        Assert.True(ActiveWorkspaceContext.IsDashboardVisible);
    }
}
