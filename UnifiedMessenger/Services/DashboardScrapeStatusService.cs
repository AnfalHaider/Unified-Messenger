using System.Collections.Concurrent;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed class DashboardScrapeStatusEntry
{
    public bool Success { get; init; }

    public string Context { get; init; } = string.Empty;

    public string? Detail { get; init; }

    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class DashboardScrapeStatusService
{
    private static readonly Lazy<DashboardScrapeStatusService> LazyInstance =
        new(() => new DashboardScrapeStatusService());

    private readonly ConcurrentDictionary<string, DashboardScrapeStatusEntry> _entries =
        new(StringComparer.OrdinalIgnoreCase);

    public static DashboardScrapeStatusService Instance => LazyInstance.Value;

    public void Record(string instanceId, bool success, string? context, string? detail)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        _entries[instanceId.Trim()] = new DashboardScrapeStatusEntry
        {
            Success = success,
            Context = string.IsNullOrWhiteSpace(context) ? "dashboard-scrape" : context.Trim(),
            Detail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim(),
            RecordedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public bool TryGetEntry(string instanceId, out DashboardScrapeStatusEntry? entry)
    {
        entry = null;
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        if (_entries.TryGetValue(instanceId.Trim(), out var found))
        {
            entry = found;
            return true;
        }

        return false;
    }

    public string BuildGoogleTrustScrapeFooter(IEnumerable<string> googleInstanceIds)
    {
        var ids = googleInstanceIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        if (ids.Count == 0)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        foreach (var id in ids)
        {
            if (!TryGetEntry(id, out var entry))
            {
                parts.Add("scrape pending");
                continue;
            }

            var status = entry!.Success ? "OK" : "failed";
            var age = RelativeTimeFormatter.Format(entry.RecordedAtUtc);
            parts.Add($"{status} · {age}");
        }

        if (parts.Count == 1)
        {
            return $"Last dashboard scrape: {parts[0]}";
        }

        return $"Last scrape ({parts.Count} locations): {string.Join("; ", parts.Take(3))}";
    }

    internal void ClearForTests() => _entries.Clear();
}
