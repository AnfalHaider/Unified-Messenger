using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// F-PERF-01 — the conversation scan must only run on channels that actually have a conversation scraper.
///
/// <para>
/// Found in the owner's live log: three <c>googlebusiness</c> instances repeatedly reporting
/// "Conversation scan function is not injected on this page". They were behaving exactly as designed —
/// Google is reviews + Q&amp;A only, permanently, and has no conversation scraper — but
/// <c>OversightAlertMonitor</c> selects instances on "professional and connected" alone, so the WhatsApp
/// scan ran against them every cycle.
/// </para>
/// <para>
/// Two consequences, the second worse than the first: permanent log noise that trains people to ignore
/// real warnings, and — after v4.99.8 — a recorded read failure that would render
/// "can't read this account — click Re-sync" on three healthy accounts. That is the exact false positive
/// <c>AccountReadHealth</c> was written to avoid.
/// </para>
/// </summary>
[Collection("AccountReadHealth")]
public class ScanAppliesOnlyToScrapedChannelsTests : IDisposable
{
    public ScanAppliesOnlyToScrapedChannelsTests() => AccountReadHealth.Reset();

    public void Dispose()
    {
        AccountReadHealth.Reset();
        GC.SuppressFinalize(this);
    }

    private static MessengerInstance Instance(string platform) =>
        new()
        {
            Id = $"acct-{platform}",
            DisplayName = $"{platform} account",
            ProfileName = $"acct-{platform}",
            Platform = platform,
            Category = WorkspaceCategory.Professional
        };

    [Theory]
    [InlineData("whatsapp")]
    [InlineData("whatsappbusiness")]
    public void WhatsAppFamilyChannelsParticipateInTheConversationScan(string platform)
    {
        // The positive case, so this suite cannot degrade into "nothing is ever scanned".
        Assert.True(PlatformModuleSettingsHelper.IsPlatformModuleEnabled(platform));
    }

    [Theory]
    [InlineData("googlebusiness")]  // the live case: reviews + Q&A only, no conversation scraper
    [InlineData("messenger")]
    [InlineData("discord")]
    [InlineData("generic")]
    [InlineData("telegram")]
    [InlineData("instagram")]
    [InlineData("metabusinesssuite")]
    public void ChannelsWithoutAConversationScraperAreExcluded(string platform)
    {
        Assert.False(PlatformModuleSettingsHelper.IsPlatformModuleEnabled(platform));
    }

    [Fact]
    public async Task ScanningAChannelWithoutAScraperReturnsNullAndRecordsNoFailure()
    {
        // THE regression guard. Returning null is correct; recording a failure is not — these accounts
        // are not broken, the scan simply does not apply to them.
        var google = Instance("googlebusiness");

        var result = await OversightSnapshotReader.RefreshAsync(google);

        Assert.Null(result);
        Assert.False(
            AccountReadHealth.LastReadFailed(google.Id),
            "a channel with no conversation scraper must not be reported as unreadable");
        Assert.Null(AccountReadHealth.TryGet(google.Id));
    }

    [Theory]
    [InlineData("messenger")]
    [InlineData("discord")]
    [InlineData("generic")]
    public async Task NoEmbedOnlyChannelIsEverMarkedUnreadable(string platform)
    {
        var instance = Instance(platform);

        await OversightSnapshotReader.RefreshAsync(instance);

        Assert.False(AccountReadHealth.LastReadFailed(instance.Id));
    }
}
