using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Adapters;

namespace UnifiedMessenger.Tests;

public class PlatformAdapterFactoryTests
{
    [Theory]
    [InlineData("whatsapp", "whatsapp")]
    [InlineData("telegram", "telegram")]
    [InlineData("messenger", "messenger")]
    [InlineData("slack", "slack")]
    [InlineData("discord", "discord")]
    [InlineData("signal", "signal")]
    [InlineData("teams", "teams")]
    [InlineData("metabusiness", "metabusiness")]
    [InlineData("googlebusiness", "googlebusiness")]
    [InlineData("unknown", "generic")]
    [InlineData(" WhatsApp ", "whatsapp")]
    public void Resolve_ReturnsExpectedPlatformId(string platformId, string expectedPlatformId)
    {
        var adapter = PlatformAdapterFactory.Resolve(platformId);

        Assert.Equal(expectedPlatformId, adapter.PlatformId);
    }
}

public class AdapterAssetTests
{
    public static TheoryData<string, string> AdapterScripts => new()
    {
        { "whatsapp", "whatsapp-adapter.js" },
        { "telegram", "telegram-adapter.js" },
        { "messenger", "messenger-adapter.js" },
        { "slack", "slack-adapter.js" },
        { "discord", "discord-adapter.js" },
        { "signal", "signal-adapter.js" },
        { "teams", "teams-adapter.js" },
        { "metabusiness", "meta_business_scraper.js" },
        { "googlebusiness", "google_business_scraper.js" }
    };

    [Theory]
    [MemberData(nameof(AdapterScripts))]
    public void AdapterScriptExistsInOutput(string platformId, string scriptFileName)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", scriptFileName);
        Assert.True(File.Exists(scriptPath), $"Missing adapter script for {platformId}: {scriptPath}");
    }

    [Theory]
    [InlineData("whatsapp")]
    [InlineData("telegram")]
    [InlineData("messenger")]
    [InlineData("slack")]
    [InlineData("discord")]
    [InlineData("signal")]
    [InlineData("teams")]
    [InlineData("metabusiness")]
    [InlineData("googlebusiness")]
    public void ChromeStylesExistInOutput(string platformId)
    {
        var cssPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Styles", $"{platformId}-chrome.css");
        Assert.True(File.Exists(cssPath), $"Missing chrome CSS for {platformId}: {cssPath}");
    }
}
