using System.Diagnostics;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed class ResourceSnapshot
{
    public int ActiveAccountCount { get; init; }

    public int TotalUnreadCount { get; init; }

    /// <summary>Working set of the app's own process only (excludes WebView2 child processes).</summary>
    public long WorkingSetMegabytes { get; init; }

    /// <summary>Summed working set of all <c>msedgewebview2</c> child processes — where most RAM actually lives.</summary>
    public long WebView2WorkingSetMegabytes { get; init; }

    /// <summary>Number of live <c>msedgewebview2</c> processes (browser + renderer + GPU + utility across sessions).</summary>
    public int WebView2ProcessCount { get; init; }

    /// <summary>App process + all WebView2 child processes — the honest total RAM footprint.</summary>
    public long TotalWorkingSetMegabytes { get; init; }

    public string VisibleInstanceName { get; init; } = "None";

    public IReadOnlyList<InstanceResourceTile> InstanceTiles { get; init; } = [];
}

public sealed class InstanceResourceTile
{
    public required string InstanceId { get; init; }

    public required string DisplayName { get; init; }

    public required string Platform { get; init; }

    public required string AccentColor { get; init; }

    public required string IconGlyph { get; init; }

    public int UnreadCount { get; init; }

    public AdapterHealthState HealthState { get; init; }

    public bool IsVisible { get; init; }

    public string MemoryTier { get; init; } = MemoryTierPreference.Normal.ToString();
}

public sealed class ResourceMonitorService
{
    private const long MegabyteDivisor = 1024 * 1024;

    private static readonly Lazy<ResourceMonitorService> LazyInstance = new(() => new ResourceMonitorService());

    private Func<long>? _workingSetBytesProvider;
    private Func<IReadOnlyList<long>>? _webViewWorkingSetsProvider;

    public static ResourceMonitorService Instance => LazyInstance.Value;

    internal ResourceMonitorService()
    {
    }

    internal static ResourceMonitorService CreateForTests(
        Func<long>? workingSetBytesProvider = null,
        Func<IReadOnlyList<long>>? webViewWorkingSetsProvider = null) =>
        new()
        {
            _workingSetBytesProvider = workingSetBytesProvider,
            _webViewWorkingSetsProvider = webViewWorkingSetsProvider
        };

    public ResourceSnapshot Capture(
        IEnumerable<MessengerInstance> instances,
        IInstanceSessionManager sessionManager,
        INotificationHubService notificationHub,
        AdapterHealthMonitor healthMonitor)
    {
        ArgumentNullException.ThrowIfNull(instances);
        ArgumentNullException.ThrowIfNull(sessionManager);
        ArgumentNullException.ThrowIfNull(notificationHub);
        ArgumentNullException.ThrowIfNull(healthMonitor);

        var instanceList = instances
            .Where(instance => !string.IsNullOrWhiteSpace(instance.Id))
            .OrderBy(instance => instance.SortOrder)
            .ThenBy(instance => instance.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var visibleId = sessionManager.VisibleInstanceId;
        var tiles = instanceList
            .Select(instance => BuildTile(instance, visibleId, notificationHub, healthMonitor))
            .ToList();

        var workingSetBytes = _workingSetBytesProvider?.Invoke()
            ?? Process.GetCurrentProcess().WorkingSet64;

        var webViewSets = _webViewWorkingSetsProvider?.Invoke() ?? SampleWebViewWorkingSets();
        var (webViewBytes, webViewCount) = AggregateWebViewMemory(webViewSets);

        return new ResourceSnapshot
        {
            ActiveAccountCount = instanceList.Count,
            TotalUnreadCount = SumInstanceUnreadCounts(instanceList, notificationHub),
            WorkingSetMegabytes = ConvertWorkingSetToMegabytes(workingSetBytes),
            WebView2WorkingSetMegabytes = ConvertWorkingSetToMegabytes(webViewBytes),
            WebView2ProcessCount = webViewCount,
            TotalWorkingSetMegabytes = ConvertWorkingSetToMegabytes(workingSetBytes + webViewBytes),
            VisibleInstanceName = ResolveVisibleDisplayName(instanceList, visibleId),
            InstanceTiles = tiles
        };
    }

    internal static long ConvertWorkingSetToMegabytes(long workingSetBytes) =>
        workingSetBytes / MegabyteDivisor;

    /// <summary>
    /// Pure aggregation of WebView2 child-process working sets (unit-testable). Non-positive samples are
    /// ignored (a process can exit between enumeration and the WorkingSet64 read). Returns total bytes + count.
    /// </summary>
    internal static (long TotalBytes, int Count) AggregateWebViewMemory(IEnumerable<long> processWorkingSets)
    {
        ArgumentNullException.ThrowIfNull(processWorkingSets);

        long total = 0;
        var count = 0;
        foreach (var workingSet in processWorkingSets)
        {
            if (workingSet <= 0)
            {
                continue;
            }

            total += workingSet;
            count++;
        }

        return (total, count);
    }

    /// <summary>
    /// Samples the working set of every live <c>msedgewebview2</c> process. This is the bulk of the app's
    /// real RAM at many-instance scale (each session spawns a browser + renderer + GPU/utility tree); the
    /// app's own process working set alone badly under-reports the footprint. Best-effort and never throws.
    /// </summary>
    private static IReadOnlyList<long> SampleWebViewWorkingSets()
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName("msedgewebview2");
        }
        catch
        {
            return [];
        }

        var sets = new List<long>(processes.Length);
        foreach (var process in processes)
        {
            try
            {
                sets.Add(process.WorkingSet64);
            }
            catch
            {
                // Process may have exited between enumeration and the read — skip it.
            }
            finally
            {
                process.Dispose();
            }
        }

        return sets;
    }

    internal static string ResolveVisibleDisplayName(
        IReadOnlyList<MessengerInstance> instances,
        string? visibleInstanceId)
    {
        if (string.IsNullOrWhiteSpace(visibleInstanceId))
        {
            return "None";
        }

        var normalizedId = visibleInstanceId.Trim();
        return instances
            .FirstOrDefault(instance => instance.Id.Equals(normalizedId, StringComparison.OrdinalIgnoreCase))
            ?.DisplayName ?? "None";
    }

    internal static int SumInstanceUnreadCounts(
        IReadOnlyList<MessengerInstance> instances,
        INotificationHubService notificationHub)
    {
        ArgumentNullException.ThrowIfNull(notificationHub);

        return instances.Sum(instance => notificationHub.GetBadgeCount(instance.Id));
    }

    internal static InstanceResourceTile BuildTile(
        MessengerInstance instance,
        string? visibleInstanceId,
        INotificationHubService notificationHub,
        AdapterHealthMonitor healthMonitor)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(notificationHub);
        ArgumentNullException.ThrowIfNull(healthMonitor);

        var instanceId = instance.Id.Trim();
        var isVisible = !string.IsNullOrWhiteSpace(visibleInstanceId)
            && instanceId.Equals(visibleInstanceId.Trim(), StringComparison.OrdinalIgnoreCase);

        return new InstanceResourceTile
        {
            InstanceId = instanceId,
            DisplayName = instance.DisplayName,
            Platform = instance.Platform,
            AccentColor = instance.AccentColor,
            IconGlyph = instance.IconGlyph,
            UnreadCount = notificationHub.GetBadgeCount(instanceId),
            HealthState = healthMonitor.GetStatus(instanceId).State,
            IsVisible = isVisible,
            MemoryTier = instance.MemoryTier.ToString()
        };
    }
}
