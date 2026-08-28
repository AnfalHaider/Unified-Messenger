using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Shell;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Regression cover for the startup warm doing nothing at all.
///
/// <para>
/// <c>ShellController.InitializeAsync</c> called <c>WarmAllSessionsAsync(instances, visibleInstanceId:
/// null)</c>, and with the shipping defaults (<c>EnableLazyWebViewLoading = true</c>) that lands in the
/// lazy branch, which warms the named account and returns. Named account null → nothing warmed. It was
/// passed null because nothing recorded which account had been open: <c>LastVisitedSection</c> stores a
/// *section*, and <c>SelectInstanceAsync</c> persisted nothing at all.
/// </para>
/// <para>
/// The consequence was not a slow start, it was no data. An account only reports <c>Connected</c> once its
/// page has loaded and the handshake has run, and <c>OversightAlertMonitor</c> skips every account that is
/// not — so the 25–90s background scan never ran for anything. Metrics accrued only for accounts the owner
/// opened by hand, and first-response time is sampled by seeing a chat waiting at one scan and answered at
/// a later one, so scanning only on a manual visit misses fast replies entirely and keeps the slow ones.
/// </para>
/// </summary>
public class StartupWarmTests
{
    private static MessengerInstance Account(string id) =>
        new() { Id = id, DisplayName = id, Platform = "whatsapp" };

    private static readonly MessengerInstance[] Accounts =
    [
        Account("acct-a"), Account("acct-b"), Account("acct-c")
    ];

    [Fact]
    public void TheRememberedAccountIsWarmed()
    {
        Assert.Equal("acct-b", ShellController.ResolveStartupWarmInstanceId(Accounts, "acct-b"));
    }

    [Fact]
    public void ARememberedAccountThatWasDeletedIsNotWarmed()
    {
        // Accounts get deleted between runs; a stale id must not become a warm target that cannot resolve.
        Assert.Null(ShellController.ResolveStartupWarmInstanceId(Accounts, "acct-gone"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NothingRememberedWarmsNothing(string? remembered)
    {
        Assert.Null(ShellController.ResolveStartupWarmInstanceId(Accounts, remembered));
    }

    [Fact]
    public void TheIdIsMatchedWithoutCaseSensitivity()
    {
        Assert.Equal("ACCT-A", ShellController.ResolveStartupWarmInstanceId(Accounts, "ACCT-A"));
    }

    /// <summary>
    /// The progress readout announced the full account count in every mode, so the shipping configuration
    /// said "starting 3 accounts" and started none.
    /// </summary>
    [Fact]
    public void TheProgressCountMatchesWhatTheLazyWarmActuallyStarts()
    {
        var lazy = new AppSettings { EnableLazyWebViewLoading = true };

        Assert.Equal(1, ShellController.StartupWarmCount(Accounts, "acct-b", lazy));
        Assert.Equal(0, ShellController.StartupWarmCount(Accounts, null, lazy));
    }

    [Fact]
    public void WarmAllStillAnnouncesEveryAccount()
    {
        var warmAll = new AppSettings
        {
            EnableLazyWebViewLoading = false,
            StartupWarmMode = StartupWarmMode.WarmAll
        };

        Assert.Equal(Accounts.Length, ShellController.StartupWarmCount(Accounts, "acct-b", warmAll));
    }

    /// <summary>
    /// Lazy loading is a separate switch from the warm mode and wins over it, which is what made this
    /// defect the default rather than an opt-in: a machine set to VisibleOnly still takes the lazy path.
    /// </summary>
    [Fact]
    public void LazyLoadingOverridesTheWarmMode()
    {
        var settings = new AppSettings
        {
            EnableLazyWebViewLoading = true,
            StartupWarmMode = StartupWarmMode.WarmAll
        };

        Assert.Equal(1, ShellController.StartupWarmCount(Accounts, "acct-b", settings));
    }
}
