using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class WebViewNavigationGuardTests
{
    [Theory]
    [InlineData("https://web.whatsapp.com/", true)]
    [InlineData("http://example.com/path", true)]
    [InlineData("file:///C:/secret", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("", false)]
    [InlineData("https://discord.com/login", true)]
    [InlineData("https://accounts.google.com/o/oauth2/v2/auth", true)]
    [InlineData("https://teams.microsoft.com/v2/", true)]
    public void IsAllowedNavigationUri_ValidatesSchemes(string uri, bool expected) =>
        Assert.Equal(expected, WebViewNavigationGuard.IsAllowedNavigationUri(uri));
}
