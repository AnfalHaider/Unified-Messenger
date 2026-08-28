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

    /// <summary>
    /// Why the most recent save failed, or null when the last one succeeded.
    /// </summary>
    /// <remarks>
    /// On the contract for the same reason the two properties above are, and with the same warning
    /// attached: a state nobody reads is a state the owner is never told about. The consumer is
    /// <c>MainWindow.OnSettingsSaveFailed</c>, wired at the same time as this was added.
    /// </remarks>
    string? LastSaveFailure { get; }

    event EventHandler? Changed;

    /// <summary>
    /// Raised once when a save starts failing, carrying the owner-readable reason.
    /// </summary>
    /// <remarks>
    /// Once, not per attempt: a jammed file with a NumberBox being dragged would otherwise raise this on
    /// every value change. It goes quiet again after a save succeeds.
    /// </remarks>
    event EventHandler<string>? SaveFailed;

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(Action<AppSettings> mutate, CancellationToken cancellationToken = default);
}
