using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed class RichTriageStoreService
{
    public const string FileName = "triage_v2.json";

    private const int SaveDebounceMilliseconds = 750;
    private const int MaxStoredItems = 200;
    private static readonly TimeSpan RetentionWindow = TimeSpan.FromHours(48);

    private static readonly Lazy<RichTriageStoreService> LazyInstance =
        new(() => new RichTriageStoreService());

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _storePath;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly object _debounceLock = new();
    private CancellationTokenSource? _saveDebounceCts;
    private int _saveGeneration;
    private bool _isLoaded;
    private bool _suppressPersist;

    private RichTriageStoreService()
    {
        _storePath = Path.Combine(ApplicationPaths.UserDataRoot, FileName);
        MessageTriageService.Instance.Changed += OnTriageChanged;
        ThreadRegistryService.Instance.Changed += OnThreadsChanged;
    }

    internal RichTriageStoreService(string storePath, bool subscribeToTriageChanges = false)
    {
        _storePath = storePath;
        if (subscribeToTriageChanges)
        {
            MessageTriageService.Instance.Changed += OnTriageChanged;
            ThreadRegistryService.Instance.Changed += OnThreadsChanged;
        }
    }

    public static RichTriageStoreService Instance => LazyInstance.Value;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isLoaded)
            {
                return;
            }

            if (!File.Exists(_storePath))
            {
                _isLoaded = true;
                return;
            }

            RichTriageStoreFile? store;
            try
            {
                await using var stream = File.OpenRead(_storePath);
                store = await JsonSerializer
                    .DeserializeAsync<RichTriageStoreFile>(stream, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Triage store is corrupt; resetting to empty: {ex.Message}");
                BackupCorruptFile();
                _isLoaded = true;
                return;
            }

            var migrated = RichTriageStoreMigrator.Migrate(store);
            var items = PruneItems(migrated.Items);
            var threads = PruneThreads(migrated.Threads);
            _suppressPersist = true;
            try
            {
                MessageTriageService.Instance.RestoreItems(items);
                ThreadRegistryService.Instance.RestoreThreads(threads);
            }
            finally
            {
                _suppressPersist = false;
            }

            _isLoaded = true;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        CancelScheduledSave();
        await SaveAsync(
            MessageTriageService.Instance.GetAllItems(),
            ThreadRegistryService.Instance.GetAllThreads(),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        CancelScheduledSave();

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            MessageTriageService.Instance.RestoreItems([]);
            ThreadRegistryService.Instance.RestoreThreads([]);
            if (File.Exists(_storePath))
            {
                File.Delete(_storePath);
            }
        }
        finally
        {
            _writeGate.Release();
        }
    }

    internal void SetLoadedForTests() => _isLoaded = true;

    internal Task SaveSnapshotForTestsAsync(
        IEnumerable<MessageTriageItem> items,
        IEnumerable<ThreadData>? threads = null,
        CancellationToken cancellationToken = default) =>
        SaveAsync(PruneItems(items), threads?.ToList() ?? [], cancellationToken);

    internal static async Task<RichTriageStoreFile?> ReadStoreForTestsAsync(
        string storePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(storePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(storePath);
        var store = await JsonSerializer
            .DeserializeAsync<RichTriageStoreFile>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return store is null ? null : RichTriageStoreMigrator.Migrate(store);
    }

    internal static async Task<IReadOnlyList<MessageTriageItem>> ReadFileForTestsAsync(
        string storePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(storePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(storePath);
        var store = await JsonSerializer
            .DeserializeAsync<RichTriageStoreFile>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return store?.Items ?? [];
    }

    internal static List<MessageTriageItem> PruneItems(IEnumerable<MessageTriageItem> items)
    {
        var cutoff = DateTimeOffset.UtcNow - RetentionWindow;
        return items
            .Where(item => item.TimestampUtc >= cutoff)
            .OrderByDescending(item => item.TimestampUtc)
            .Take(MaxStoredItems)
            .ToList();
    }

    private void OnTriageChanged(object? sender, EventArgs e)
    {
        if (_suppressPersist)
        {
            return;
        }

        SchedulePersist(
            MessageTriageService.Instance.GetAllItems(),
            ThreadRegistryService.Instance.GetAllThreads());
    }

    private void OnThreadsChanged(object? sender, EventArgs e)
    {
        if (_suppressPersist)
        {
            return;
        }

        SchedulePersist(
            MessageTriageService.Instance.GetAllItems(),
            ThreadRegistryService.Instance.GetAllThreads());
    }

    private void SchedulePersist(
        IReadOnlyList<MessageTriageItem> items,
        IReadOnlyList<ThreadData> threads)
    {
        if (!_isLoaded)
        {
            return;
        }

        CancellationToken token;
        int generation;
        IReadOnlyList<MessageTriageItem> snapshot = items;
        IReadOnlyList<ThreadData> threadSnapshot = threads;

        lock (_debounceLock)
        {
            _saveDebounceCts?.Cancel();
            _saveDebounceCts?.Dispose();
            _saveDebounceCts = new CancellationTokenSource();
            token = _saveDebounceCts.Token;
            generation = Interlocked.Increment(ref _saveGeneration);
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(SaveDebounceMilliseconds, token).ConfigureAwait(false);
                if (generation != Volatile.Read(ref _saveGeneration))
                {
                    return;
                }

                await SaveAsync(snapshot, threadSnapshot, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // debounced
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Triage store save failed: {ex.Message}");
            }
        }, token);
    }

    private void CancelScheduledSave()
    {
        lock (_debounceLock)
        {
            Interlocked.Increment(ref _saveGeneration);
            _saveDebounceCts?.Cancel();
            _saveDebounceCts?.Dispose();
            _saveDebounceCts = null;
        }
    }

    private async Task SaveAsync(
        IReadOnlyList<MessageTriageItem> items,
        IReadOnlyList<ThreadData> threads,
        CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var store = RichTriageStoreMigrator.Migrate(new RichTriageStoreFile
            {
                Version = RichTriageStoreFile.CurrentVersion,
                Items = PruneItems(items),
                Threads = PruneThreads(threads)
            });

            Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);

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
                    .SerializeAsync(stream, store, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, _storePath, overwrite: true);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    internal static List<ThreadData> PruneThreads(IEnumerable<ThreadData> threads)
    {
        var cutoff = DateTimeOffset.UtcNow - RetentionWindow;
        return threads
            .Where(thread => thread.LastMessageTime >= cutoff)
            .OrderByDescending(thread => thread.LastMessageTime)
            .Take(MaxStoredItems)
            .ToList();
    }

    private void BackupCorruptFile()
    {
        try
        {
            var backupPath = $"{_storePath}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
            File.Move(_storePath, backupPath, overwrite: false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to backup corrupt triage store: {ex.Message}");
        }
    }
}
