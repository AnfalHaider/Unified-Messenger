using System.Text.Json;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed class AppSettingsService
{
    private const string FileName = "settings.json";

    private static readonly Lazy<AppSettingsService> LazyInstance = new(() => new AppSettingsService());

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _storePath;

    private AppSettingsService()
    {
        _storePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UnifiedMessenger",
            FileName);
    }

    public static AppSettingsService Instance => LazyInstance.Value;

    public AppSettings Settings { get; private set; } = new();

    public event EventHandler? Changed;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_storePath))
        {
            Settings = new AppSettings();
            await SaveAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await using var stream = File.OpenRead(_storePath);
        Settings = await JsonSerializer
            .DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false) ?? new AppSettings();
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);

        await using var stream = File.Create(_storePath);
        await JsonSerializer
            .SerializeAsync(stream, Settings, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UpdateAsync(
        Action<AppSettings> mutate,
        CancellationToken cancellationToken = default)
    {
        mutate(Settings);
        await SaveAsync(cancellationToken).ConfigureAwait(false);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
