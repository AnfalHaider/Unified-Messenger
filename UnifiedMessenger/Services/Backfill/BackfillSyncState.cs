namespace UnifiedMessenger.Services.Backfill;

public enum BackfillSyncState
{
    NotStarted,
    Running,
    Completed,
    Failed,
    Skipped
}
