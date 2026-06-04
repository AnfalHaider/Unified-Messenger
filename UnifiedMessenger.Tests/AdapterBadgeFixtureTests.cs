namespace UnifiedMessenger.Tests;

public class AdapterBadgeFixtureTests
{
    public static TheoryData<string, string, string[]> AdapterFixtures => new()
    {
        {
            "whatsapp-adapter.js",
            "whatsapp-badge.html",
            ["aria-label", "unread messages", "icon-unread-count"]
        },
        {
            "telegram-adapter.js",
            "telegram-badge.html",
            ["dialog-subtitle-badge", "ChatBadge", "unread"]
        },
        {
            "slack-adapter.js",
            "slack-badge.html",
            ["unread_count", "p-channel_sidebar__badge"]
        },
        {
            "google_business_scraper.js",
            "google-business-reviews.html",
            ["review", "unreplied", "aria-label"]
        }
    };

    [Theory]
    [MemberData(nameof(AdapterFixtures))]
    public void AdapterScript_SelectorsExistInFixtureHtml(
        string scriptFileName,
        string fixtureFileName,
        string[] expectedFragments)
    {
        var script = ReadAdapterScript(scriptFileName);
        var fixture = ReadFixture(fixtureFileName);

        foreach (var fragment in expectedFragments)
        {
            Assert.True(
                script.Contains(fragment, StringComparison.OrdinalIgnoreCase) ||
                fixture.Contains(fragment, StringComparison.OrdinalIgnoreCase),
                $"Expected '{fragment}' in script '{scriptFileName}' or fixture '{fixtureFileName}'.");
        }
    }

    [Fact]
    public void AdapterCoreScript_ExposesBadgePublishHook()
    {
        var script = ReadAdapterScript("adapter-core.js");

        Assert.Contains("__unifiedMessengerPublishBadge", script, StringComparison.Ordinal);
        Assert.Contains("__umPostMessage", script, StringComparison.Ordinal);
    }

    private static string ReadAdapterScript(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", fileName);
        return File.ReadAllText(path);
    }

    private static string ReadFixture(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        return File.ReadAllText(path);
    }
}
