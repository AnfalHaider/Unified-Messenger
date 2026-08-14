using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public interface IAppSettingsService
{
    AppSettings Settings { get; }

    /// <summary>
    /// True when the last <see cref="LoadAsync"/> could not read the settings file and fell back to
    /// defaults. On the contract rather than only on the concrete class because the shell has to ask:
    /// these two properties existed from v4.99.4 with no consumer at all, which is exactly why the user
    /// was never told (F-DURA-01).
    /// </summary>
    bool RecoveredFromCorruptFile { get; }

    /// <summary>Where the unreadable settings file was preserved, when it could be preserved.</summary>
    string? CorruptFileBackupPath { get; }

    event EventHandler? Changed;

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(Action<AppSettings> mutate, CancellationToken cancellationToken = default);
}
