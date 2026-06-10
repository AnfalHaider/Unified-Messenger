using UnifiedMessenger.Models;

namespace UnifiedMessenger.Tests;

public class PlatformDefinitionTests
{
    [Theory]
    [InlineData("whatsapp", "WhatsApp")]
    [InlineData("telegram", "Telegram")]
    [InlineData("messenger", "Messenger")]
    [InlineData("slack", "Slack")]
    [InlineData("discord", "Discord")]
    [InlineData("signal", "Signal")]
    [InlineData("teams", "Microsoft Teams")]
    [InlineData("metabusiness", "Meta Business Suite")]
    [InlineData("googlebusiness", "Google Business Profile")]
    public void FindById_ReturnsKnownPlatform(string id, string expectedName)
    {
        var platform = PlatformDefinition.FindById(id);

        Assert.NotNull(platform);
        Assert.Equal(expectedName, platform!.DisplayName);
        if (!string.Equals(id, "generic", StringComparison.OrdinalIgnoreCase))
        {
            Assert.False(string.IsNullOrWhiteSpace(platform.DefaultUrl));
        }
    }

    [Fact]
    public void FindById_IsCaseInsensitive()
    {
        var platform = PlatformDefinition.FindById("WHATSAPP");

        Assert.NotNull(platform);
        Assert.Equal("whatsapp", platform!.Id);
    }

    [Fact]
    public void FindById_ReturnsNullForUnknownPlatform()
    {
        Assert.Null(PlatformDefinition.FindById("unknown-platform"));
    }

    [Fact]
    public void NormalizePlatformId_UnknownPlatformFallsBackToGeneric()
    {
        Assert.Equal("generic", PlatformDefinition.NormalizePlatformId("not-a-platform"));
        Assert.Equal("whatsapp", PlatformDefinition.NormalizePlatformId("WHATSAPP"));
    }

    [Fact]
    public void All_ContainsUniquePlatformIds()
    {
        var ids = PlatformDefinition.All.Select(p => p.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Signal_PlatformNotesBadgeFallbackOnly()
    {
        var signal = PlatformDefinition.FindById("signal");

        Assert.NotNull(signal);
        Assert.Contains("badge", signal!.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("signal.org", signal.DefaultUrl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Teams_UsesTeamsMicrosoftUrl()
    {
        var teams = PlatformDefinition.FindById("teams");

        Assert.NotNull(teams);
        Assert.StartsWith("https://teams.microsoft.com", teams!.DefaultUrl, StringComparison.OrdinalIgnoreCase);
    }
}
