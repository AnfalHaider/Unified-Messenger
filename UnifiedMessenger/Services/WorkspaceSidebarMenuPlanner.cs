using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public enum SidebarMenuEntryKind
{
    SectionHeader,
    Dashboard,
    EmptyHint,
    Instance
}

public sealed record SidebarMenuEntry(
    string Key,
    SidebarMenuEntryKind Kind,
    MessengerInstance? Instance = null,
    string? SectionTitle = null,
    string? HintText = null);

public sealed class SidebarMenuPlan
{
    public required IReadOnlyList<SidebarMenuEntry> Entries { get; init; }
}

public static class WorkspaceSidebarMenuPlanner
{
    public const string OverviewHeaderKey = "header:overview";

    public const string ActiveAccountsHeaderKey = "header:active-accounts";

    public const string ActiveAccountsEmptyKey = "empty:active-accounts";

    public static SidebarMenuPlan BuildPlan(IEnumerable<MessengerInstance> instances)
    {
        ArgumentNullException.ThrowIfNull(instances);

        var enabledInstances = PlatformModuleSettingsHelper.FilterEnabledInstances(instances).ToList();
        return BuildWhatsAppFocusPlan(enabledInstances);
    }

    private static SidebarMenuPlan BuildWhatsAppFocusPlan(IReadOnlyList<MessengerInstance> enabledInstances)
    {
        var entries = new List<SidebarMenuEntry>
        {
            new(OverviewHeaderKey, SidebarMenuEntryKind.SectionHeader, SectionTitle: "Overview"),
            new(WorkspaceSidebarHelper.DashboardSelectionKey, SidebarMenuEntryKind.Dashboard)
        };

        if (enabledInstances.Count == 0)
        {
            entries.Add(new SidebarMenuEntry(
                ActiveAccountsEmptyKey,
                SidebarMenuEntryKind.EmptyHint,
                HintText: "No WhatsApp accounts yet."));
            return new SidebarMenuPlan { Entries = entries };
        }

        entries.Add(new SidebarMenuEntry(
            ActiveAccountsHeaderKey,
            SidebarMenuEntryKind.SectionHeader,
            SectionTitle: "ACTIVE ACCOUNTS"));

        var (professional, personal) = WorkspaceSidebarHelper.PartitionInstances(enabledInstances);
        foreach (var instance in professional.Concat(personal))
        {
            entries.Add(new SidebarMenuEntry(
                instance.Id.Trim(),
                SidebarMenuEntryKind.Instance,
                Instance: instance));
        }

        return new SidebarMenuPlan { Entries = entries };
    }

    public static bool HasSameStructure(SidebarMenuPlan? previous, SidebarMenuPlan current)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (previous is null)
        {
            return false;
        }

        if (previous.Entries.Count != current.Entries.Count)
        {
            return false;
        }

        for (var index = 0; index < current.Entries.Count; index++)
        {
            if (!string.Equals(
                    previous.Entries[index].Key,
                    current.Entries[index].Key,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public static IReadOnlyList<string> BuildStructureKeys(SidebarMenuPlan plan) =>
        plan.Entries.Select(entry => entry.Key).ToList();
}
