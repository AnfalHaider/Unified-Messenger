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
    public Task DiscordAdapter_PostsBadgeCount_FromFixtureHtml() =>
        StaThreadRunner.RunAsync(() =>
        {
            using var harness = HeadlessWebView2Harness.Create();
            var message = harness.RunAdapterBadgeTest(
                adapterScriptFileName: "discord-adapter.js",
                fixtureFileName: "discord-badge.html",
                instanceId: "test-discord",
                platform: "discord",
                expectedCount: 6);

            Assert.Equal("badge-count", message.Type);
            Assert.Equal("test-discord", message.InstanceId);
            Assert.Equal("discord", message.Platform);
            Assert.Equal(6, message.Count);
        });

    [SkippableFact]
    public Task TeamsAdapter_PostsBadgeCount_FromFixtureHtml() =>
        StaThreadRunner.RunAsync(() =>
        {
            using var harness = HeadlessWebView2Harness.Create();
            var message = harness.RunAdapterBadgeTest(
                adapterScriptFileName: "teams-adapter.js",
                fixtureFileName: "teams-badge.html",
                instanceId: "test-teams",
                platform: "teams",
                expectedCount: 15);

            Assert.Equal("badge-count", message.Type);
            Assert.Equal("test-teams", message.InstanceId);
            Assert.Equal("teams", message.Platform);
            Assert.Equal(15, message.Count);
        });

    [SkippableFact]
    public Task TelegramAdapter_PostsBadgeCount_FromFixtureHtml() =>
        StaThreadRunner.RunAsync(() =>
        {
            using var harness = HeadlessWebView2Harness.Create();
            var message = harness.RunAdapterBadgeTest(
                adapterScriptFileName: "telegram-adapter.js",
                fixtureFileName: "telegram-badge.html",
                instanceId: "test-telegram",
                platform: "telegram",
                expectedCount: 7);

            Assert.Equal("badge-count", message.Type);
            Assert.Equal("test-telegram", message.InstanceId);
            Assert.Equal("telegram", message.Platform);
            Assert.Equal(7, message.Count);
        });

    [SkippableFact]
    public Task MessengerAdapter_PostsBadgeCount_FromFixtureHtml() =>
        StaThreadRunner.RunAsync(() =>
        {
            using var harness = HeadlessWebView2Harness.Create();
            var message = harness.RunAdapterBadgeTest(
                adapterScriptFileName: "messenger-adapter.js",
                fixtureFileName: "messenger-badge.html",
                instanceId: "test-messenger",
                platform: "messenger",
                expectedCount: 5);

            Assert.Equal("badge-count", message.Type);
            Assert.Equal("test-messenger", message.InstanceId);
            Assert.Equal("messenger", message.Platform);
            Assert.Equal(5, message.Count);
        });

    [SkippableFact]
    public Task SignalAdapter_PostsBadgeCount_FromFixtureHtml() =>
        StaThreadRunner.RunAsync(() =>
        {
            using var harness = HeadlessWebView2Harness.Create();
            var message = harness.RunAdapterBadgeTest(
                adapterScriptFileName: "signal-adapter.js",
                fixtureFileName: "signal-badge.html",
                instanceId: "test-signal",
                platform: "signal",
                expectedCount: 7);

            Assert.Equal("badge-count", message.Type);
            Assert.Equal("test-signal", message.InstanceId);
            Assert.Equal("signal", message.Platform);
            Assert.Equal(7, message.Count);
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
