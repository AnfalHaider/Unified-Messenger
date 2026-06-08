using System.Collections.ObjectModel;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class DashboardPageHelperBranchFilterTests
{
    [Fact]
    public void BuildBranchFilterCollection_GroupsByBranchKey()
    {
        var instances = new[]
        {
            new MessengerInstance { Id = "wa-dha", DisplayName = "Depilex DHA-2", Category = WorkspaceCategory.Professional },
            new MessengerInstance { Id = "meta-dha", DisplayName = "Depilex DHA-2 Meta", Category = WorkspaceCategory.Professional },
            new MessengerInstance { Id = "f11", DisplayName = "Depilex F-11", Category = WorkspaceCategory.Professional },
            new MessengerInstance { Id = "personal", DisplayName = "Personal WA", Category = WorkspaceCategory.Personal }
        };

        ObservableCollection<DashboardBranchFilterEntry> collection =
            DashboardPageHelper.BuildBranchFilterCollection(instances);

        Assert.Equal(3, collection.Count);
        Assert.True(collection[0].IsAllBranches);
        Assert.Equal("DHA-2 (2 inboxes)", collection[1].DisplayName);
        Assert.Equal("F-11", collection[2].DisplayName);
        Assert.Equal("DHA-2", collection[1].BranchKey);
    }

    [Fact]
    public void ResolveBranchInstanceId_AllBranches_ReturnsNull()
    {
        var entry = DashboardBranchFilterEntry.CreateAllBranches();

        Assert.Null(DashboardPageHelper.ResolveBranchInstanceId(entry));
    }

    [Fact]
    public void ResolveBranchInstanceId_BranchEntry_ReturnsBranchKey()
    {
        var entry = DashboardBranchFilterEntry.FromBranch("F-11", 2);

        Assert.Equal("F-11", DashboardPageHelper.ResolveBranchInstanceId(entry));
    }

    [Fact]
    public void FilterProfessionalInstances_ReturnsAllInboxesForBranch()
    {
        var instances = new[]
        {
            new MessengerInstance { Id = "wa-f11", DisplayName = "Depilex F-11", Category = WorkspaceCategory.Professional },
            new MessengerInstance { Id = "meta-f11", DisplayName = "Depilex F-11 Meta", Category = WorkspaceCategory.Professional },
            new MessengerInstance { Id = "dha", DisplayName = "Depilex DHA-2", Category = WorkspaceCategory.Professional }
        };

        var filtered = DashboardPageHelper.FilterProfessionalInstances(instances, "F-11").ToList();

        Assert.Equal(2, filtered.Count);
        Assert.All(filtered, instance => Assert.Equal("F-11", BranchWorkspaceHelper.ResolveBranchKey(instance)));
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
    public void BuildFilteredTriageSnapshot_RespectsBranchKey()
    {
        var triage = new MessageTriageService();
        var instances = new[]
        {
            new MessengerInstance
            {
                Id = "f11",
                DisplayName = "Depilex F-11",
                Platform = "metabusiness",
                StartUrl = "https://example.com",
                Category = WorkspaceCategory.Professional
            },
            new MessengerInstance
            {
                Id = "d12",
                DisplayName = "Depilex D-12",
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
            "Depilex F-11");

        triage.ProcessInboundForTests(
            new InboundMessageSelection
            {
                InstanceId = "d12",
                Platform = "metabusiness",
                MessageText = "Refund my payment now, terrible service."
            },
            "Depilex D-12");

        var all = DashboardPageHelper.BuildFilteredTriageSnapshot(instances, triageService: triage);
        var f11Only = DashboardPageHelper.BuildFilteredTriageSnapshot(instances, "F-11", triage);

        Assert.Equal(2, all.UrgentQueue.Count);
        Assert.Single(f11Only.UrgentQueue);
        Assert.Equal("f11", f11Only.UrgentQueue[0].InstanceId);
    }
}
