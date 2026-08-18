using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed record ImportInstancesResult(int ActiveCount, int ArchivedCount);

/// <summary>
/// How the account list came to be what it is. Only <see cref="Loaded"/> and <see cref="FirstRun"/> mean the
/// in-memory list is a faithful picture of the owner's accounts.
/// </summary>
public enum RegistryLoadOutcome
{
    /// <summary><see cref="InstanceRegistryService.LoadAsync"/> has not run yet.</summary>
    NotLoaded,

    /// <summary>The file was read and parsed.</summary>
    Loaded,

    /// <summary>The file genuinely does not exist. A seeded starter account is correct here.</summary>
    FirstRun,

    /// <summary>The file existed but could not be parsed; the bytes were preserved alongside it.</summary>
    RecoveredFromCorruptFile,

    /// <summary>
    /// The file could not be read at all — locked, denied, or on a folder that was not reachable.
    /// The owner's accounts still exist on disk; this session simply cannot see them.
    /// </summary>
    Failed
}

public sealed partial class InstanceRegistryService : IInstanceRegistryService
{
    private const string FileName = "instances.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _storePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private InstanceStore _store = new();
    private bool _isLoaded;

    public InstanceRegistryService()
    {
        _storePath = Path.Combine(ApplicationPaths.UserDataRoot, FileName);
    }

    internal InstanceRegistryService(string storePath)
    {
        _storePath = storePath;
        _store = new InstanceStore();
    }

    public IReadOnlyList<MessengerInstance> Instances => _store.Instances;

    public IReadOnlyList<MessengerInstance> ArchivedInstances => _store.ArchivedInstances;

    /// <summary>How the current in-memory list came to be. See <see cref="RegistryLoadOutcome"/>.</summary>
    public RegistryLoadOutcome LoadOutcome { get; private set; } = RegistryLoadOutcome.NotLoaded;

    /// <summary>Why the load failed, for the log and the on-screen notice. Null unless <see cref="LoadOutcome"/> is Failed.</summary>
    public string? LoadFailureDetail { get; private set; }

    /// <summary>Where the unparseable file was preserved, when <see cref="LoadOutcome"/> is RecoveredFromCorruptFile.</summary>
    public string? CorruptFileBackupPath { get; private set; }

    /// <summary>
    /// Attempts before giving up on a locked or denied file. A real-time virus scanner opening a
    /// just-written file, or the tail of a previous instance's shutdown, both clear in well under a second;
    /// what must not happen is one unlucky millisecond deciding what the owner sees.
    /// </summary>
    private const int ReadAttempts = 5;

    private static readonly TimeSpan ReadRetryDelay = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Reads the account list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What this used to do, and why it was dangerous.</b> The first line was
    /// <c>if (!File.Exists(_storePath))</c> → seed a starter account and save. But
    /// <see cref="File.Exists(string)"/> returns <c>false</c> for <i>any</i> failure, not only absence: a
    /// denied folder, a locked file, an unreadable path all look identical to a brand-new install. So a
    /// transient access problem made an owner with nine connected accounts open the app to a first-run
    /// welcome screen and a single demo account — and if the block had cleared a moment later, the next
    /// thing that saved would have written that one account over their nine.
    /// </para>
    /// <para>
    /// It now distinguishes the three cases by <i>opening</i> the file and reading the exception:
    /// not-found means first run, a parse error means corruption, and anything else means "cannot see the
    /// data right now" — which seeds nothing, saves nothing, and is reported to the owner.
    /// </para>
    /// </remarks>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isLoaded)
            {
                return;
            }

            var (outcome, loaded, failure) = await ReadStoreAsync(cancellationToken).ConfigureAwait(false);

