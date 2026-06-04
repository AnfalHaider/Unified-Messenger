using System.Diagnostics;
using System.Text.Json;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services.Adapters;

public static class DashboardScrapeStatusHandler
{
    public const string GoogleViewContext = "google-view-context";

    public const string ViewStateLocationsDirectory = "locations-directory";

    public const string ViewStateDeepData = "deep-data";

    public const string AwaitingViewContextDetail = "Connected · awaiting view context";

    public static void Apply(JsonElement root, MessengerInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var success = ParseSuccess(root);
        var context = ReadString(root, "context") ?? "scrape";

        if (context.Equals(GoogleViewContext, StringComparison.OrdinalIgnoreCase))
        {
            ApplyGoogleViewContext(root, instance, success);
            return;
        }

        DashboardScrapeStatusService.Instance.Record(
            instance.Id,
            success,
            context,
            ReadString(root, "detail"));

        if (success)
        {
            return;
        }

        var detail = ReadString(root, "detail") ?? "Scrape failed";
        Debug.WriteLine(
            $"Dashboard scrape failed for {instance.Id} ({instance.Platform}) [{context}]: {detail}");
        AdapterHealthMonitor.Instance.RecordHeartbeat(
            instance.Id,
            PlatformDefinition.NormalizePlatformId(instance.Platform));
    }

    private static void ApplyGoogleViewContext(JsonElement root, MessengerInstance instance, bool success)
    {
        var viewState = ReadString(root, "viewState");
        var detail = ReadString(root, "detail");
        var connection = InstanceConnectionStatusService.Instance;

        if (!success)
        {
            Debug.WriteLine(
                $"Google view context failed for {instance.Id}: {detail ?? "unknown"}");
            connection.SetConnected(instance.Id, detail ?? "Connected · awaiting view context");
            return;
        }

        if (viewState.Equals(ViewStateLocationsDirectory, StringComparison.OrdinalIgnoreCase) ||
            (detail?.Contains("awaiting view context", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            connection.SetConnected(
                instance.Id,
                string.IsNullOrWhiteSpace(detail) ? AwaitingViewContextDetail : detail.Trim());
        }
        else if (viewState.Equals(ViewStateDeepData, StringComparison.OrdinalIgnoreCase))
        {
            connection.SetConnected(instance.Id, null);
        }
        else if (!string.IsNullOrWhiteSpace(detail))
        {
            connection.SetConnected(instance.Id, detail.Trim());
        }

        AdapterHealthMonitor.Instance.RecordHeartbeat(
            instance.Id,
            PlatformDefinition.NormalizePlatformId(instance.Platform));
    }

    private static bool ParseSuccess(JsonElement root)
    {
        if (!root.TryGetProperty("success", out var successElement))
        {
            return true;
        }

        return successElement.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => successElement.GetString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true,
            _ => true
        };
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }
}
