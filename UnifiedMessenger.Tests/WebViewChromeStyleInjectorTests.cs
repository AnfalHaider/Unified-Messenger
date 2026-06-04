using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class WebViewChromeStyleInjectorTests
{
    [Theory]
    [InlineData("whatsapp", "whatsapp-chrome.css")]
    [InlineData("slack", "slack-chrome.css")]
    [InlineData("generic", "generic-chrome.css")]
    [InlineData("not-a-platform", "generic-chrome.css")]
    public void ResolvePlatformStylesheetFileName_MapsKnownPlatforms(string platformId, string expectedFileName)
    {
        Assert.Equal(expectedFileName, WebViewChromeStyleInjector.ResolvePlatformStylesheetFileName(platformId));
    }

    [Fact]
    public async Task LoadChromeCssAsync_IncludesGenericBaseAndPlatformRules()
    {
        var css = await WebViewChromeStyleInjector.LoadChromeCssAsync("whatsapp");

        Assert.Contains("--um-shell-seam", css, StringComparison.Ordinal);
        Assert.Contains("#pane-side", css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadChromeCssAsync_UsesGenericOnlyForUnknownPlatform()
    {
        var css = await WebViewChromeStyleInjector.LoadChromeCssAsync("unknown-platform");

        Assert.Contains("--um-shell-seam", css, StringComparison.Ordinal);
        Assert.DoesNotContain("#pane-side", css, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDocumentCreatedScript_GuardsDuplicateStyleElement()
    {
        var script = WebViewChromeStyleInjector.BuildDocumentCreatedScript("body { margin: 0; }");

        Assert.Contains("unified-messenger-chrome", script, StringComparison.Ordinal);
        Assert.Contains("getElementById", script, StringComparison.Ordinal);
        Assert.DoesNotContain("__unifiedMessengerChromeInjected", script, StringComparison.Ordinal);
    }
}
