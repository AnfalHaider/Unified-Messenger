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

    private bool _refuseSaves;

    private string? _preservedCorruptPath;

    /// <summary>Where an unreadable triage store was set aside on this launch, or null.</summary>
    public string? PreservedCorruptPath => _preservedCorruptPath;
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
            catch (Exception ex) when (CorruptFileRecovery.IsUnreadable(ex))
            {
                // Was `catch (JsonException)`, which is the narrower half of the problem CorruptFileRecovery
                // exists for. An IOException — the file locked by a backup tool or antivirus — escaped
                // LoadAsync entirely, and a JsonException reset the store to empty whether or not the bad
                // bytes had actually been preserved. Either way the owner's mark-handled and snooze
                // decisions vanished with no message and, in Release, no log line at all.
                _preservedCorruptPath = CorruptFileRecovery.Preserve(_storePath, "Triage.Store", ex);

                // Refuse to write over a file we could not read AND could not set aside — the same guard
                // InstanceRegistryService uses. Saving here would destroy the only copy of the data.
                _refuseSaves = _preservedCorruptPath is null;
                if (_refuseSaves)
                {
                    AppLogger.LogWarning(
                        "Triage.Store",
                        "Could not read or preserve the triage store; leaving the file alone and not saving over it this session.");
                }

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
                AppLogger.LogWarning("Triage.Store", $"Triage save failed: {ex.Message}");
            }
        }, token);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        // The file could not be read and could not be moved aside, so its bytes are the only copy of the
        // owner's triage decisions. Writing an empty store over them is exactly the silent data loss this
        // guard exists to stop.
        if (_refuseSaves)
        {
            return;
        }

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
            AppLogger.LogWarning("Triage.Store", $"Could not back up corrupt triage file: {ex.Message}");
        }
    }
}
