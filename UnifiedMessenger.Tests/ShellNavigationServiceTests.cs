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
        InstanceNavigationRequest? request = null;
        service.InstanceNavigationRequested += (_, navigation) => request = navigation;

        service.RequestInstance(instanceId!);

        Assert.Null(request);
    }

    [Fact]
    public void RequestInstance_TrimsAndRaisesEvent()
    {
        var service = ShellNavigationService.CreateForTests();
        InstanceNavigationRequest? request = null;
        service.InstanceNavigationRequested += (_, navigation) => request = navigation;

        service.RequestInstance("  inst-whatsapp  ");

        Assert.NotNull(request);
        Assert.Equal("inst-whatsapp", request!.InstanceId);
        Assert.False(request.HasConversationTarget);
    }

    [Fact]
    public void RequestInstance_WithConversationTarget_IncludesConversationFields()
    {
        var service = ShellNavigationService.CreateForTests();
        InstanceNavigationRequest? request = null;
        service.InstanceNavigationRequested += (_, navigation) => request = navigation;

        service.RequestInstance("inst-meta", "Sara Khan", "Sara Khan");

        Assert.NotNull(request);
        Assert.Equal("inst-meta", request!.InstanceId);
        Assert.Equal("Sara Khan", request.ConversationKey);
        Assert.Equal("Sara Khan", request.CustomerName);
        Assert.True(request.HasConversationTarget);
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

    [Fact]
    public void RequestAddInstance_RaisesEvent()
    {
        var service = ShellNavigationService.CreateForTests();
        var requestCount = 0;
        service.AddInstanceRequested += (_, _) => requestCount++;

        service.RequestAddInstance();

        Assert.Equal(1, requestCount);
    }

    [Theory]
    [InlineData("inst-1", true)]
    [InlineData(" ", false)]
    public void IsValidInstanceId_ValidatesInput(string instanceId, bool expected)
    {
        Assert.Equal(expected, ShellNavigationService.IsValidInstanceId(instanceId));
    }
}
