using UnifiedMessenger.Tests.WebView2;

namespace UnifiedMessenger.Tests;

[Collection("WebView2")]
public sealed class HeadlessAdapterIntegrationTests
{
    [SkippableFact]
    public Task WhatsAppAdapter_PostsBadgeCount_FromFixtureHtml() =>
        StaThreadRunner.RunAsync(() =>
        {
            using var harness = HeadlessWebView2Harness.Create();
            var message = harness.RunAdapterBadgeTest(
                adapterScriptFileName: "whatsapp-adapter.js",
                fixtureFileName: "whatsapp-badge.html",
                instanceId: "test-whatsapp",
                platform: "whatsapp",
                expectedCount: 6);

            Assert.Equal("badge-count", message.Type);
            Assert.Equal("test-whatsapp", message.InstanceId);
            Assert.Equal("whatsapp", message.Platform);
            Assert.Equal(6, message.Count);
        });

    [SkippableFact]
    public Task SlackAdapter_PostsBadgeCount_FromFixtureHtml() =>
        StaThreadRunner.RunAsync(() =>
        {
            using var harness = HeadlessWebView2Harness.Create();
            var message = harness.RunAdapterBadgeTest(
                adapterScriptFileName: "slack-adapter.js",
                fixtureFileName: "slack-badge.html",
                instanceId: "test-slack",
                platform: "slack",
                expectedCount: 4);

            Assert.Equal("badge-count", message.Type);
            Assert.Equal("test-slack", message.InstanceId);
            Assert.Equal("slack", message.Platform);
            Assert.Equal(4, message.Count);
        });

    [SkippableFact]
    public Task WhatsAppAdapter_DomScrapeFallback_MatchesFixtureBadgeTotal() =>
        StaThreadRunner.RunAsync(() =>
        {
            using var harness = HeadlessWebView2Harness.Create();
            harness.RunAdapterBadgeTest(
                adapterScriptFileName: "whatsapp-adapter.js",
                fixtureFileName: "whatsapp-badge.html",
                instanceId: "test-whatsapp-dom",
                platform: "whatsapp",
                expectedCount: 6);

            var domCount = harness.TryScrapeDomBadgeCount();
            Assert.Equal(6, domCount);
        });
}

[CollectionDefinition("WebView2", DisableParallelization = true)]
public sealed class WebView2TestCollection;
