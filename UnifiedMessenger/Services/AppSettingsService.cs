using System.Diagnostics;
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
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                // Must be AppLogger, not Debug.WriteLine. Debug.WriteLine is compiled out of the Release
                // build that ships, which is why a real corrupt-settings reset left NO trace in app.log —
                // verified by corrupting settings.json against the shipping binary and finding nothing
                // logged. Losing the user's configuration silently is not acceptable; at minimum it must
                // be on the record.
                AppLogger.LogError("Settings.Load.Corrupt", ex);
                RecoveredFromCorruptFile = true;
                CorruptFileBackupPath = BackupCorruptFile();
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

    private async Task SaveCoreAsync(CancellationToken cancellationToken)
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
    }

    private static AppSettings CreateDefaultSettings() =>
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

    /// <summary>Moves the unreadable settings file aside. Returns the backup path, or null on failure.</summary>
    private string? BackupCorruptFile()
    {
        try
        {
            if (!File.Exists(_storePath))
            {
                return null;
            }

            var backupPath = $"{_storePath}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
            File.Move(_storePath, backupPath, overwrite: true);
            return backupPath;
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Settings.BackupCorruptFile", ex);
            return null;
        }
    }
}
