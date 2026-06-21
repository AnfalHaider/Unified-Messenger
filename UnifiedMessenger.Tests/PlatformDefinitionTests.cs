using UnifiedMessenger.Models;

namespace UnifiedMessenger.Tests;

public class PlatformDefinitionTests
{
    [Theory]
    [InlineData("whatsapp", "WhatsApp")]
    [InlineData("whatsappbusiness", "WhatsApp Business")]
    [InlineData("telegram", "Telegram")]
    [InlineData("messenger", "Messenger")]
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
        Assert.Null(PlatformDefinition.FindById("not-a-real-platform"));
    }

    [Fact]
    public void NormalizePlatformId_UnknownPlatformFallsBackToWhatsApp()
    {
        Assert.Equal("whatsapp", PlatformDefinition.NormalizePlatformId("not-a-platform"));
        Assert.Equal("whatsapp", PlatformDefinition.NormalizePlatformId("WHATSAPP"));
    }

    [Fact]
    public void All_ContainsAllRegisteredPlatforms()
    {
        var ids = PlatformDefinition.All.Select(p => p.Id).ToList();

        Assert.Equal(9, ids.Count);
        Assert.Contains("whatsapp", ids);
        Assert.Contains("whatsappbusiness", ids);
        Assert.Contains("googlebusiness", ids);
        Assert.Contains("telegram", ids);
        Assert.Contains("messenger", ids);
        Assert.Contains("discord", ids);
        Assert.Contains("metabusinesssuite", ids);
        Assert.Contains("instagram", ids);
        Assert.Contains("generic", ids);
    }

    [Theory]
    [InlineData("discord", "Discord")]
    [InlineData("metabusinesssuite", "Meta Business Suite")]
    [InlineData("instagram", "Instagram")]
    public void EmbedChannels_AreRegisteredWithDefaultUrl(string id, string displayName)
    {
        var platform = PlatformDefinition.FindById(id);

        Assert.NotNull(platform);
        Assert.Equal(displayName, platform!.DisplayName);
        Assert.Equal(id, PlatformDefinition.NormalizePlatformId(id));
        Assert.False(string.IsNullOrWhiteSpace(platform.DefaultUrl));
    }

    [Fact]
    public void GoogleBusiness_IsRegisteredAsEmbeddableChannel()
    {
        var google = PlatformDefinition.FindById("googlebusiness");

        Assert.NotNull(google);
        Assert.Equal("Google Business", google!.DisplayName);
        Assert.Equal("googlebusiness", PlatformDefinition.NormalizePlatformId("googlebusiness"));
        Assert.False(string.IsNullOrWhiteSpace(google.DefaultUrl));
    }

    [Fact]
    public void Telegram_IsRegisteredAsEmbeddableChannel()
    {
        var telegram = PlatformDefinition.FindById("telegram");

        Assert.NotNull(telegram);
        Assert.Equal("Telegram", telegram!.DisplayName);
        Assert.Equal("telegram", PlatformDefinition.NormalizePlatformId("telegram"));
        Assert.False(string.IsNullOrWhiteSpace(telegram.DefaultUrl));
    }

    [Fact]
    public void Messenger_IsRegisteredAsEmbeddableChannel()
    {
        var messenger = PlatformDefinition.FindById("messenger");

        Assert.NotNull(messenger);
        Assert.Equal("Messenger", messenger!.DisplayName);
        Assert.Equal("messenger", PlatformDefinition.NormalizePlatformId("messenger"));
        Assert.False(string.IsNullOrWhiteSpace(messenger.DefaultUrl));
    }

    [Fact]
    public void Generic_IsRegisteredWithEmptyDefaultUrl()
    {
        // The generic web-page platform must round-trip through Find/Normalize (so it isn't collapsed to
        // whatsapp) and must have a blank DefaultUrl so ResolveStartUrl accepts any user-supplied host.
        var generic = PlatformDefinition.FindById("generic");

        Assert.NotNull(generic);
        Assert.Equal("Web page", generic!.DisplayName);
        Assert.Equal(string.Empty, generic.DefaultUrl);
        Assert.Equal("generic", PlatformDefinition.NormalizePlatformId("generic"));
    }
}
