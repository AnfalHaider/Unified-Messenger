using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class WebViewNavigationGuardTests
{
    [Theory]
    [InlineData("https://web.whatsapp.com/", true)]
    [InlineData("https://web.telegram.org/k/", true)]
    [InlineData("https://www.messenger.com/", true)]
    [InlineData("https://app.slack.com/client", true)]
    [InlineData("https://discord.com/login", true)]
    [InlineData("https://teams.microsoft.com/v2/", true)]
    [InlineData("https://business.facebook.com/latest/inbox/", true)]
    [InlineData("https://business.google.com/locations", true)]
    [InlineData("https://accounts.google.com/o/oauth2/v2/auth", true)]
    [InlineData("https://login.microsoftonline.com/common/oauth2/v2.0/authorize", true)]
    [InlineData("https://www.facebook.com/login.php", true)]
    [InlineData("http://example.com/path", false)]
    [InlineData("https://evil.example/phish", false)]
    [InlineData("file:///C:/secret", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("", false)]
    public void IsAllowedNavigationUri_ValidatesPlatformAndOAuthHosts(string uri, bool expected) =>
        Assert.Equal(expected, WebViewNavigationGuard.IsAllowedNavigationUri(uri));

    [Theory]
    [InlineData("https://chat.mycompany.example/", true)]
    [InlineData("https://evil.example/phish", false)]
    public void IsAllowedNavigationUri_AllowsAdditionalInstanceHosts(string uri, bool expected)
    {
        var additionalHosts = new[] { "chat.mycompany.example" };

        Assert.Equal(expected, WebViewNavigationGuard.IsAllowedNavigationUri(uri, additionalHosts));
    }
}
