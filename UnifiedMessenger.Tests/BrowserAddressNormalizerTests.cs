using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class BrowserAddressNormalizerTests
{
    [Theory]
    [InlineData("example.com", "https://example.com/")]
    [InlineData("https://example.com/path", "https://example.com/path")]
    [InlineData("http://example.com/", "http://example.com/")]
    [InlineData("  example.com/a?b=c  ", "https://example.com/a?b=c")]
    [InlineData("sub.example.co.uk", "https://sub.example.co.uk/")]
    public void Normalize_AcceptsWebAddressesAndDefaultsToHttps(string input, string expected)
    {
        var result = BrowserAddressNormalizer.Normalize(input);

        Assert.True(result.IsValid, result.Error);
        Assert.Equal(expected, result.Url);
    }

    [Fact]
    public void Normalize_AllowsAnExplicitPort()
    {
        var result = BrowserAddressNormalizer.Normalize("example.com:8080/status");

        Assert.True(result.IsValid, result.Error);
        Assert.Equal("https://example.com:8080/status", result.Url);
    }

    [Fact]
    public void Normalize_AllowsLocalhostDespiteHavingNoDot()
    {
        // A local-only app should be able to point a tab at a local dashboard.
        var result = BrowserAddressNormalizer.Normalize("localhost:3000");

        Assert.True(result.IsValid, result.Error);
    }

    [Theory]
    [InlineData("file:///C:/Windows/System32/drivers/etc/hosts")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("ftp://example.com/file")]
    [InlineData("about:config")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("vbscript:msgbox(1)")]
    public void Normalize_RefusesEverythingThatIsNotPlainWebNavigation(string input)
    {
        // The webview holds live signed-in sessions. file: would expose local disk to a page, and
        // javascript:/data: are how script gets smuggled into another origin.
        var result = BrowserAddressNormalizer.Normalize(input);

        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.Equal(string.Empty, result.Url);
    }

    [Fact]
    public void Normalize_DoesNotLetABlockedSchemeSlipThroughTheHttpsGuess()
    {
        // Regression guard: naively prefixing "https://" would turn this into a valid-looking URL.
        var result = BrowserAddressNormalizer.Normalize("file:///etc/passwd");

        Assert.False(result.IsValid);
        Assert.DoesNotContain("https://file", result.Url, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Normalize_RejectsEmptyInput(string? input)
    {
        Assert.False(BrowserAddressNormalizer.Normalize(input).IsValid);
    }

    [Theory]
    [InlineData("how do I reset my password")]
    [InlineData("notaurl")]
    public void Normalize_RejectsNonUrlTextRatherThanSearchingForIt(string input)
    {
        // Handing this to a search engine would send the owner's typing off the machine — exactly what
        // this app promises never to do. Reject with a reason instead.
        var result = BrowserAddressNormalizer.Normalize(input);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("https://www.example.com/path", "Example.com")]
    [InlineData("https://docs.example.org/", "Docs.example.org")]
    [InlineData("not a url", "Web page")]
    [InlineData("", "Web page")]
    public void SuggestDisplayName_NamesASiteAfterItsHost(string url, string expected)
    {
        Assert.Equal(expected, BrowserAddressNormalizer.SuggestDisplayName(url));
    }

    [Theory]
    [InlineData("https://example.com/a/b", "example.com/a/b")]
    [InlineData("https://example.com/", "example.com")]
    [InlineData("", "")]
    public void ToDisplayForm_DropsTheSchemeAndTrailingSlash(string url, string expected)
    {
        Assert.Equal(expected, BrowserAddressNormalizer.ToDisplayForm(url));
    }
}

public class PlatformFreeBrowsingTests
{
    [Fact]
    public void OnlyTheCustomUrlPlatformAllowsFreeBrowsing()
    {
        // Every real service tab is pinned to its own site by the navigation guard, so an address bar
        // there would only ever produce blocked navigations.
        foreach (var platform in PlatformDefinition.All)
        {
            var expected = platform.Id == "generic";
            Assert.Equal(expected, platform.AllowsCustomUrl);
        }
    }

    [Fact]
    public void AllowsFreeBrowsing_TreatsUnknownPlatformsAsPinned()
    {
        Assert.False(PlatformDefinition.AllowsFreeBrowsing("not-a-real-platform"));
        Assert.False(PlatformDefinition.AllowsFreeBrowsing(null));
        Assert.True(PlatformDefinition.AllowsFreeBrowsing("generic"));
    }
}
