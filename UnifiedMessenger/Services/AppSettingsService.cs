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
            catch (JsonException ex)
            {
                Debug.WriteLine($"Settings file is corrupt; resetting to defaults: {ex.Message}");
                BackupCorruptFile();
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

    private void BackupCorruptFile()
    {
        try
        {
            if (!File.Exists(_storePath))
            {
                return;
            }

            var backupPath = $"{_storePath}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
            File.Move(_storePath, backupPath, overwrite: true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not back up corrupt settings file: {ex.Message}");
        }
    }
}
