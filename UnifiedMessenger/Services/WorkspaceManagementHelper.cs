using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Backs the Workspace Management dialog: derives the editable list of locations from the
/// professional accounts (one per resolved branch/location key), reusing any existing
/// <see cref="WorkspaceProfile"/> and creating defaults for new locations. Pure and testable.
/// </summary>
public static class WorkspaceManagementHelper
{
    public static List<WorkspaceProfile> BuildEditableProfiles(
        IReadOnlyList<MessengerInstance> instances,
        IReadOnlyList<WorkspaceProfile> existing)
    {
        ArgumentNullException.ThrowIfNull(instances);
        ArgumentNullException.ThrowIfNull(existing);

        var keys = instances
            .Where(instance => instance.IsProfessional)
            .Select(BranchWorkspaceHelper.ResolveBranchKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var byKey = existing
            .Where(profile => !string.IsNullOrWhiteSpace(profile.LocationKey))
            .GroupBy(profile => profile.LocationKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var result = new List<WorkspaceProfile>();
        foreach (var key in keys)
        {
            if (byKey.TryGetValue(key, out var profile))
            {
                profile.DisplayName = string.IsNullOrWhiteSpace(profile.DisplayName) ? key : profile.DisplayName;
                result.Add(profile);
            }
            else
            {
                result.Add(new WorkspaceProfile { LocationKey = key, DisplayName = key });
            }
        }

        return result;
    }
}
