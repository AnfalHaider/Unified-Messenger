using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class WebViewProfileManagerTests
{
    [Theory]
    [InlineData("whatsapp-default")]
    [InlineData("slack-work-abc123")]
    [InlineData("meta-business (#1)")]
    public void TryValidateProfileName_AcceptsValidNames(string profileName)
    {
        Assert.True(WebViewProfileManager.TryValidateProfileName(profileName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad/name")]
    [InlineData("ends-with-dot.")]
    public void TryValidateProfileName_RejectsInvalidNames(string profileName)
    {
        Assert.False(WebViewProfileManager.TryValidateProfileName(profileName));
    }

    [Fact]
    public void NormalizeProfileName_TrimsWhitespace()
    {
        Assert.Equal("whatsapp-default", WebViewProfileManager.NormalizeProfileName("  whatsapp-default  "));
    }
}
