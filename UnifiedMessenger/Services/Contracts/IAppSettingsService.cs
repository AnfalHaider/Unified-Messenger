using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public interface IAppSettingsService
{
    AppSettings Settings { get; }

    event EventHandler? Changed;

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(Action<AppSettings> mutate, CancellationToken cancellationToken = default);
}
