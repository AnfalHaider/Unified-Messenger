using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed record ImportInstancesResult(int ActiveCount, int ArchivedCount);

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
                _store = CreateDefaultStore();
                NormalizeStore(ensureUniqueIdentifiers: true);
                await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
                _isLoaded = true;
                return;
            }

            InstanceStore loaded;
            try
            {
                await using var stream = File.OpenRead(_storePath);
                loaded = await JsonSerializer
                    .DeserializeAsync<InstanceStore>(stream, JsonOptions, cancellationToken)
                    .ConfigureAwait(false) ?? CreateDefaultStore();
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Instances file is corrupt; resetting to defaults: {ex.Message}");
                BackupCorruptFile();
                loaded = CreateDefaultStore();
            }

            _store = loaded;
            var migrated = MigrateStoreIfNeeded();
            NormalizeStore(ensureUniqueIdentifiers: migrated);

            if (migrated || _store.Instances.Count == 0)
            {
                if (_store.Instances.Count == 0)
                {
                    _store.Instances.Add(CreateDefaultWhatsAppInstance());
                    NormalizeStore(ensureUniqueIdentifiers: true);
                }

                await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
            }

            _isLoaded = true;
        }
        finally
        {
            _gate.Release();
        }
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
            var instance = FindById(instanceId)
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
            var instance = FindById(instanceId) ??
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
            return _store.Instances.FirstOrDefault(i => i.Id.Equals(instanceId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateInstanceCategoryAsync(
        string instanceId,
        WorkspaceCategory category,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var instance = FindById(instanceId)
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
            var instance = FindById(instanceId)
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

    public async Task UpdateInstanceNotificationsMutedAsync(
        string instanceId,
        bool muted,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var instance = FindById(instanceId)
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
            var instance = FindById(instanceId)
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

    public async Task ReorderInstanceBeforeAsync(
        string instanceId,
        string targetInstanceId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var instance = FindById(instanceId)
                ?? throw new InvalidOperationException("Instance not found.");
            var target = FindById(targetInstanceId)
                ?? throw new InvalidOperationException("Target instance not found.");

            if (instance.IsProfessional != target.IsProfessional)
            {
                return;
            }

            var peers = _store.Instances
                .Where(i => i.IsProfessional == instance.IsProfessional)
                .OrderBy(i => i.SortOrder)
                .ThenBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            peers.Remove(instance);
            var targetIndex = peers.FindIndex(i => i.Id.Equals(targetInstanceId, StringComparison.OrdinalIgnoreCase));
            if (targetIndex < 0)
            {
                peers.Add(instance);
            }
            else
            {
                peers.Insert(targetIndex, instance);
            }

            RenormalizeSortOrders(peers, preserveListOrder: true);
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
            var instance = FindById(instanceId)
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
            var instance = FindById(instanceId)
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

    private async Task SaveCoreAsync(CancellationToken cancellationToken)
    {
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
            Debug.WriteLine($"Could not back up corrupt instances file: {ex.Message}");
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

            instance.StartUrl = ResolveStartUrl(platform, instance.StartUrl);
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
