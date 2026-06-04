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
        service.SetConnected("branch-1", "Signed-in UI detected");

        Assert.Equal(InstanceConnectionStatus.Connected, service.GetStatus("branch-1"));
        Assert.Equal("Signed-in UI detected", service.GetDetail("branch-1"));
    }

    [Theory]
    [InlineData("Connected", InstanceConnectionStatus.Connected)]
    [InlineData("LoggedOut", InstanceConnectionStatus.LoggedOut)]
    [InlineData("Error", InstanceConnectionStatus.Error)]
    public void ParseStatus_MapsHandshakePayload(string raw, InstanceConnectionStatus expected)
    {
        Assert.Equal(expected, InstanceConnectionStatusService.ParseStatus(raw));
    }

    [Fact]
    public void IsValidTransition_AllowsConnectedAfterInitializing()
    {
        Assert.True(InstanceConnectionStatusService.IsValidTransition(
            InstanceConnectionStatus.Initializing,
            InstanceConnectionStatus.Connected));
    }
}
