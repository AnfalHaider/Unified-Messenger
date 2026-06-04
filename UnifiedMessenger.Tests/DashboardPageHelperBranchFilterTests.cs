using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class DashboardPageHelperBranchFilterTests
{
    [Fact]
    public void BuildBranchOptions_IncludesAllBranchesFirst()
    {
        var instances = new[]
        {
            new MessengerInstance { Id = "f11", DisplayName = "Depilex F-11", Platform = "googlebusiness", Category = WorkspaceCategory.Professional },
            new MessengerInstance { Id = "dha", DisplayName = "Depilex DHA-2", Platform = "metabusiness", Category = WorkspaceCategory.Professional }
        };

        var options = DashboardPageHelper.BuildBranchOptions(instances);

        Assert.Equal("All Branches", options[0].DisplayName);
        Assert.Equal(3, options.Count);
    }

    [Fact]
    public void FilterProfessionalInstances_ReturnsSingleBranch()
    {
        var instances = new[]
        {
            new MessengerInstance { Id = "f11", DisplayName = "Depilex F-11", Category = WorkspaceCategory.Professional },
            new MessengerInstance { Id = "dha", DisplayName = "Depilex DHA-2", Category = WorkspaceCategory.Professional }
        };

        var filtered = DashboardPageHelper.FilterProfessionalInstances(instances, "dha").ToList();

        Assert.Single(filtered);
        Assert.Equal("dha", filtered[0].Id);
    }
}
