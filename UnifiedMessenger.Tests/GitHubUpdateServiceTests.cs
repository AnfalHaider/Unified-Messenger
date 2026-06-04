using System.Text.Json;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class GitHubUpdateServiceTests
{
    [Theory]
    [InlineData("v1.0.4", "1.0.4")]
    [InlineData("V2.3.1", "2.3.1")]
    [InlineData("1.0.0.0", "1.0.0.0")]
    public void TryParseReleaseVersion_NormalizesTagPrefix(string tagName, string expectedVersion)
    {
        Assert.True(GitHubUpdateService.TryParseReleaseVersion(tagName, out var version));
        Assert.Equal(new Version(expectedVersion), version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    public void TryParseReleaseVersion_RejectsInvalidTags(string? tagName)
    {
        Assert.False(GitHubUpdateService.TryParseReleaseVersion(tagName, out _));
    }

    [Fact]
    public void ParseRelease_FindsSetupAssetDownloadUrl()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "tag_name": "v1.0.4",
              "assets": [
                {
                  "name": "UnifiedMessengerSetup.exe",
                  "browser_download_url": "https://example.com/UnifiedMessengerSetup.exe"
                }
              ]
            }
            """);

        var release = GitHubUpdateService.ParseRelease(document.RootElement);

        Assert.NotNull(release);
        Assert.Equal("v1.0.4", release!.TagName);
        Assert.Equal(
            "https://example.com/UnifiedMessengerSetup.exe",
            release.DownloadUrl);
    }

    [Fact]
    public void SelectFirstPublishedRelease_SkipsDraftEntries()
    {
        using var document = JsonDocument.Parse(
            """
            [
              {
                "tag_name": "v9.9.9",
                "draft": true,
                "assets": [
                  {
                    "name": "UnifiedMessengerSetup.exe",
                    "browser_download_url": "https://example.com/draft.exe"
                  }
                ]
              },
              {
                "tag_name": "v1.0.3",
                "draft": false,
                "assets": [
                  {
                    "name": "UnifiedMessengerSetup.exe",
                    "browser_download_url": "https://example.com/stable.exe"
                  }
                ]
              }
            ]
            """);

        var release = GitHubUpdateService.SelectFirstPublishedRelease(document.RootElement);

        Assert.NotNull(release);
        Assert.Equal("v1.0.3", release!.TagName);
        Assert.Equal("https://example.com/stable.exe", release.DownloadUrl);
    }

    [Theory]
    [InlineData("https://github.com/owner/repo/releases/download/v1/setup.exe", true)]
    [InlineData("http://example.com/setup.exe", false)]
    [InlineData("not-a-url", false)]
    public void IsValidDownloadUrl_RequiresHttpsAbsoluteUrl(string url, bool expected)
    {
        Assert.Equal(expected, GitHubUpdateService.IsValidDownloadUrl(url));
    }

    [Theory]
    [InlineData("1.0.3", "1.0.4", true)]
    [InlineData("1.0.4", "1.0.4", false)]
    [InlineData("1.0.5", "1.0.4", false)]
    public void IsNewerVersion_ComparesReleaseVersions(string current, string latest, bool expected)
    {
        Assert.Equal(
            expected,
            GitHubUpdateService.IsNewerVersion(new Version(current), new Version(latest)));
    }

    [Fact]
    public void DescribeUnavailableReleaseSource_ExplainsPrivateRepositoryWithoutToken()
    {
        var message = GitHubUpdateService.DescribeUnavailableReleaseSource(
            GitHubUpdateService.DefaultGitHubOwner,
            GitHubUpdateService.DefaultGitHubRepo,
            tokenConfiguredOverride: false);

        Assert.Contains("not public", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(GitHubUpdateService.SetupAssetName, message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildLatestReleaseUrl_UsesGitHubReleasesApi()
    {
        Assert.Equal(
            "https://api.github.com/repos/AnfalHaider/Unified-Messenger/releases/latest",
            GitHubUpdateService.BuildLatestReleaseUrl(
                GitHubUpdateService.DefaultGitHubOwner,
                GitHubUpdateService.DefaultGitHubRepo));
    }
}
