using System.Collections.ObjectModel;
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
    public void BuildBranchFilterCollection_IncludesAllBranchesAndProfessionalOnly()
    {
        var instances = new[]
        {
            new MessengerInstance { Id = "f11", DisplayName = "Depilex F-11", Category = WorkspaceCategory.Professional },
            new MessengerInstance { Id = "d12", DisplayName = "Depilex D-12", Category = WorkspaceCategory.Professional },
            new MessengerInstance { Id = "personal", DisplayName = "Personal WA", Category = WorkspaceCategory.Personal }
        };

        ObservableCollection<DashboardBranchFilterEntry> collection =
            DashboardPageHelper.BuildBranchFilterCollection(instances);

        Assert.Equal(3, collection.Count);
        Assert.True(collection[0].IsAllBranches);
        Assert.Equal("All Branches", collection[0].DisplayName);
        Assert.Equal("Depilex D-12", collection[1].DisplayName);
        Assert.Equal("Depilex F-11", collection[2].DisplayName);
        Assert.Equal("d12", collection[1].InstanceId);
        Assert.Equal("f11", collection[2].InstanceId);
    }

    [Fact]
    public void ResolveBranchInstanceId_AllBranches_ReturnsNull()
    {
        var entry = DashboardBranchFilterEntry.CreateAllBranches();

        Assert.Null(DashboardPageHelper.ResolveBranchInstanceId(entry));
    }

    [Fact]
    public void ResolveBranchInstanceId_BranchEntry_ReturnsTrimmedId()
    {
        var entry = DashboardBranchFilterEntry.FromInstance(new MessengerInstance
        {
            Id = "  f11  ",
            DisplayName = "Depilex F-11",
            Category = WorkspaceCategory.Professional
        });

        Assert.Equal("f11", DashboardPageHelper.ResolveBranchInstanceId(entry));
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

    [Fact]
    public void FilterProfessionalInstances_NullOrWhitespace_ReturnsAll()
    {
        var instances = new[]
        {
            new MessengerInstance { Id = "f11", Category = WorkspaceCategory.Professional },
            new MessengerInstance { Id = "dha", Category = WorkspaceCategory.Professional }
        };

        Assert.Equal(2, DashboardPageHelper.FilterProfessionalInstances(instances, null).Count());
        Assert.Equal(2, DashboardPageHelper.FilterProfessionalInstances(instances, "   ").Count());
    }

    [Fact]
    public void BuildFilteredTriageSnapshot_RespectsBranchInstanceId()
    {
        var triage = new MessageTriageService();
        var instances = new[]
        {
            new MessengerInstance
            {
                Id = "f11",
                DisplayName = "F-11",
                Platform = "metabusiness",
                StartUrl = "https://example.com",
                Category = WorkspaceCategory.Professional
            },
            new MessengerInstance
            {
                Id = "d12",
                DisplayName = "D-12",
                Platform = "metabusiness",
                StartUrl = "https://example.com",
                Category = WorkspaceCategory.Professional
            }
        };

        triage.ProcessInboundForTests(
            new InboundMessageSelection
            {
                InstanceId = "f11",
                Platform = "metabusiness",
                MessageText = "Cancel my booking immediately, urgent."
            },
            "F-11");

        triage.ProcessInboundForTests(
            new InboundMessageSelection
            {
                InstanceId = "d12",
                Platform = "metabusiness",
                MessageText = "Refund my payment now, terrible service."
            },
            "D-12");

        var all = DashboardPageHelper.BuildFilteredTriageSnapshot(instances, triageService: triage);
        var f11Only = DashboardPageHelper.BuildFilteredTriageSnapshot(instances, "f11", triage);

        Assert.Equal(2, all.UrgentQueue.Count);
        Assert.Single(f11Only.UrgentQueue);
        Assert.Equal("f11", f11Only.UrgentQueue[0].InstanceId);
        Assert.Equal(1, f11Only.PositiveCount + f11Only.NeutralCount + f11Only.NegativeCount);
    }
}
