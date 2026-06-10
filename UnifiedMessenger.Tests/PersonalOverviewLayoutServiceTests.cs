using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using Xunit;

namespace UnifiedMessenger.Tests;

public class PersonalOverviewLayoutServiceTests
{
    [Fact]
    public void Resolve_SanitizesUnknownSections()
    {
        var settings = new AppSettings
        {
            PersonalOverviewSectionOrder = ["Content", "Unknown", "Search"]
        };

        var order = PersonalOverviewLayoutService.Resolve(settings);

        Assert.Equal("Content", order[0]);
        Assert.Equal("Search", order[1]);
        Assert.Contains("Summary", order);
        Assert.Contains("Toolbar", order);
    }
}
