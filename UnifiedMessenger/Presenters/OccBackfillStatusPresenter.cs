using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Backfill;

namespace UnifiedMessenger.Presenters;

public sealed class OccBackfillStatusPresentation
{
    public bool IsRunning { get; init; }

    public int RunningCount { get; init; }

    public string StatusText { get; init; } = string.Empty;

    public bool ShowStatus { get; init; }
}

public static class OccBackfillStatusPresenter
{
    public static OccBackfillStatusPresentation BuildStatus(IEnumerable<MessengerInstance> instances)
    {
        ArgumentNullException.ThrowIfNull(instances);

        var runningCount = instances.Count(instance =>
            BackfillSyncManager.Instance.GetState(instance.Id) == BackfillSyncState.Running);

        return new OccBackfillStatusPresentation
        {
            IsRunning = runningCount > 0,
            RunningCount = runningCount,
            StatusText = runningCount == 1
                ? "Backfill running for 1 account…"
                : $"Backfill running for {runningCount} accounts…",
            ShowStatus = runningCount > 0
        };
    }
}
