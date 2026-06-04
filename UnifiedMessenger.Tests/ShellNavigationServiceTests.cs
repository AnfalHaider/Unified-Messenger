using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class ShellNavigationServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RequestInstance_IgnoresInvalidInstanceIds(string? instanceId)
    {
        var service = ShellNavigationService.CreateForTests();
        string? launchedId = null;
        service.InstanceLaunchRequested += (_, id) => launchedId = id;

        service.RequestInstance(instanceId!);

        Assert.Null(launchedId);
    }

    [Fact]
    public void RequestInstance_TrimsAndRaisesEvent()
    {
        var service = ShellNavigationService.CreateForTests();
        string? launchedId = null;
        service.InstanceLaunchRequested += (_, id) => launchedId = id;

        service.RequestInstance("  inst-whatsapp  ");

        Assert.Equal("inst-whatsapp", launchedId);
    }

    [Fact]
    public void RequestArchivedInstanceRestore_TrimsAndRaisesEvent()
    {
        var service = ShellNavigationService.CreateForTests();
        string? restoredId = null;
        service.ArchivedInstanceRestoreRequested += (_, id) => restoredId = id;

        service.RequestArchivedInstanceRestore(" archived-1 ");

        Assert.Equal("archived-1", restoredId);
    }

    [Fact]
    public void RequestDashboardRefresh_RaisesRefreshEvent()
    {
        var service = ShellNavigationService.CreateForTests();
        var refreshCount = 0;
        service.DashboardRefreshRequested += (_, _) => refreshCount++;

        service.RequestDashboardRefresh();

        Assert.Equal(1, refreshCount);
    }

    [Theory]
    [InlineData("inst-1", true)]
    [InlineData(" ", false)]
    public void IsValidInstanceId_ValidatesInput(string instanceId, bool expected)
    {
        Assert.Equal(expected, ShellNavigationService.IsValidInstanceId(instanceId));
    }
}
