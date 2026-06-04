namespace UnifiedMessenger.Models;

/// <summary>
/// ComboBox item for dashboard branch filtering. "All Branches" uses <see cref="IsAllBranches"/> with no instance.
/// </summary>
public sealed class DashboardBranchFilterEntry
{
    public bool IsAllBranches { get; set; }

    public MessengerInstance? Instance { get; set; }

    public string InstanceId =>
        IsAllBranches ? string.Empty : Instance?.Id?.Trim() ?? string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public static DashboardBranchFilterEntry CreateAllBranches() =>
        new() { IsAllBranches = true, DisplayName = "All Branches" };

    public static DashboardBranchFilterEntry FromInstance(MessengerInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        return new DashboardBranchFilterEntry
        {
            IsAllBranches = false,
            Instance = instance,
            DisplayName = string.IsNullOrWhiteSpace(instance.DisplayName)
                ? instance.Id
                : instance.DisplayName.Trim()
        };
    }
}
