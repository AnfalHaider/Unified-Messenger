using System.Text.Json;
using System.Text.Json.Serialization;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed class AppSettingsService : IAppSettingsService
{
    private const string FileName = "settings.json";

    private static readonly Lazy<AppSettingsService> LazyInstance = new(() => new AppSettingsService());

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _storePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _isLoaded;

    private AppSettingsService()
    {
        _storePath = Path.Combine(ApplicationPaths.UserDataRoot, FileName);
    }

    internal AppSettingsService(string storePath)
    {
        _storePath = storePath;
    }

    public static AppSettingsService Instance => LazyInstance.Value;

    public AppSettings Settings { get; private set; } = new();

    public event EventHandler? Changed;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isLoaded)
            {
                return;
            }

            if (!File.Exists(_storePath))
            {
                Settings = CreateDefaultSettings();
                Settings.Normalize();
                PersonalOverviewLayoutService.Normalize(Settings);
                await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
                _isLoaded = true;
                return;
            }

            AppSettings loaded;
            try
            {
                await using var stream = File.OpenRead(_storePath);
                loaded = await JsonSerializer
                    .DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)
                    .ConfigureAwait(false) ?? new AppSettings();
            }
            // JsonException alone was too narrow: a settings file locked by a backup tool or antivirus, or
            // sitting on an unavailable network profile, throws IOException/UnauthorizedAccessException and
            // used to escape LoadAsync entirely — failing startup rather than degrading to defaults.
            // Recording and preserving are shared with the other durable stores so all three behave alike.
            catch (Exception ex) when (CorruptFileRecovery.IsUnreadable(ex))
            {
                RecoveredFromCorruptFile = true;
                CorruptFileBackupPath = CorruptFileRecovery.Preserve(_storePath, "Settings", ex);
                loaded = new AppSettings();
            }

            loaded.Normalize();
            PersonalOverviewLayoutService.Normalize(loaded);
            var needsLitePurge = loaded.Version < AppSettings.CurrentVersion;
            Settings = loaded;

            if (needsLitePurge)
            {
                Settings.Version = AppSettings.CurrentVersion;
                await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
            }

            _isLoaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateAsync(
        Action<AppSettings> mutate,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            mutate(Settings);
            Settings.Normalize();
            PersonalOverviewLayoutService.Normalize(Settings);
            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
            Changed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Raised the first time a save fails after a run of successful ones. Carries the owner-readable
    /// reason. Not raised again until a save succeeds, so a jammed file cannot produce a dialog per
    /// keystroke in a NumberBox.
    /// </summary>
    public event EventHandler<string>? SaveFailed;

    /// <summary>Why the most recent save failed, or null when the last one succeeded.</summary>
    public string? LastSaveFailure { get; private set; }

    /// <summary>
    /// Writes the settings file, reporting rather than throwing when it cannot be written.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This used to let an <see cref="IOException"/> out, and roughly thirty Settings handlers are
    /// <c>async void</c> event handlers that call it with no <c>try</c> of their own — a shape the file
    /// itself is inconsistent about, since the toggle immediately below <c>RunInBackgroundOnClose</c> does
    /// catch. An exception from an <c>async void</c> handler reaches <c>App.OnUnhandledException</c>, which
    /// deliberately leaves <c>Handled=false</c>, so <b>flipping a Settings toggle while the settings file
    /// was locked closed the app</b>. Antivirus, a backup tool mid-scan, a full disk, a roaming profile on
    /// an unreachable share: all ordinary, none the owner's fault, and the app simply vanished.
    /// </para>
    /// <para>
    /// Fixing it in the thirty-odd call sites would have left the thirty-first to be written without the
    /// guard. Fixing it here also covers the fire-and-forget callers (<c>_ = UpdateAsync(…)</c> in
    /// <c>MainWindow</c> and <c>ShellNavigationCoordinator</c>) whose failures were not fatal but were
    /// entirely silent — the preference just did not persist, and nothing anywhere said so.
    /// </para>
    /// <para>
    /// Absorbing the failure must not mean hiding it: the reason is logged, kept on
    /// <see cref="LastSaveFailure"/>, and raised once through <see cref="SaveFailed"/> for the shell to put
    /// in front of the owner. Programmer errors are deliberately not caught — only the file-unwritable set.
    /// </para>
    /// </remarks>
    private async Task SaveCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
            Settings.Normalize();

            var tempPath = _storePath + ".tmp";

            await using (var stream = new FileStream(
                             tempPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 4096,
                             options: FileOptions.Asynchronous))
            {
                await JsonSerializer
                    .SerializeAsync(stream, Settings, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, _storePath, overwrite: true);
            LastSaveFailure = null;
        }
        catch (Exception ex) when (CorruptFileRecovery.IsUnreadable(ex))
        {
            var wasFailing = LastSaveFailure is not null;
            LastSaveFailure = UserFacingError.Describe("Settings.Save", ex);

            if (!wasFailing)
            {
                SaveFailed?.Invoke(this, LastSaveFailure);
            }
        }
    }

    internal static AppSettings CreateDefaultSettings() =>
        new()
        {
            MaxConcurrentWebViews = 6,
            StartupWarmMode = StartupWarmMode.VisibleOnly
        };

    /// <summary>
    /// True when the last <see cref="LoadAsync"/> could not read the settings file and fell back to
    /// defaults. The user's saved preferences are gone from the live session; tell them.
    /// </summary>
    public bool RecoveredFromCorruptFile { get; private set; }

    /// <summary>Where the unreadable settings file was preserved, when it could be preserved.</summary>
    public string? CorruptFileBackupPath { get; private set; }
}
