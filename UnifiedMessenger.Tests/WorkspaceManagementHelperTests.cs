using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using Xunit;

namespace UnifiedMessenger.Tests;

public class WorkspaceManagementHelperTests
{
    private static MessengerInstance Pro(string id, string branch) =>
        new()
        {
            Id = id,
            DisplayName = id,
            ProfileName = id,
            Platform = "whatsapp",
            BranchKey = branch,
            Category = WorkspaceCategory.Professional
        };

    private static MessengerInstance Personal(string id) =>
        new()
        {
            Id = id,
            DisplayName = id,
            ProfileName = id,
            Platform = "whatsapp",
            Category = WorkspaceCategory.Personal
        };

    [Fact]
    public void DerivesDistinctProfessionalLocations_PreservesExisting_ExcludesPersonal()
    {
        var instances = new List<MessengerInstance>
        {
            Pro("a", "F-11"), Pro("b", "F-11"), Pro("c", "DHA"), Personal("p")
        };
        var existing = new List<WorkspaceProfile>
        {
            new() { LocationKey = "F-11", DisplayName = "F-11", SlaThresholdMinutes = 20 }
        };

        var result = WorkspaceManagementHelper.BuildEditableProfiles(instances, existing);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.LocationKey == "F-11" && p.SlaThresholdMinutes == 20);
        Assert.Contains(result, p => p.LocationKey == "DHA" && p.SlaThresholdMinutes is null);
    }

    [Fact]
    public void NoProfessionalInstances_ReturnsEmpty()
    {
        var result = WorkspaceManagementHelper.BuildEditableProfiles(
            [Personal("p")], []);

        Assert.Empty(result);
    }
}
