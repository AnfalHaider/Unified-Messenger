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
        Assert.Equal("Custom URL (any website)", generic!.DisplayName);
        Assert.Equal(string.Empty, generic.DefaultUrl);
        Assert.Equal("generic", PlatformDefinition.NormalizePlatformId("generic"));
    }

    [Fact]
    public void GetSelectablePlatforms_HidesTelegramMetaSuiteInstagram_KeepsTheRest()
    {
        var ids = UnifiedMessenger.Services.PlatformModuleSettingsHelper
            .GetSelectablePlatforms(new AppSettings())
            .Select(p => p.Id)
            .ToList();

        Assert.DoesNotContain("telegram", ids);
        Assert.DoesNotContain("metabusinesssuite", ids);
        Assert.DoesNotContain("instagram", ids);
        Assert.Contains("whatsapp", ids);
        Assert.Contains("googlebusiness", ids);
        Assert.Contains("messenger", ids);
        Assert.Contains("discord", ids);
        Assert.Contains("generic", ids);
    }

    [Theory]
    [InlineData("business.google.com", "google.com")]
    [InlineData("www.messenger.com", "messenger.com")]
    [InlineData("web.whatsapp.com", "whatsapp.com")]
    [InlineData("google.com", "google.com")]
    [InlineData("shop.example.co.uk", "example.co.uk")]
    public void RegistrableDomain_ReturnsETldPlusOne(string host, string expected)
    {
        Assert.Equal(expected, UnifiedMessenger.Services.WebViewNavigationGuard.RegistrableDomain(host));
    }

    [Fact]
    public void NavGuard_AllowsGoogleRedirectHostsForGoogleBusiness()
    {
        // The Google Business load bug: business.google.com redirects across other google.com hosts, which
        // the guard used to cancel. These must now be allowed.
        Assert.True(UnifiedMessenger.Services.WebViewNavigationGuard.IsAllowedNavigationUri("https://business.google.com/"));
        Assert.True(UnifiedMessenger.Services.WebViewNavigationGuard.IsAllowedNavigationUri("https://www.google.com/business/"));
        Assert.True(UnifiedMessenger.Services.WebViewNavigationGuard.IsAllowedNavigationUri("https://accounts.google.com/ServiceLogin"));
    }

    [Fact]
    public void NavGuard_AllowsHttpForCustomUrlHost()
    {
        // A custom-URL tab may be entered as http:// (or first hop through http). The guard used to accept
        // https only, leaving the tab on about:blank. Both schemes are now allowed for an allowlisted host.
        var extraHosts = new[] { "example.com" };
        Assert.True(UnifiedMessenger.Services.WebViewNavigationGuard.IsAllowedNavigationUri("http://example.com/", extraHosts));
        Assert.True(UnifiedMessenger.Services.WebViewNavigationGuard.IsAllowedNavigationUri("https://example.com/", extraHosts));
        // Non-web schemes stay blocked.
        Assert.False(UnifiedMessenger.Services.WebViewNavigationGuard.IsAllowedNavigationUri("ftp://example.com/", extraHosts));
    }

    [Fact]
    public void NavGuard_AllowAllHostsSentinel_AllowsAnyHttpHost_ButNotOtherSchemes()
    {
        // Custom-URL / generic tabs attach with the "*" sentinel so an arbitrary site's own cross-domain
        // redirects (OAuth, CDNs, apex↔www hops to another domain) aren't cancelled to a blank page.
        var wildcard = new[] { "*" };
        Assert.True(UnifiedMessenger.Services.WebViewNavigationGuard.IsAllowedNavigationUri("https://anything.example.org/x", wildcard));
        Assert.True(UnifiedMessenger.Services.WebViewNavigationGuard.IsAllowedNavigationUri("http://some-cdn.net/asset", wildcard));
        Assert.True(UnifiedMessenger.Services.WebViewNavigationGuard.IsAllowedNavigationUri("https://accounts.thirdparty.io/login", wildcard));
        // Scheme guard still holds — non-web schemes are never allowed, even with the wildcard.
        Assert.False(UnifiedMessenger.Services.WebViewNavigationGuard.IsAllowedNavigationUri("file:///C:/x", wildcard));
        Assert.False(UnifiedMessenger.Services.WebViewNavigationGuard.IsAllowedNavigationUri("ftp://host/x", wildcard));
    }
}
