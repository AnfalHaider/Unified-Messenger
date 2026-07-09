using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public interface IGitHubUpdateService
{
    Func<UpdateCheckResult, CancellationToken, Task<bool>>? PromptForUpdateApplicationAsync { get; set; }

    Task CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    Task<UpdateCheckResult> CheckForUpdatesManualAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads, verifies, and applies the update described by a prior check result, then exits so the
    /// installer can swap the binary. Throws if the result is not an applicable available update.
    /// </summary>
    Task ApplyUpdateAsync(UpdateCheckResult result, CancellationToken cancellationToken = default);
}
