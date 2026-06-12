using UnifiedMessenger.Models;

namespace UnifiedMessenger.Tests;

public class PlatformDefinitionTests
{
    [Theory]
    [InlineData("whatsapp", "WhatsApp")]
    [InlineData("whatsappbusiness", "WhatsApp Business")]
    public void FindById_ReturnsKnownPlatform(string id, string expectedName)
    {
        var platform = PlatformDefinition.FindById(id);

        Assert.NotNull(platform);
        Assert.Equal(expectedName, platform!.DisplayName);
        Assert.False(string.IsNullOrWhiteSpace(platform.DefaultUrl));
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
        Assert.Null(PlatformDefinition.FindById("telegram"));
    }

    [Fact]
    public void NormalizePlatformId_UnknownPlatformFallsBackToWhatsApp()
    {
        Assert.Equal("whatsapp", PlatformDefinition.NormalizePlatformId("not-a-platform"));
        Assert.Equal("whatsapp", PlatformDefinition.NormalizePlatformId("WHATSAPP"));
    }

    [Fact]
    public void All_ContainsOnlyWhatsAppPlatforms()
    {
        var ids = PlatformDefinition.All.Select(p => p.Id).ToList();

        Assert.Equal(2, ids.Count);
        Assert.Contains("whatsapp", ids);
        Assert.Contains("whatsappbusiness", ids);
    }
}
