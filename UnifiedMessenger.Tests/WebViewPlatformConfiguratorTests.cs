using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class WebViewPlatformConfiguratorTests
{
    [Fact]
    public void ChromeDesktopUserAgent_DoesNotExposeWebViewMarker()
    {
        Assert.DoesNotContain("WebView", WebViewPlatformConfigurator.ChromeDesktopUserAgent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Edg/", WebViewPlatformConfigurator.ChromeDesktopUserAgent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Chrome/", WebViewPlatformConfigurator.ChromeDesktopUserAgent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://discord.com/app", true)]
    [InlineData("https://discord.com/login", true)]
    [InlineData("https://status.discord.com/", true)]
    [InlineData("https://accounts.google.com/o/oauth2/v2/auth", false)]
    [InlineData("https://teams.microsoft.com/", false)]
    [InlineData("file:///secret", false)]
    public void IsValidDiscordStartUrl_ValidatesDiscordHosts(string url, bool expected) =>
        Assert.Equal(expected, WebViewPlatformConfigurator.IsValidDiscordStartUrl(url));

    [Theory]
    [InlineData("discord.com", true)]
    [InlineData("cdn.discordapp.com", true)]
    [InlineData("example.com", false)]
    public void IsDiscordNavigationHost_MatchesDiscordDomains(string host, bool expected) =>
        Assert.Equal(expected, WebViewPlatformConfigurator.IsDiscordNavigationHost(host));
}