            if (outcome == RegistryLoadOutcome.Failed)
            {
                // Deliberately no seeding and no save. The owner's accounts are on disk and intact; this
                // session simply could not reach them, and inventing a replacement list is how a display
                // problem turns into data loss.
                _store = new InstanceStore();
                LoadOutcome = RegistryLoadOutcome.Failed;
                LoadFailureDetail = failure;
                AppLogger.LogWarning(
                    "Registry",
                    $"Could not read the account list at '{_storePath}': {failure}. " +
                    "Running with no accounts for this session; nothing was written.");
                return;
            }

            _store = loaded!;
            var migrated = MigrateStoreIfNeeded();
            NormalizeStore(ensureUniqueIdentifiers: migrated || outcome != RegistryLoadOutcome.Loaded);

            if (migrated || _store.Instances.Count == 0)
            {
                if (_store.Instances.Count == 0)
                {
                    _store.Instances.Add(CreateDefaultWhatsAppInstance());
                    NormalizeStore(ensureUniqueIdentifiers: true);
                }

                await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
            }

            LoadOutcome = outcome;
            _isLoaded = true;

            // The one line that makes the next "where did my accounts go?" answerable in seconds: which
            // file this session actually read, and how many accounts came out of it.
            AppLogger.LogInfo(
                "Registry",
                $"Loaded {_store.Instances.Count} account(s) ({_store.ArchivedInstances.Count} archived) " +
                $"from '{_storePath}' — outcome {outcome}.");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Tries the read again after a failure — what the "Try again" button on the notice calls. Returns true
    /// when the accounts are now readable.
    /// </summary>
    /// <remarks>
    /// Worth offering rather than insisting on a restart: the causes of a failed read (a scanner holding
    /// the file, a folder that was briefly unreachable, a drive still coming up) are usually over within
    /// seconds, and a second attempt costs the owner nothing.
    /// </remarks>
    public async Task<bool> RetryLoadAsync(CancellationToken cancellationToken = default)
    {
        if (LoadOutcome != RegistryLoadOutcome.Failed)
        {
            return LoadOutcome != RegistryLoadOutcome.NotLoaded;
        }

        LoadOutcome = RegistryLoadOutcome.NotLoaded;
        LoadFailureDetail = null;
        await LoadAsync(cancellationToken).ConfigureAwait(false);

        return LoadOutcome != RegistryLoadOutcome.Failed;
    }

