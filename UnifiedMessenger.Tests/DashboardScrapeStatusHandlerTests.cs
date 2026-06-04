using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Adapters;

namespace UnifiedMessenger.Tests;

public class DashboardScrapeStatusHandlerTests
{
    [Fact]
    public void Apply_GoogleViewContext_LocationsDirectory_SetsAwaitingDetail()
    {
        var service = InstanceConnectionStatusService.Instance;
        var instanceId = $"gbp-loc-{Guid.NewGuid():N}";
        service.SetInitializing(instanceId);
        var instance = new MessengerInstance
        {
            Id = instanceId,
            DisplayName = "Depilex F-11",
            Platform = "googlebusiness",
            Category = WorkspaceCategory.Professional
        };

        using var document = JsonDocument.Parse(
            """
            {
              "type": "dashboard-scrape-status",
              "success": true,
              "context": "google-view-context",
              "viewState": "locations-directory",
              "detail": "Connected · awaiting view context"
            }
            """);

        DashboardScrapeStatusHandler.Apply(document.RootElement, instance);

        Assert.Equal(InstanceConnectionStatus.Connected, service.GetStatus(instanceId));
        Assert.Equal(
            DashboardScrapeStatusHandler.AwaitingViewContextDetail,
            service.GetDetail(instanceId));
    }

    [Fact]
    public void Apply_GoogleViewContext_DeepData_ClearsAwaitingDetail()
    {
        var service = InstanceConnectionStatusService.Instance;
        var instanceId = $"gbp-deep-{Guid.NewGuid():N}";
        service.SetInitializing(instanceId);
        service.SetConnected(instanceId, DashboardScrapeStatusHandler.AwaitingViewContextDetail);

        var instance = new MessengerInstance
        {
            Id = instanceId,
            DisplayName = "Depilex F-11",
            Platform = "googlebusiness",
            Category = WorkspaceCategory.Professional
        };

        using var document = JsonDocument.Parse(
            """
            {
              "type": "dashboard-scrape-status",
              "success": true,
              "context": "google-view-context",
              "viewState": "deep-data",
              "detail": ""
            }
            """);

        DashboardScrapeStatusHandler.Apply(document.RootElement, instance);

        Assert.Equal(InstanceConnectionStatus.Connected, service.GetStatus(instanceId));
        Assert.Null(service.GetDetail(instanceId));
    }
}
