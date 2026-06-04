namespace UnifiedMessenger.Tests;

public class ProAdapterScriptTests
{
    public static TheoryData<string> ProAdapters => new()
    {
        "meta_business_scraper.js",
        "google_business_scraper.js",
        "generic-adapter.js"
    };

    [Theory]
    [MemberData(nameof(ProAdapters))]
    public void ProAdapter_RegistersDisposeHook(string scriptFileName)
    {
        var script = ReadScript(scriptFileName);

        Assert.Contains("__umAdapterDispose", script, StringComparison.Ordinal);
        Assert.Contains("__umRegisterDisposable", script, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ProAdapters))]
    public void ProAdapter_DebouncesPublishUpdates(string scriptFileName)
    {
        var script = ReadScript(scriptFileName);

        Assert.True(
            script.Contains("schedulePublish", StringComparison.Ordinal) ||
            script.Contains("schedulePublishBadgeCount", StringComparison.Ordinal),
            $"Expected debounced publish helper in {scriptFileName}");
    }

    [Fact]
    public void MetaBusinessScraper_EmitsInboundMessageType()
    {
        var script = ReadScript("meta_business_scraper.js");

        Assert.Contains("meta-inbound-message", script, StringComparison.Ordinal);
        Assert.Contains("meta-telemetry-snapshot", script, StringComparison.Ordinal);
        Assert.Contains("__umRunSafeScrape", script, StringComparison.Ordinal);
        Assert.Contains("__umForceDashboardScrape", script, StringComparison.Ordinal);
        Assert.Contains("__umShouldEmitPreview", script, StringComparison.Ordinal);
        Assert.Contains("disconnectObservers", script, StringComparison.Ordinal);
    }

    [Fact]
    public void GoogleBusinessScraper_DeduplicatesReviewAlerts()
    {
        var script = ReadScript("google_business_scraper.js");

        Assert.Contains("google-review-alert", script, StringComparison.Ordinal);
        Assert.Contains("google-review-snapshot", script, StringComparison.Ordinal);
        Assert.Contains("rememberReviewKey", script, StringComparison.Ordinal);
        Assert.Contains("maxKnownReviewKeys", script, StringComparison.Ordinal);
        Assert.Contains("__umSubmitReviewReply", script, StringComparison.Ordinal);
        Assert.DoesNotContain("submit.click()", script, StringComparison.Ordinal);
        Assert.Contains("__umRunSafeScrape", script, StringComparison.Ordinal);
        Assert.Contains("__umForceDashboardScrape", script, StringComparison.Ordinal);
    }

    [Fact]
    public void GoogleBusinessScraper_RoutesLocationsDirectoryViewContext()
    {
        var script = ReadScript("google_business_scraper.js");

        Assert.Contains("locations-directory", script, StringComparison.Ordinal);
        Assert.Contains("google-view-context", script, StringComparison.Ordinal);
        Assert.Contains("ensureScrapeViewContext", script, StringComparison.Ordinal);
        Assert.Contains("NAVIGATION_COOLDOWN_MS = 12000", script, StringComparison.Ordinal);
        Assert.Contains("Connected · awaiting view context", script, StringComparison.Ordinal);
        Assert.Contains("tryNavigateFromLocationsDirectory", script, StringComparison.Ordinal);
    }

    [Fact]
    public void AdapterCore_ProvidesSafeScrapeBridge()
    {
        var script = ReadScript("adapter-core.js");

        Assert.Contains("__umPublishDashboardScrapeStatus", script, StringComparison.Ordinal);
        Assert.Contains("__umRunSafeScrape", script, StringComparison.Ordinal);
        Assert.Contains("dashboard-scrape-status", script, StringComparison.Ordinal);
    }

    [Fact]
    public void GenericAdapter_UsesTitleFallbackAndSpaHooks()
    {
        var script = ReadScript("generic-adapter.js");

        Assert.Contains("__umCountFromTitle", script, StringComparison.Ordinal);
        Assert.Contains("hookSpaNavigation", script, StringComparison.Ordinal);
        Assert.Contains("observeTitle", script, StringComparison.Ordinal);
    }

    private static string ReadScript(string scriptFileName)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", scriptFileName);
        Assert.True(File.Exists(scriptPath), $"Missing adapter script: {scriptPath}");
        return File.ReadAllText(scriptPath);
    }
}
