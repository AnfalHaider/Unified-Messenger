namespace UnifiedMessenger.Models;

public enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    Failed
}

public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    Version? CurrentVersion = null,
    Version? LatestVersion = null,
    string? ErrorMessage = null,
    string? DownloadUrl = null);
