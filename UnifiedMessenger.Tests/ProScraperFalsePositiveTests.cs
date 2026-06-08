namespace UnifiedMessenger.Tests;

/// <summary>
/// Guards Meta/Google scraper scripts against regressions that reintroduce inflated DOM counts.
/// </summary>
public class ProScraperFalsePositiveTests
{
    [Fact]
    public void MetaBusinessScraper_UsesConservativeUnreadResolution()
    {
        var script = ReadScript("meta_business_scraper.js");

        Assert.Contains("resolveUnreadCountResult", script, StringComparison.Ordinal);
        Assert.Contains("countFromBizInbox", script, StringComparison.Ordinal);
        Assert.Contains("countFromInboxNav", script, StringComparison.Ordinal);
        Assert.Contains("isInboxContext", script, StringComparison.Ordinal);
        Assert.Contains("INBOUND_SIGNAL_COOLDOWN_MS", script, StringComparison.Ordinal);
        Assert.Contains("resolved.trusted", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Math.max(titleCount, navCount, bodyCount", script, StringComparison.Ordinal);
        Assert.DoesNotContain("countFromNavigation", script, StringComparison.Ordinal);
        Assert.DoesNotContain("__umFindTextMatch", script, StringComparison.Ordinal);
    }

    [Fact]
    public void MetaBusinessScraper_ScopesTelemetryToInboxRoot()
    {
        var script = ReadScript("meta_business_scraper.js");

        Assert.Contains("getTelemetryRoot", script, StringComparison.Ordinal);
        Assert.Contains("isTelemetryNoise", script, StringComparison.Ordinal);
        Assert.Contains("data-pagelet=\"BizInbox\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public void GoogleBusinessScraper_ScopesCountsToMainAndRejectsNoise()
    {
        var script = ReadScript("google_business_scraper.js");

        Assert.Contains("getScrapeRoot", script, StringComparison.Ordinal);
        Assert.Contains("isExcludedAriaLabel", script, StringComparison.Ordinal);
        Assert.Contains("parseUnrepliedFromText", script, StringComparison.Ordinal);
        Assert.Contains("isUnrepliedReviewNode", script, StringComparison.Ordinal);
        Assert.DoesNotContain("walkTextNodes(document.body", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Aggregate rating", script, StringComparison.Ordinal);
        Assert.DoesNotContain("var generic = label.match(/(\\d+)/)", script, StringComparison.Ordinal);
    }

    [Fact]
    public void GoogleBusinessScraper_ViewContextDoesNotDependOnScrapeCounts()
    {
        var script = ReadScript("google_business_scraper.js");

        var deepDataStart = script.IndexOf("function isDeepDataView", StringComparison.Ordinal);
        Assert.True(deepDataStart >= 0);
        var deepDataEnd = script.IndexOf("function resolveViewContext", deepDataStart, StringComparison.Ordinal);
        Assert.True(deepDataEnd > deepDataStart);
        var deepDataBlock = script[deepDataStart..deepDataEnd];

        Assert.DoesNotContain("scanUnrepliedCounts", deepDataBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("scanReviewAlerts", deepDataBlock, StringComparison.Ordinal);
        Assert.Contains("getScrapeRoot", deepDataBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void MetaDecoyFixture_DocumentsInboxScopedPatterns()
    {
        var fixture = ReadFixture("meta-business-inbox-decoys.html");
        var script = ReadScript("meta_business_scraper.js");

        Assert.Contains("data-pagelet=\"BizInbox\"", fixture, StringComparison.Ordinal);
        Assert.Contains("notifications", fixture, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("countFromBizInbox", script, StringComparison.Ordinal);
    }

    [Fact]
    public void GoogleDecoyFixture_DocumentsMainScopedPatterns()
    {
        var fixture = ReadFixture("google-business-false-positive-decoys.html");
        var script = ReadScript("google_business_scraper.js");

        Assert.Contains("role=\"main\"", fixture, StringComparison.Ordinal);
        Assert.Contains("total reviews", fixture, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("getScrapeRoot", script, StringComparison.Ordinal);
    }

    private static string ReadScript(string scriptFileName)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", scriptFileName);
        Assert.True(File.Exists(scriptPath), $"Missing adapter script: {scriptPath}");
        return File.ReadAllText(scriptPath);
    }

    private static string ReadFixture(string fixtureFileName)
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName);
        Assert.True(File.Exists(fixturePath), $"Missing fixture: {fixturePath}");
        return File.ReadAllText(fixturePath);
    }
}
