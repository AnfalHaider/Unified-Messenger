using UnifiedMessenger.Services.Adapters;

namespace UnifiedMessenger.Tests;

[Collection(UnifiedMessengerSerialCollection.Name)]
public class PlatformAdapterFactoryTests
{
    [Theory]
    [InlineData("whatsapp", "whatsapp")]
    [InlineData("whatsappbusiness", "whatsappbusiness")]
    [InlineData("generic", "generic")]
    [InlineData("googlebusiness", "generic")]
    [InlineData("telegram", "generic")]
    [InlineData("messenger", "generic")]
    [InlineData("discord", "generic")]
    [InlineData("metabusinesssuite", "generic")]
    [InlineData("instagram", "generic")]
    [InlineData("unknown", "whatsapp")]
    [InlineData(" WhatsApp ", "whatsapp")]
    public void Resolve_ReturnsWhatsAppFamilyAdapter(string platformId, string expectedPlatformId)
    {
        var adapter = PlatformAdapterFactory.Resolve(platformId);
        Assert.Equal(expectedPlatformId, adapter.PlatformId);
    }
}

public class AdapterAssetTests
{
    [Fact]
    public void WhatsAppAdapterScriptExistsInOutput()
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", "whatsapp-adapter.js");
        Assert.True(File.Exists(scriptPath), $"Missing adapter script: {scriptPath}");
    }

    [Fact]
    public void WhatsAppChromeStylesExistInOutput()
    {
        var cssPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Styles", "whatsapp-chrome.css");
        Assert.True(File.Exists(cssPath), $"Missing chrome CSS: {cssPath}");
    }
}
