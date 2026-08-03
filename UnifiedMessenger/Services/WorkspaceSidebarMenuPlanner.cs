using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public enum SidebarMenuEntryKind
{
    SectionHeader,

    /// <summary>The Dashboard row. Kept distinct from <see cref="Section"/> so its existing automation
    /// id and visuals (which the UI smoke harness clicks by name) stay exactly as they were.</summary>
    Dashboard,

    /// <summary>A navigable section row — Analytics, Reviews, Reports.</summary>
    Section,

    EmptyHint,
    Instance
}

/// <summary>Which scope of accounts the sidebar shows.</summary>
public enum SidebarScope
{
    All,
    Professional,
    Personal
}

public sealed record SidebarMenuEntry(
    string Key,
    SidebarMenuEntryKind Kind,
    MessengerInstance? Instance = null,
    string? SectionTitle = null,
    string? HintText = null,
    ShellSection? Section = null,
    string? IconGlyph = null);

public sealed class SidebarMenuPlan
{
    public required IReadOnlyList<SidebarMenuEntry> Entries { get; init; }
}

public static class WorkspaceSidebarMenuPlanner
{
    public const string OverviewHeaderKey = "header:overview";

    public const string ActiveAccountsHeaderKey = "header:active-accounts";

    public const string ProfessionalHeaderKey = "header:professional";

    public const string PersonalHeaderKey = "header:personal";

    public const string ActiveAccountsEmptyKey = "empty:active-accounts";

    public static SidebarMenuPlan BuildPlan(
        IEnumerable<MessengerInstance> instances,
        SidebarScope scope = SidebarScope.All)
    {
        ArgumentNullException.ThrowIfNull(instances);

        var enabledInstances = FilterScope(PlatformModuleSettingsHelper.FilterSidebarVisibleInstances(instances), scope).ToList();
        return BuildWhatsAppFocusPlan(enabledInstances);
    }

    /// <summary>Filter accounts to the chosen scope. Pure; used by the sidebar scope switch.</summary>
    public static IEnumerable<MessengerInstance> FilterScope(IEnumerable<MessengerInstance> instances, SidebarScope scope) =>
        scope switch
        {
            SidebarScope.Professional => instances.Where(i => i.IsProfessional),
            SidebarScope.Personal => instances.Where(i => !i.IsProfessional),
            _ => instances
        };

    /// <summary>True when both scopes have accounts — i.e. the scope switch is worth showing.</summary>
    public static bool HasMixedScopes(IEnumerable<MessengerInstance> instances)
    {
        var enabled = PlatformModuleSettingsHelper.FilterSidebarVisibleInstances(instances).ToList();
        return enabled.Any(i => i.IsProfessional) && enabled.Any(i => !i.IsProfessional);
    }

    private static SidebarMenuPlan BuildWhatsAppFocusPlan(IReadOnlyList<MessengerInstance> enabledInstances)
    {
        var entries = new List<SidebarMenuEntry>
        {
            new(OverviewHeaderKey, SidebarMenuEntryKind.SectionHeader, SectionTitle: "Overview"),
            new(WorkspaceSidebarHelper.DashboardSelectionKey, SidebarMenuEntryKind.Dashboard)
        };

        entries.AddRange(BuildSectionEntries());

        if (enabledInstances.Count == 0)
        {
            entries.Add(new SidebarMenuEntry(
                ActiveAccountsEmptyKey,
                SidebarMenuEntryKind.EmptyHint,
                HintText: "No accounts yet."));
            return new SidebarMenuPlan { Entries = entries };
        }

        var (professional, personal) = WorkspaceSidebarHelper.PartitionInstances(enabledInstances);
        var professionalList = professional.ToList();
        var personalList = personal.ToList();

        // Scope-first navigation: group accounts under "Professional" and "Personal" headers (only the
        // groups that have accounts). A single mixed list collapses to one header so the structure stays
        // clean when the owner runs only one scope.
        if (professionalList.Count > 0 && personalList.Count > 0)
        {
            AddProfessionalSection(entries, ProfessionalHeaderKey, "PROFESSIONAL", professionalList);
            AddSection(entries, PersonalHeaderKey, "PERSONAL", personalList);
        }
        else if (personalList.Count == 0)
        {
            AddProfessionalSection(entries, ActiveAccountsHeaderKey, "ACTIVE ACCOUNTS", professionalList);
        }
        else
        {
            AddSection(entries, ActiveAccountsHeaderKey, "ACTIVE ACCOUNTS", personalList);
        }

        return new SidebarMenuPlan { Entries = entries };
    }

