using System.Diagnostics;
using System.Text.Json;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Loads and debounce-saves triage items, thread registry, and kanban display order to <c>triage_v2.json</c>.
/// </summary>
public sealed class TriagePersistenceService
{
    private const string FileName = "triage_v2.json";
    private const int SaveDebounceMilliseconds = 750;

    private static readonly Lazy<TriagePersistenceService> LazyInstance =
        new(() => new TriagePersistenceService());

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _storePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _debounceLock = new();
    private CancellationTokenSource? _saveDebounceCts;
    private int _saveGeneration;
    private bool _isLoaded;
    private bool _isRestoring;
    private bool _subscriptionsAttached;

    internal bool SuppressPersistence { get; set; }

    public static TriagePersistenceService Instance => LazyInstance.Value;

    public TriagePersistenceService()
    {
        _storePath = Path.Combine(ApplicationPaths.UserDataRoot, FileName);
    }

    internal TriagePersistenceService(string storePath)
    {
        _storePath = storePath;
    }

    public void AttachChangeSubscriptions()
    {
        if (_subscriptionsAttached)
        {
            return;
        }

        _subscriptionsAttached = true;
        MessageTriageService.Instance.Changed += OnOperationalStateChanged;
        ThreadRegistryService.Instance.Changed += OnOperationalStateChanged;
        ThreadDisplayOrderService.Instance.Changed += OnOperationalStateChanged;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        AttachChangeSubscriptions();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
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

            TriageV2Store? store;
            try
            {
                await using var stream = File.OpenRead(_storePath);
                store = await JsonSerializer
                    .DeserializeAsync<TriageV2Store>(stream, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Triage store is corrupt; resetting to empty: {ex.Message}");
                BackupCorruptFile();
                _isLoaded = true;
                return;
            }

            if (store is null)
            {
                _isLoaded = true;
                return;
            }

            RestoreFromStore(store);
            _isLoaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        CancelScheduledSave();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            MessageTriageService.Instance.DrainPendingQueue();
            MessageTriageService.Instance.RestoreItems([]);
            ThreadRegistryService.Instance.RestoreThreads([]);
            ThreadDisplayOrderService.Instance.ResetForTests();

            if (File.Exists(_storePath))
            {
                File.Delete(_storePath);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        CancelScheduledSave();
        await SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    internal TriageV2Store BuildStoreSnapshot()
    {
        var threads = ThreadRegistryService.Instance.GetAllThreads().ToList();
        var threadIds = threads
            .Select(thread => thread.ThreadId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList();

        ThreadDisplayOrderService.Instance.PruneOrphans(threadIds);

        return new TriageV2Store
        {
            Version = TriageV2Store.CurrentVersion,
            SavedAtUtc = DateTimeOffset.UtcNow,
            Metadata = BuildMetadata(threads),
            TriageItems = MessageTriageService.Instance
                .GetAllItems()
                .Select(MessageTriageItemRecord.FromItem)
                .ToList(),
            Threads = threads,
            DisplayOrder = ThreadDisplayOrderService.Instance.Export()
        };
    }

    internal void RestoreFromStore(TriageV2Store store)
    {
        _isRestoring = true;
        try
        {
            var triageItems = (store.TriageItems ?? [])
                .Select(record => record.ToItem())
                .ToList();
            var threads = store.Threads ?? [];

            MessageTriageService.Instance.RestoreItems(triageItems);
            ThreadRegistryService.Instance.RestoreThreads(threads);
            ThreadDisplayOrderService.Instance.Load(store.DisplayOrder);

            var threadIds = threads
                .Select(thread => thread.ThreadId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();
            ThreadDisplayOrderService.Instance.PruneOrphans(threadIds);
            ThreadRegistryService.Instance.RefreshOperationalFlags();
            UnifiedMessengerDashboardService.Instance.NotifyChanged();
        }
        finally
        {
            _isRestoring = false;
        }
    }

    private static UnifiedMessengerStoreMetadata BuildMetadata(IReadOnlyList<ThreadData> threads)
    {
        var branches = threads
            .Where(thread => !string.IsNullOrWhiteSpace(thread.BranchName))
            .GroupBy(
                thread => $"{thread.InstanceId}|{thread.BranchName}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(thread => new UnifiedMessengerBranchRecord
            {
                BranchName = thread.BranchName,
                Platform = thread.Platform,
                InstanceId = thread.InstanceId,
                InstanceDisplayName = thread.InstanceDisplayName
            })
            .OrderBy(record => record.BranchName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new UnifiedMessengerStoreMetadata
        {
            SavedAtUtc = DateTimeOffset.UtcNow,
            Branches = branches
        };
    }

    private void OnOperationalStateChanged(object? sender, EventArgs e)
    {
        if (_isRestoring || SuppressPersistence)
        {
            return;
        }

        ScheduleSave();
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

    private void ScheduleSave()
    {
        CancellationToken token;
        int generation;

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

                await SaveAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // debounced or cleared
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Triage save failed: {ex.Message}");
            }
        }, token);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            TriageV2Store store;
            lock (_debounceLock)
            {
                store = BuildStoreSnapshot();
            }

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
            _gate.Release();
        }
    }

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
            Debug.WriteLine($"Could not back up corrupt triage file: {ex.Message}");
        }
    }
}
