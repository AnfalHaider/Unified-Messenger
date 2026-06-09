using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public interface IGitHubUpdateService
{
    Func<UpdateCheckResult, CancellationToken, Task<bool>>? PromptForUpdateApplicationAsync { get; set; }

    Task CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    Task<UpdateCheckResult> CheckForUpdatesManualAsync(CancellationToken cancellationToken = default);
}