    /// <summary>
    /// The section rows that sit under "Overview" beside Dashboard. Notifications and Settings are
    /// deliberately absent: they already exist as footer buttons with working badges and automation ids,
    /// and duplicating a destination in two places is worse than an unconventional position.
    /// </summary>
    /// <remarks>Glyphs are Segoe Fluent Icons, matching the rest of the shell.</remarks>
    private static IEnumerable<SidebarMenuEntry> BuildSectionEntries()
    {
        yield return SectionEntry(ShellSection.Analytics, "\uE9D2"); // chart
        yield return SectionEntry(ShellSection.Reviews, "\uE734");   // star
        yield return SectionEntry(ShellSection.Reports, "\uE9F9");   // report
    }

    private static SidebarMenuEntry SectionEntry(ShellSection section, string glyph) =>
        new(
            WorkspaceSidebarHelper.SectionSelectionKey(section),
            SidebarMenuEntryKind.Section,
            SectionTitle: SectionTitle(section),
            Section: section,
            IconGlyph: glyph);

    public static string SectionTitle(ShellSection section) => section switch
    {
        ShellSection.Analytics => "Analytics",
        ShellSection.Reviews => "Reviews",
        ShellSection.Reports => "Reports",
        ShellSection.Settings => "Settings",
        _ => "Dashboard"
    };

    private static void AddSection(
        List<SidebarMenuEntry> entries,
        string headerKey,
        string title,
        IReadOnlyList<MessengerInstance> instances)
    {
        entries.Add(new SidebarMenuEntry(headerKey, SidebarMenuEntryKind.SectionHeader, SectionTitle: title));
        foreach (var instance in instances)
        {
            entries.Add(new SidebarMenuEntry(instance.Id.Trim(), SidebarMenuEntryKind.Instance, Instance: instance));
        }
    }

    /// <summary>
    /// The professional scope becomes a location rail: accounts assigned to the same location appear
    /// under a location sub-header (only when a location has 2+ accounts, so single-account locations
    /// stay flat and the rail isn't cluttered). Order: grouped locations first, then ungrouped accounts.
    /// </summary>
    private static void AddProfessionalSection(
        List<SidebarMenuEntry> entries,
        string headerKey,
        string title,
        IReadOnlyList<MessengerInstance> professional)
    {
        entries.Add(new SidebarMenuEntry(headerKey, SidebarMenuEntryKind.SectionHeader, SectionTitle: title));

        var groups = professional
            .GroupBy(BranchWorkspaceHelper.ResolveBranchKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in groups.Where(g => g.Count() >= 2))
        {
            entries.Add(new SidebarMenuEntry(
                $"loc:{group.Key.ToLowerInvariant()}",
                SidebarMenuEntryKind.SectionHeader,
                SectionTitle: group.Key));
            foreach (var instance in group)
            {
                entries.Add(new SidebarMenuEntry(instance.Id.Trim(), SidebarMenuEntryKind.Instance, Instance: instance));
            }
        }

        foreach (var instance in groups.Where(g => g.Count() < 2).SelectMany(g => g))
        {
            entries.Add(new SidebarMenuEntry(instance.Id.Trim(), SidebarMenuEntryKind.Instance, Instance: instance));
        }
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
