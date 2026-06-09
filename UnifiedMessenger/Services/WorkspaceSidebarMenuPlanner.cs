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

    public const string ProfessionalHeaderKey = "header:pro";

    public const string PersonalHeaderKey = "header:personal";

    public const string ProfessionalEmptyKey = "empty:pro";

    public const string PersonalEmptyKey = "empty:personal";

    public static SidebarMenuPlan BuildPlan(IEnumerable<MessengerInstance> instances)
    {
        ArgumentNullException.ThrowIfNull(instances);

        var entries = new List<SidebarMenuEntry>
        {
            new(OverviewHeaderKey, SidebarMenuEntryKind.SectionHeader, SectionTitle: "Overview"),
            new(WorkspaceSidebarHelper.DashboardSelectionKey, SidebarMenuEntryKind.Dashboard),
            new(ProfessionalHeaderKey, SidebarMenuEntryKind.SectionHeader, SectionTitle: "Pro / Business")
        };

        var (professional, personal) = WorkspaceSidebarHelper.PartitionInstances(instances);
        if (professional.Count == 0)
        {
            entries.Add(new SidebarMenuEntry(
                ProfessionalEmptyKey,
                SidebarMenuEntryKind.EmptyHint,
                HintText: "No business accounts yet."));
        }
        else
        {
            foreach (var instance in professional)
            {
                entries.Add(new SidebarMenuEntry(
                    instance.Id.Trim(),
                    SidebarMenuEntryKind.Instance,
                    Instance: instance));
            }
        }

        entries.Add(new SidebarMenuEntry(
            PersonalHeaderKey,
            SidebarMenuEntryKind.SectionHeader,
            SectionTitle: "Personal / Life"));

        if (personal.Count == 0)
        {
            entries.Add(new SidebarMenuEntry(
                PersonalEmptyKey,
                SidebarMenuEntryKind.EmptyHint,
                HintText: "No personal accounts yet."));
        }
        else
        {
            foreach (var instance in personal)
            {
                entries.Add(new SidebarMenuEntry(
                    instance.Id.Trim(),
                    SidebarMenuEntryKind.Instance,
                    Instance: instance));
            }
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
