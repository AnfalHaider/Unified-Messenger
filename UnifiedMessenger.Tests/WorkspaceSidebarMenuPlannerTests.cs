using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using Xunit;

namespace UnifiedMessenger.Tests;

public class WorkspaceSidebarMenuPlannerTests
{
    private static MessengerInstance Inst(string id, bool professional, string? branchKey = null, string platform = "whatsapp") =>
        new()
        {
            Id = id,
            DisplayName = id,
            ProfileName = id,
            Platform = platform,
            Category = professional ? WorkspaceCategory.Professional : WorkspaceCategory.Personal,
            BranchKey = branchKey
        };

    [Theory]
    [InlineData("googlebusiness")]
    [InlineData("telegram")]
    [InlineData("messenger")]
    [InlineData("generic")]
    public void BuildPlan_EmbedChannels_AppearInSidebar(string platform)
    {
        // Regression: embed channels are addable in "Add account" and showed in the Work Queue, but the
        // sidebar gated on WhatsApp-only, so they were addable-but-invisible (and thus un-openable).
        var plan = WorkspaceSidebarMenuPlanner.BuildPlan(new[]
        {
            Inst("embed", professional: true, platform: platform)
        });

        Assert.Contains("embed", plan.Entries.Select(e => e.Key));
    }

    [Fact]
    public void BuildPlan_ProfessionalWithSharedLocation_AddsLocationSubHeader()
    {
        var plan = WorkspaceSidebarMenuPlanner.BuildPlan(new[]
        {
            Inst("a", professional: true, branchKey: "Islamabad"),
            Inst("b", professional: true, branchKey: "Islamabad"),
            Inst("c", professional: true) // its own location → no sub-header
        });

        var keys = plan.Entries.Select(e => e.Key).ToList();
        Assert.Contains("loc:islamabad", keys);                 // shared location grouped
        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
        Assert.Contains("c", keys);
        Assert.DoesNotContain(keys, k => k.StartsWith("loc:", StringComparison.Ordinal) && k != "loc:islamabad");
    }

    [Fact]
    public void BuildPlan_MixedScopes_SplitsIntoProfessionalAndPersonalSections()
    {
        var plan = WorkspaceSidebarMenuPlanner.BuildPlan(new[]
        {
            Inst("pro1", professional: true),
            Inst("per1", professional: false)
        });

        var keys = plan.Entries.Select(e => e.Key).ToList();
        Assert.Contains(WorkspaceSidebarMenuPlanner.ProfessionalHeaderKey, keys);
        Assert.Contains(WorkspaceSidebarMenuPlanner.PersonalHeaderKey, keys);
        Assert.DoesNotContain(WorkspaceSidebarMenuPlanner.ActiveAccountsHeaderKey, keys);

        // Professional section precedes the personal one.
        Assert.True(
            keys.IndexOf(WorkspaceSidebarMenuPlanner.ProfessionalHeaderKey) <
            keys.IndexOf(WorkspaceSidebarMenuPlanner.PersonalHeaderKey));
    }

    [Fact]
    public void FilterScope_AndHasMixedScopes()
    {
        var instances = new[]
        {
            Inst("pro1", professional: true),
            Inst("per1", professional: false)
        };

        Assert.True(WorkspaceSidebarMenuPlanner.HasMixedScopes(instances));
        Assert.Single(WorkspaceSidebarMenuPlanner.FilterScope(instances, SidebarScope.Professional));
        Assert.Single(WorkspaceSidebarMenuPlanner.FilterScope(instances, SidebarScope.Personal));
        Assert.Equal(2, WorkspaceSidebarMenuPlanner.FilterScope(instances, SidebarScope.All).Count());

        var proOnly = new[] { Inst("pro1", professional: true) };
        Assert.False(WorkspaceSidebarMenuPlanner.HasMixedScopes(proOnly));
    }

    [Fact]
    public void BuildPlan_SingleScope_UsesOneActiveAccountsHeader()
    {
        var plan = WorkspaceSidebarMenuPlanner.BuildPlan(new[]
        {
            Inst("pro1", professional: true),
            Inst("pro2", professional: true)
        });

        var keys = plan.Entries.Select(e => e.Key).ToList();
        Assert.Contains(WorkspaceSidebarMenuPlanner.ActiveAccountsHeaderKey, keys);
        Assert.DoesNotContain(WorkspaceSidebarMenuPlanner.ProfessionalHeaderKey, keys);
        Assert.DoesNotContain(WorkspaceSidebarMenuPlanner.PersonalHeaderKey, keys);
    }

    [Fact]
    public void BuildPlan_EmitsTheNavigableSectionRows()
    {
        var plan = WorkspaceSidebarMenuPlanner.BuildPlan(new[] { Inst("pro1", professional: true) });

        var sections = plan.Entries
            .Where(e => e.Kind == SidebarMenuEntryKind.Section)
            .ToList();

        Assert.Equal(3, sections.Count);
        Assert.Equal(
            new[] { ShellSection.Analytics, ShellSection.Reviews, ShellSection.Reports },
            sections.Select(e => e.Section!.Value).ToArray());

        // Every section row must carry the payload the click handler navigates with, and a glyph.
        Assert.All(sections, e => Assert.NotNull(e.Section));
        Assert.All(sections, e => Assert.False(string.IsNullOrWhiteSpace(e.IconGlyph)));
        Assert.All(sections, e => Assert.False(string.IsNullOrWhiteSpace(e.SectionTitle)));
    }

    [Fact]
    public void BuildPlan_SectionRowKeysAreUniqueAndMatchTheSelectionKeys()
    {
        // Row keys are how selection is resolved, so a collision would highlight the wrong row.
        var plan = WorkspaceSidebarMenuPlanner.BuildPlan(Array.Empty<MessengerInstance>());
        var keys = plan.Entries.Select(e => e.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var entry in plan.Entries.Where(e => e.Kind == SidebarMenuEntryKind.Section))
        {
            Assert.Equal(WorkspaceSidebarHelper.SectionSelectionKey(entry.Section!.Value), entry.Key);
        }
    }

    [Fact]
    public void BuildPlan_SectionsAppearWithNoAccountsConfigured()
    {
        // The rail must still be navigable on a fresh install, before any account exists.
        var plan = WorkspaceSidebarMenuPlanner.BuildPlan(Array.Empty<MessengerInstance>());

        Assert.Equal(3, plan.Entries.Count(e => e.Kind == SidebarMenuEntryKind.Section));
        Assert.Contains(plan.Entries, e => e.Kind == SidebarMenuEntryKind.Dashboard);
        Assert.Contains(plan.Entries, e => e.Key == WorkspaceSidebarMenuPlanner.ActiveAccountsEmptyKey);
    }
}