    /// <summary>
    /// One read attempt per retry, returning the outcome rather than throwing, so the caller can tell
    /// "there is nothing here yet" apart from "I could not look".
    /// </summary>
    private async Task<(RegistryLoadOutcome Outcome, InstanceStore? Store, string? Failure)> ReadStoreAsync(
        CancellationToken cancellationToken)
    {
        string? lastFailure = null;

        for (var attempt = 1; attempt <= ReadAttempts; attempt++)
        {
            try
            {
                InstanceStore? loaded;

                // Scoped tightly on purpose: the corrupt-file recovery below moves this very file, which
                // cannot succeed while this handle is open. The parse-error path gets that for free (the
                // throw unwinds the using first); the `null` path did not, and silently downgraded a
                // recoverable file to an unreadable one.
                await using (var stream = new FileStream(
                    _storePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    loaded = await JsonSerializer
                        .DeserializeAsync<InstanceStore>(stream, JsonOptions, cancellationToken)
                        .ConfigureAwait(false);
                }

                // A file containing the literal `null` parses to null. Treat it as corrupt rather than as
                // an empty account list, so the bytes are preserved before anything replaces them.
                return loaded is null
                    ? RecoverFromCorruptFile("the file contained no account list")
                    : (RegistryLoadOutcome.Loaded, loaded, null);
            }
            catch (FileNotFoundException)
            {
                return (RegistryLoadOutcome.FirstRun, CreateDefaultStore(), null);
            }
            catch (DirectoryNotFoundException)
            {
                return (RegistryLoadOutcome.FirstRun, CreateDefaultStore(), null);
            }
            catch (JsonException ex)
            {
                return RecoverFromCorruptFile(ex.Message);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Locked, denied, or a folder that momentarily was not there. Worth another look.
                lastFailure = $"{ex.GetType().Name}: {ex.Message}";
                if (attempt < ReadAttempts)
                {
                    await Task.Delay(ReadRetryDelay, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        return (RegistryLoadOutcome.Failed, null, lastFailure);
    }

    private (RegistryLoadOutcome Outcome, InstanceStore? Store, string? Failure) RecoverFromCorruptFile(string reason)
    {
        Debug.WriteLine($"Instances file is corrupt; resetting to defaults: {reason}");

        var backupPath = BackupCorruptFile();
        if (backupPath is null)
        {
            // The bytes could not be set aside, so replacing them would destroy the only copy of a file
            // that a person could still repair by hand. Refuse rather than overwrite.
            return (RegistryLoadOutcome.Failed, null,
                $"the file could not be parsed ({reason}) and could not be preserved either");
        }

        CorruptFileBackupPath = backupPath;
        AppLogger.LogWarning(
            "Registry",
            $"The account list could not be parsed ({reason}). The file was preserved as '{backupPath}'.");

        return (RegistryLoadOutcome.RecoveredFromCorruptFile, CreateDefaultStore(), null);
    }

    public async Task<MessengerInstance> AddInstanceAsync(
        string displayName,
        string platformId,
        string? customUrl,
        WorkspaceCategory category = WorkspaceCategory.Personal,
        CancellationToken cancellationToken = default)
    {
        var platform = PlatformDefinition.FindById(platformId)
            ?? throw new ArgumentException($"Unknown platform: {platformId}", nameof(platformId));

        var startUrl = ResolveStartUrl(platform, customUrl);
        var instanceId = Guid.NewGuid().ToString("N");
        var profileName = CreateProfileName(displayName, platform.Id);

        WebViewProfileManager.ValidateProfileName(profileName);

        var instance = new MessengerInstance
        {
            Id = instanceId,
            DisplayName = displayName.Trim(),
            ProfileName = profileName,
            StartUrl = startUrl,
            Platform = platform.Id,
            IconGlyph = platform.IconGlyph,
            AccentColor = platform.AccentColor,
            Category = category,
            SortOrder = NextSortOrder(category)
        };
        instance.Normalize();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _store.Instances.Add(instance);
            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        return instance;
    }

    public async Task<MessengerInstance> RestoreArchivedInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var archived = _store.ArchivedInstances.FirstOrDefault(
                i => i.Id.Equals(instanceId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("Archived instance not found.");

            _store.ArchivedInstances.Remove(archived);
            archived.SortOrder = NextSortOrder(archived.Category);
            _store.Instances.Add(archived);
            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
            return archived;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveFromSidebarAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var instance = FindByIdNoLock(instanceId)
                ?? throw new InvalidOperationException("Instance not found.");

            _store.Instances.Remove(instance);
            _store.ArchivedInstances.RemoveAll(i => i.Id.Equals(instanceId, StringComparison.OrdinalIgnoreCase));
            _store.ArchivedInstances.Add(instance);
            RenormalizeSortOrders(_store.Instances.Where(i => i.IsProfessional == instance.IsProfessional));
            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemovePermanentlyAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var instance = FindByIdNoLock(instanceId) ??
                           _store.ArchivedInstances.FirstOrDefault(
                               i => i.Id.Equals(instanceId, StringComparison.OrdinalIgnoreCase))
                           ?? throw new InvalidOperationException("Instance not found.");

            _store.Instances.RemoveAll(i => i.Id.Equals(instanceId, StringComparison.OrdinalIgnoreCase));
            _store.ArchivedInstances.RemoveAll(i => i.Id.Equals(instanceId, StringComparison.OrdinalIgnoreCase));
            RenormalizeSortOrders(_store.Instances.Where(i => i.IsProfessional == instance.IsProfessional));
            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
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

    public MessengerInstance? FindById(string instanceId)
    {
        _gate.Wait();
        try
        {
            return FindByIdNoLock(instanceId);
        }
        finally
        {
            _gate.Release();
        }
    }

    // Lock-free lookup for callers that ALREADY hold _gate. The async mutators (remove/move/rename/…) take
    // the gate and then need to find the instance; calling the public, gate-taking FindById from inside the
    // gate re-enters the non-reentrant SemaphoreSlim and DEADLOCKS the UI thread (this was the "Remove
    // instance gets stuck" / reorder-hang bug). In-gate callers must use this instead.
    private MessengerInstance? FindByIdNoLock(string instanceId) =>
        _store.Instances.FirstOrDefault(i => i.Id.Equals(instanceId, StringComparison.OrdinalIgnoreCase));

    public async Task UpdateInstanceCategoryAsync(
        string instanceId,
        WorkspaceCategory category,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var instance = FindByIdNoLock(instanceId)
                ?? throw new InvalidOperationException("Instance not found.");

            if (instance.Category == category)
            {
                return;
            }

            var previousCategory = instance.Category;
            instance.Category = category;
            instance.SortOrder = NextSortOrder(category);
            RenormalizeSortOrders(_store.Instances.Where(i => i.IsProfessional == (previousCategory == WorkspaceCategory.Professional)));
            RenormalizeSortOrders(_store.Instances.Where(i => i.IsProfessional == instance.IsProfessional));
            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateInstanceDisplayNameAsync(
        string instanceId,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var trimmed = displayName.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var instance = FindByIdNoLock(instanceId)
                ?? throw new InvalidOperationException("Instance not found.");

            if (instance.DisplayName.Equals(trimmed, StringComparison.Ordinal))
            {
                return;
            }

            instance.DisplayName = trimmed;
            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Sets (or clears) the user-chosen built-in avatar icon and its flat color. Pass null glyph to clear it
    /// (fall back to an imported/uploaded image if cached, else initials). The icon color is independent of
    /// the platform accent (which platform branding owns), so it persists across reloads.
    /// </summary>
    public async Task UpdateInstanceAvatarIconAsync(
        string instanceId,
        string? iconGlyph,
        string? iconColor,
        string? iconFontFamily = null,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var instance = FindByIdNoLock(instanceId)
                ?? throw new InvalidOperationException("Instance not found.");

            instance.CustomIconGlyph = string.IsNullOrWhiteSpace(iconGlyph) ? null : iconGlyph;
            instance.CustomIconColor = string.IsNullOrWhiteSpace(iconColor) ? null : iconColor;
            instance.CustomIconFontFamily = string.IsNullOrWhiteSpace(iconFontFamily) ? null : iconFontFamily;
            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateInstanceBranchKeyAsync(
        string instanceId,
        string? branchKey,
        CancellationToken cancellationToken = default)
    {
        var normalized = string.IsNullOrWhiteSpace(branchKey) ? null : branchKey.Trim();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var instance = FindByIdNoLock(instanceId)
                ?? throw new InvalidOperationException("Instance not found.");

            if (string.Equals(instance.BranchKey, normalized, StringComparison.Ordinal))
            {
                return;
            }

            instance.BranchKey = normalized;
            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateInstanceNotificationsMutedAsync(
        string instanceId,
        bool muted,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var instance = FindByIdNoLock(instanceId)
                ?? throw new InvalidOperationException("Instance not found.");

            instance.NotificationsMuted = muted;
            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MoveInstanceAsync(
        string instanceId,
        int direction,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var instance = FindByIdNoLock(instanceId)
                ?? throw new InvalidOperationException("Instance not found.");

            var peers = _store.Instances
                .Where(i => i.IsProfessional == instance.IsProfessional)
                .OrderBy(i => i.SortOrder)
                .ThenBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var index = peers.FindIndex(i => i.Id.Equals(instanceId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return;
            }

            var targetIndex = index + direction;
            if (targetIndex < 0 || targetIndex >= peers.Count)
            {
                return;
            }

            var other = peers[targetIndex];
            (instance.SortOrder, other.SortOrder) = (other.SortOrder, instance.SortOrder);
            if (instance.SortOrder == other.SortOrder)
            {
                instance.SortOrder = index + direction + 1;
                other.SortOrder = index + 1;
            }

            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateInstanceMemoryTierAsync(
        string instanceId,
        MemoryTierPreference tier,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var instance = FindByIdNoLock(instanceId)
                ?? throw new InvalidOperationException("Instance not found.");

            instance.MemoryTier = tier;
            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateInstanceMetadataAsync(
        string instanceId,
        string displayName,
        string startUrl,
        string platformId,
        string? notes,
        string? branchKey = null,
        CancellationToken cancellationToken = default)
    {
        var platform = PlatformDefinition.FindById(platformId)
            ?? throw new ArgumentException($"Unknown platform: {platformId}", nameof(platformId));

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var instance = FindByIdNoLock(instanceId)
                ?? throw new InvalidOperationException("Instance not found.");

            instance.DisplayName = displayName.Trim();
            instance.StartUrl = ResolveStartUrl(platform, startUrl);
            instance.Platform = platform.Id;
            instance.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            instance.BranchKey = string.IsNullOrWhiteSpace(branchKey) ? null : branchKey.Trim();
            instance.Normalize();
            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public IEnumerable<MessengerInstance> GetOrderedInstances() =>
        _store.Instances
            .OrderBy(i => i.IsProfessional ? 0 : 1)
            .ThenBy(i => i.SortOrder)
            .ThenBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase);

    public string StorePath => _storePath;

    public async Task ExportInstancesAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        await SaveAsync(cancellationToken).ConfigureAwait(false);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Copy(_storePath, destinationPath, overwrite: true);
    }

    public async Task<ImportInstancesResult> ImportInstancesAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Import file not found.", sourcePath);
        }

        InstanceStore imported;
        try
        {
            await using var stream = File.OpenRead(sourcePath);
            imported = await JsonSerializer
                .DeserializeAsync<InstanceStore>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidDataException("Import file is empty or invalid.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Import file is not valid JSON.", ex);
        }

        if (imported.Instances.Count == 0 && imported.ArchivedInstances.Count == 0)
        {
            throw new InvalidDataException("Import file contains no instances.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(_storePath))
            {
                File.Copy(_storePath, _storePath + ".bak", overwrite: true);
            }

            _store = imported;
            MigrateStoreIfNeeded();
            NormalizeStore(ensureUniqueIdentifiers: true);
            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);

            return new ImportInstancesResult(
                _store.Instances.Count,
                _store.ArchivedInstances.Count);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// The message shown if anything tries to save while the account list is unreadable.
    /// </summary>
    internal const string RefusedSaveMessage =
        "Your accounts could not be read when the app started, so they cannot be changed right now. " +
        "Nothing has been lost — close Unified Messenger and open it again.";

    private async Task SaveCoreAsync(CancellationToken cancellationToken)
    {
        // The single most important line in this file. After a failed load the in-memory list is empty or
        // a starter account; writing it out would replace the owner's real accounts with that. Every
        // mutator funnels through here, so one guard covers add, remove, rename, reorder, recategorise and
        // the rest — there is no path that can quietly overwrite a file this session never managed to read.
        if (LoadOutcome == RegistryLoadOutcome.Failed)
        {
            AppLogger.LogWarning(
                "Registry",
                $"Refused to write '{_storePath}': the account list was never successfully read this session.");
            throw new InvalidOperationException(RefusedSaveMessage);
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
                .SerializeAsync(stream, _store, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, _storePath, overwrite: true);
    }

    /// <summary>
    /// Sets the unparseable file aside and returns where it went, or null if it could not be preserved.
    /// </summary>
    /// <remarks>
    /// The return value is the caller's permission to overwrite. Previously this was void and failure was
    /// swallowed, so a file that could not be backed up was replaced anyway — destroying the only copy of
    /// something a person could still have repaired in a text editor.
    /// </remarks>
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
            Debug.WriteLine($"Could not back up corrupt instances file: {ex.Message}");
            return null;
        }
    }

    private static InstanceStore CreateDefaultStore()
    {
        return new InstanceStore
        {
            Instances = [CreateDefaultWhatsAppInstance()]
        };
    }

    private static MessengerInstance CreateDefaultWhatsAppInstance()
    {
        var platform = PlatformDefinition.FindById("whatsapp")!;

        return new MessengerInstance
        {
            Id = "whatsapp-default",
            DisplayName = "WhatsApp",
            ProfileName = "whatsapp-default",
            StartUrl = platform.DefaultUrl,
            Platform = platform.Id,
            IconGlyph = platform.IconGlyph,
            AccentColor = platform.AccentColor,
            Category = WorkspaceCategory.Personal,
            SortOrder = 1
        };
    }

    private bool MigrateStoreIfNeeded()
    {
        var migrated = false;

        if (_store.Version < 2)
        {
            foreach (var instance in AllInstances())
            {
                if (!Enum.IsDefined(instance.Category))
                {
                    instance.Category = WorkspaceCategory.Personal;
                }
            }

            _store.Version = 2;
            migrated = true;
        }

        if (_store.Version < 3)
        {
            var order = 0;
            foreach (var instance in _store.Instances)
            {
                if (instance.SortOrder == 0)
                {
                    instance.SortOrder = ++order;
                }
            }

            _store.Version = 3;
            migrated = true;
        }

        if (_store.Version < 5)
        {
            foreach (var instance in AllInstances())
            {
                instance.Normalize();
            }

            _store.Version = 5;
            migrated = true;
        }

        if (_store.Version < InstanceStore.CurrentVersion)
        {
            RenormalizeSortOrders(_store.Instances.Where(i => i.IsProfessional));
            RenormalizeSortOrders(_store.Instances.Where(i => !i.IsProfessional));
            _store.Version = InstanceStore.CurrentVersion;
            migrated = true;
        }

        return migrated;
    }

    private void NormalizeStore(bool ensureUniqueIdentifiers)
    {
        foreach (var instance in AllInstances())
        {
            instance.Normalize();
        }

        ValidateInstanceStartUrls();

        RenormalizeSortOrders(_store.Instances.Where(i => i.IsProfessional));
        RenormalizeSortOrders(_store.Instances.Where(i => !i.IsProfessional));

        if (!ensureUniqueIdentifiers)
        {
            return;
        }

        EnsureUniqueInstanceIds();
        EnsureValidProfileNames();
    }

    private IEnumerable<MessengerInstance> AllInstances() =>
        _store.Instances.Concat(_store.ArchivedInstances);

    private void EnsureUniqueInstanceIds()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var instance in AllInstances())
        {
            if (string.IsNullOrWhiteSpace(instance.Id) || !seen.Add(instance.Id))
            {
                instance.Id = Guid.NewGuid().ToString("N");
                seen.Add(instance.Id);
            }
        }
    }

    private void EnsureValidProfileNames()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var instance in AllInstances())
        {
            if (string.IsNullOrWhiteSpace(instance.ProfileName) ||
                !TryValidateProfileName(instance.ProfileName))
            {
                instance.ProfileName = CreateProfileName(instance.DisplayName, instance.Platform);
            }

            var baseName = instance.ProfileName;
            var suffix = 2;
            while (!seen.Add(instance.ProfileName))
            {
                instance.ProfileName = CreateProfileName($"{baseName}-{suffix}", instance.Platform);
                suffix++;
            }
        }
    }

    private static bool TryValidateProfileName(string profileName)
    {
        try
        {
            WebViewProfileManager.ValidateProfileName(profileName);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private int NextSortOrder(WorkspaceCategory category)
    {
        var isProfessional = category == WorkspaceCategory.Professional;
        var maxOrder = _store.Instances
            .Where(i => i.IsProfessional == isProfessional)
            .Select(i => i.SortOrder)
            .DefaultIfEmpty(0)
            .Max();

        return maxOrder + 1;
    }

    private static void RenormalizeSortOrders(
        IEnumerable<MessengerInstance> instances,
        bool preserveListOrder = false)
    {
        List<MessengerInstance> ordered = preserveListOrder
            ? instances.ToList()
            : instances
                .OrderBy(i => i.SortOrder)
                .ThenBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].SortOrder = i + 1;
        }
    }

    private void ValidateInstanceStartUrls()
    {
        foreach (var instance in AllInstances())
        {
            if (string.IsNullOrWhiteSpace(instance.Platform))
            {
                throw new InvalidDataException(
                    $"Instance '{instance.DisplayName}' is missing a platform identifier.");
            }

            var platform = PlatformDefinition.FindById(instance.Platform);
            if (platform is null)
            {
                throw new InvalidDataException(
                    $"Instance '{instance.DisplayName}' uses unknown platform '{instance.Platform}'.");
            }

            MigrateLegacyStartUrl(instance);
            instance.StartUrl = ResolveStartUrl(platform, instance.StartUrl);
        }
    }

    // Move Google Business accounts off superseded landing pages onto the current default (the /reviews
    // manager view). Covers the bare business.google.com root (redirects single-location managers into a raw
    // Search results page) and the short-lived /locations default. Only rewrites those exact legacy values,
    // never a user's own override.
    private static readonly HashSet<string> LegacyGoogleBusinessStartUrls = new(StringComparer.OrdinalIgnoreCase)
    {
        "https://business.google.com",
        "https://business.google.com/locations",
    };

    private static void MigrateLegacyStartUrl(MessengerInstance instance)
    {
        if (!PlatformDefinition.NormalizePlatformId(instance.Platform)
                .Equals("googlebusiness", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var current = instance.StartUrl?.Trim().TrimEnd('/');
        if (current is not null && LegacyGoogleBusinessStartUrls.Contains(current))
        {
            instance.StartUrl = "https://business.google.com/reviews";
        }
    }

    private static string ResolveStartUrl(PlatformDefinition platform, string? customUrl)
    {
        if (!string.IsNullOrWhiteSpace(customUrl))
        {
            var trimmed = customUrl.Trim();
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                throw new ArgumentException("Custom URL must be a valid http or https address.", nameof(customUrl));
            }

            // For platforms with a fixed default URL, the custom URL must share the same host to prevent
            // a crafted import from redirecting a known-platform instance to an arbitrary site.
            if (!string.IsNullOrWhiteSpace(platform.DefaultUrl) &&
                Uri.TryCreate(platform.DefaultUrl, UriKind.Absolute, out var defaultUri))
            {
                var expectedHost = defaultUri.Host;
                if (!uri.Host.Equals(expectedHost, StringComparison.OrdinalIgnoreCase) &&
                    !uri.Host.EndsWith("." + expectedHost, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(
                        $"Start URL host must match the expected platform host ({expectedHost}).",
                        nameof(customUrl));
                }
            }

            return trimmed;
        }

        if (string.IsNullOrWhiteSpace(platform.DefaultUrl))
        {
            throw new ArgumentException("A custom URL is required for this platform.", nameof(customUrl));
        }

        return platform.DefaultUrl;
    }

    public static string CreateProfileName(string displayName, string platformId)
    {
        var slug = ProfileSlugPattern().Replace(displayName.ToLowerInvariant(), "-").Trim('-');
        if (string.IsNullOrEmpty(slug))
        {
            slug = platformId;
        }

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var profileName = $"{platformId}-{slug}-{suffix}";

        if (profileName.Length > 64)
        {
            profileName = profileName[..64].TrimEnd('.', ' ');
        }

        return profileName;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex ProfileSlugPattern();
}
