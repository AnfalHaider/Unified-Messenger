using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public interface IInstanceRegistryService
{
    IReadOnlyList<MessengerInstance> Instances { get; }

    IReadOnlyList<MessengerInstance> ArchivedInstances { get; }

    string StorePath { get; }

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);

    MessengerInstance? FindById(string instanceId);

    IEnumerable<MessengerInstance> GetOrderedInstances();

    Task<MessengerInstance> AddInstanceAsync(
        string displayName,
        string platformId,
        string? customUrl,
        WorkspaceCategory category = WorkspaceCategory.Personal,
        CancellationToken cancellationToken = default);

    Task<MessengerInstance> RestoreArchivedInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default);

    Task RemoveFromSidebarAsync(string instanceId, CancellationToken cancellationToken = default);

    Task RemovePermanentlyAsync(string instanceId, CancellationToken cancellationToken = default);

    Task UpdateInstanceCategoryAsync(
        string instanceId,
        WorkspaceCategory category,
        CancellationToken cancellationToken = default);

    Task UpdateInstanceDisplayNameAsync(
        string instanceId,
        string displayName,
        CancellationToken cancellationToken = default);

    /// <summary>Set (or clear, when null) the account's built-in avatar icon glyph, flat color, and font.</summary>
    Task UpdateInstanceAvatarIconAsync(
        string instanceId,
        string? iconGlyph,
        string? iconColor,
        string? iconFontFamily = null,
        CancellationToken cancellationToken = default);

    /// <summary>Assign (or clear, when null/empty) the account's location/workspace key. Metadata only —
    /// does not reload the session.</summary>
    Task UpdateInstanceBranchKeyAsync(
        string instanceId,
        string? branchKey,
        CancellationToken cancellationToken = default);

    Task UpdateInstanceNotificationsMutedAsync(
        string instanceId,
        bool isMuted,
        CancellationToken cancellationToken = default);

    Task MoveInstanceAsync(
        string instanceId,
        int direction,
        CancellationToken cancellationToken = default);

    Task ReorderInstanceBeforeAsync(
        string instanceId,
        string targetInstanceId,
        CancellationToken cancellationToken = default);

    Task UpdateInstanceMemoryTierAsync(
        string instanceId,
        MemoryTierPreference tier,
        CancellationToken cancellationToken = default);

    Task UpdateInstanceMetadataAsync(
        string instanceId,
        string displayName,
        string startUrl,
        string platformId,
        string? notes,
        string? branchKey = null,
        CancellationToken cancellationToken = default);

    Task ExportInstancesAsync(string destinationPath, CancellationToken cancellationToken = default);

    Task<ImportInstancesResult> ImportInstancesAsync(
        string sourcePath,
        CancellationToken cancellationToken = default);
}
