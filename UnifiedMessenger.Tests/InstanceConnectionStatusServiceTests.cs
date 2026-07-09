using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class InstanceConnectionStatusServiceTests
{
    [Fact]
    public void SetConnected_UpdatesStatus()
    {
        var service = new InstanceConnectionStatusService();

        service.SetInitializing("branch-1");
        service.SetConnected("branch-1", "Signed in");

        Assert.Equal(InstanceConnectionStatus.Connected, service.GetStatus("branch-1"));
        Assert.Equal("Signed in", service.GetDetail("branch-1"));
    }

    [Theory]
    [InlineData("Connected", InstanceConnectionStatus.Connected)]
    [InlineData("connected", InstanceConnectionStatus.Connected)]
    [InlineData("LoggedOut", InstanceConnectionStatus.LoggedOut)]
    [InlineData("loggedout", InstanceConnectionStatus.LoggedOut)]
    [InlineData("Error", InstanceConnectionStatus.Error)]
    public void ParseStatus_MapsHandshakePayload(string raw, InstanceConnectionStatus expected)
    {
        Assert.Equal(expected, InstanceConnectionStatusService.ParseStatus(raw));
    }

    [Fact]
    public void Changed_IncludesInstanceId()
    {
        var service = new InstanceConnectionStatusService();
        string? capturedId = null;

        service.Changed += (_, args) => capturedId = args.InstanceId;

        service.SetInitializing("branch-9");
        service.SetConnected("branch-9", "ok");

        Assert.Equal("branch-9", capturedId);
    }

    [Fact]
    public void IsValidTransition_AllowsConnectedAfterInitializing()
    {
        Assert.True(InstanceConnectionStatusService.IsValidTransition(
            InstanceConnectionStatus.Initializing,
            InstanceConnectionStatus.Connected));
    }

    [Fact]
    public void SetConnected_RaisesChangedWhenDetailUpdatesWithoutStatusChange()
    {
        var service = new InstanceConnectionStatusService();
        var instanceId = $"detail-{Guid.NewGuid():N}";
        var changeCount = 0;

        service.Changed += (_, _) => changeCount++;

        service.SetInitializing(instanceId);
        service.SetConnected(instanceId, null);
        var afterConnect = changeCount;

        service.SetConnected(instanceId, "Connected · awaiting view context");

        Assert.Equal(afterConnect + 1, changeCount);
        Assert.Equal("Connected · awaiting view context", service.GetDetail(instanceId));
    }
}
