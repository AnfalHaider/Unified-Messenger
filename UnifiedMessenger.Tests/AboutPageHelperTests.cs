using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class AboutPageHelperTests
{
    [Fact]
    public void BuildAboutVersionLabel_UsesDefaultWhenVersionMissing()
    {
        Assert.Equal(AboutPageHelper.DefaultVersionLabel, AboutPageHelper.BuildAboutVersionLabel(null));
    }

    [Theory]
    [InlineData("1.0.3", "Unified Messenger v1.0.3")]
    public void BuildAboutVersionLabel_PrefixesProductNameWithVersion(string versionText, string expected)
    {
        Assert.Equal(expected, AboutPageHelper.BuildAboutVersionLabel(Version.Parse(versionText)));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ShouldShowBackLink_MirrorsNavigationStackState(bool canGoBack, bool expected)
    {
        Assert.Equal(expected, AboutPageHelper.ShouldShowBackLink(canGoBack));
    }
}
