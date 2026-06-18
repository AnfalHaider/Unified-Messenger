using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using Xunit;

namespace UnifiedMessenger.Tests;

public class WorkspaceSidebarMenuPlannerTests
{
    private static MessengerInstance Inst(string id, bool professional) =>
        new()
        {
            Id = id,
            DisplayName = id,
            ProfileName = id,
            Platform = "whatsapp",
            Category = professional ? WorkspaceCategory.Professional : WorkspaceCategory.Personal
        };

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
