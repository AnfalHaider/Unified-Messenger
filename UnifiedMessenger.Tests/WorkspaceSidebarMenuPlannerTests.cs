using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using Xunit;

namespace UnifiedMessenger.Tests;

public class WorkspaceSidebarMenuPlannerTests
{
    private static MessengerInstance Inst(string id, bool professional, string? branchKey = null) =>
        new()
        {
            Id = id,
            DisplayName = id,
            ProfileName = id,
            Platform = "whatsapp",
            Category = professional ? WorkspaceCategory.Professional : WorkspaceCategory.Personal,
            BranchKey = branchKey
        };

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
}
