using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed record ImportInstancesResult(int ActiveCount, int ArchivedCount);

public sealed partial class InstanceRegistryService
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
    private InstanceStore _store = new();

    public InstanceRegistryService()
    {
        var appDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UnifiedMessenger");
        _storePath = Path.Combine(appDataRoot, FileName);
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
        if (!File.Exists(_storePath))
        {
            _store = CreateDefaultStore();
            await SaveAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await using var stream = File.OpenRead(_storePath);
        var loaded = await JsonSerializer
            .DeserializeAsync<InstanceStore>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        _store = loaded ?? CreateDefaultStore();
        var migrated = MigrateStoreIfNeeded();
        ApplyBrandingToAllInstances();

        if (migrated)
        {
            await SaveAsync(cancellationToken).ConfigureAwait(false);
        }

        if (_store.Instances.Count == 0)
        {
            _store.Instances.Add(CreateDefaultWhatsAppInstance());
            await SaveAsync(cancellationToken).ConfigureAwait(false);
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
            SortOrder = _store.Instances.Count + 1
        };
        instance.ApplyPlatformBranding();

        _store.Instances.Add(instance);
        await SaveAsync(cancellationToken).ConfigureAwait(false);
        return instance;
    }

    public async Task<MessengerInstance> RestoreArchivedInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        var archived = _store.ArchivedInstances.FirstOrDefault(
            i => i.Id.Equals(instanceId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Archived instance not found.");

        _store.ArchivedInstances.Remove(archived);
        _store.Instances.Add(archived);
        await SaveAsync(cancellationToken).ConfigureAwait(false);
        return archived;
    }

    public async Task RemoveFromSidebarAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        var instance = FindById(instanceId)
            ?? throw new InvalidOperationException("Instance not found.");

        _store.Instances.Remove(instance);
        _store.ArchivedInstances.RemoveAll(i => i.Id.Equals(instanceId, StringComparison.OrdinalIgnoreCase));
        _store.ArchivedInstances.Add(instance);
        await SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RemovePermanentlyAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        var instance = FindById(instanceId) ??
                       _store.ArchivedInstances.FirstOrDefault(
                           i => i.Id.Equals(instanceId, StringComparison.OrdinalIgnoreCase))
                       ?? throw new InvalidOperationException("Instance not found.");

        _store.Instances.RemoveAll(i => i.Id.Equals(instanceId, StringComparison.OrdinalIgnoreCase));
        _store.ArchivedInstances.RemoveAll(i => i.Id.Equals(instanceId, StringComparison.OrdinalIgnoreCase));
        await SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);

        await using var stream = File.Create(_storePath);
        await JsonSerializer
            .SerializeAsync(stream, _store, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public MessengerInstance? FindById(string instanceId) =>
        _store.Instances.FirstOrDefault(i => i.Id.Equals(instanceId, StringComparison.OrdinalIgnoreCase));

    public async Task UpdateInstanceCategoryAsync(
        string instanceId,
        WorkspaceCategory category,
        CancellationToken cancellationToken = default)
    {
        var instance = FindById(instanceId)
            ?? throw new InvalidOperationException("Instance not found.");

        if (instance.Category == category)
        {
            return;
        }

        instance.Category = category;
        await SaveAsync(cancellationToken).ConfigureAwait(false);
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

        var instance = FindById(instanceId)
            ?? throw new InvalidOperationException("Instance not found.");

        if (instance.DisplayName.Equals(trimmed, StringComparison.Ordinal))
        {
            return;
        }

        instance.DisplayName = trimmed;
        await SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateInstanceNotificationsMutedAsync(
        string instanceId,
        bool muted,
        CancellationToken cancellationToken = default)
    {
        var instance = FindById(instanceId)
            ?? throw new InvalidOperationException("Instance not found.");

        instance.NotificationsMuted = muted;
        await SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MoveInstanceAsync(
        string instanceId,
        int direction,
        CancellationToken cancellationToken = default)
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

        await SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReorderInstanceBeforeAsync(
        string instanceId,
        string targetInstanceId,
        CancellationToken cancellationToken = default)
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

        for (var i = 0; i < peers.Count; i++)
        {
            peers[i].SortOrder = i + 1;
        }

        await SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateInstanceMemoryTierAsync(
        string instanceId,
        MemoryTierPreference tier,
        CancellationToken cancellationToken = default)
    {
        var instance = FindById(instanceId)
            ?? throw new InvalidOperationException("Instance not found.");

        instance.MemoryTier = tier;
        await SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateInstanceMetadataAsync(
        string instanceId,
        string displayName,
        string startUrl,
        string platformId,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var instance = FindById(instanceId)
            ?? throw new InvalidOperationException("Instance not found.");

        var platform = PlatformDefinition.FindById(platformId)
            ?? throw new ArgumentException($"Unknown platform: {platformId}", nameof(platformId));

        instance.DisplayName = displayName.Trim();
        instance.StartUrl = ResolveStartUrl(platform, startUrl);
        instance.Platform = platform.Id;
        instance.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        instance.ApplyPlatformBranding();
        await SaveAsync(cancellationToken).ConfigureAwait(false);
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

        await using var stream = File.OpenRead(sourcePath);
        var imported = await JsonSerializer
            .DeserializeAsync<InstanceStore>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidDataException("Import file is empty or invalid.");

        if (imported.Instances.Count == 0 && imported.ArchivedInstances.Count == 0)
        {
            throw new InvalidDataException("Import file contains no instances.");
        }

        _store = imported;
        MigrateStoreIfNeeded();
        ApplyBrandingToAllInstances();
        await SaveAsync(cancellationToken).ConfigureAwait(false);

        return new ImportInstancesResult(
            _store.Instances.Count,
            _store.ArchivedInstances.Count);
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
            Category = WorkspaceCategory.Personal
        };
    }

    private bool MigrateStoreIfNeeded()
    {
        var migrated = false;

        if (_store.Version < 2)
        {
            foreach (var instance in _store.Instances.Concat(_store.ArchivedInstances))
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

        return migrated;
    }

    private void ApplyBrandingToAllInstances()
    {
        foreach (var instance in _store.Instances)
        {
            instance.ApplyPlatformBranding();
        }

        foreach (var instance in _store.ArchivedInstances)
        {
            instance.ApplyPlatformBranding();
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
