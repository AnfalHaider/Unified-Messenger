using UnifiedMessenger.Services;
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
    /// `EnableLazyWebViewLoading` used to force `StartupWarmMode.Lazy` outright, which made the entire
    /// "Which accounts open at startup" dropdown inert — the owner could pick "Every account" and nothing
    /// changed, because a toggle further down the page overrode the choice before it was read. The two
    /// settings now divide the job: the dropdown decides *whether*, the toggle decides *which*.
    /// </summary>
    [Fact]
    public void TheToggleNoLongerOverridesTheDropdown()
    {
        var settings = new AppSettings
        {
            EnableLazyWebViewLoading = true,
            StartupWarmMode = StartupWarmMode.WarmAll
        };

        Assert.Equal(StartupWarmMode.WarmAll, InstanceSessionManager.ResolveWarmMode(settings));
        Assert.Equal(Accounts.Length, ShellController.StartupWarmCount(Accounts, "acct-b", settings));
    }
}

/// <summary>
/// Which accounts the background warm brings up. Professional accounts are the ones oversight scans, and
/// the ones the idle reaper already refuses to close — so they are the set that has to be live for the
/// dashboard's numbers to move without the owner opening each account by hand.
/// </summary>
public class StartupWarmSelectionTests
{
    private static MessengerInstance Work(string id) =>
        new() { Id = id, DisplayName = id, Platform = "whatsapp", Category = WorkspaceCategory.Professional };

    private static MessengerInstance Personal(string id) =>
        new() { Id = id, DisplayName = id, Platform = "whatsapp", Category = WorkspaceCategory.Personal };

    [Fact]
    public void WorkAccountsAreBroughtUpByDefault()
    {
        var settings = new AppSettings { EnableLazyWebViewLoading = true, StartupWarmMode = StartupWarmMode.VisibleOnly };

        Assert.True(InstanceSessionManager.ShouldWarmAtStartup(Work("w"), settings));
    }

    [Fact]
    public void PersonalAccountsWaitUntilOpened()
    {
        var settings = new AppSettings { EnableLazyWebViewLoading = true, StartupWarmMode = StartupWarmMode.VisibleOnly };

        Assert.False(InstanceSessionManager.ShouldWarmAtStartup(Personal("p"), settings));
    }

    [Fact]
    public void TurningTheToggleOffStartsPersonalAccountsToo()
    {
        var settings = new AppSettings { EnableLazyWebViewLoading = false, StartupWarmMode = StartupWarmMode.VisibleOnly };

        Assert.True(InstanceSessionManager.ShouldWarmAtStartup(Personal("p"), settings));
    }

    [Fact]
    public void EveryAccountMeansEveryAccount()
    {
        var settings = new AppSettings { EnableLazyWebViewLoading = true, StartupWarmMode = StartupWarmMode.WarmAll };

        Assert.True(InstanceSessionManager.ShouldWarmAtStartup(Work("w"), settings));
        Assert.True(InstanceSessionManager.ShouldWarmAtStartup(Personal("p"), settings));
    }

    /// <summary>The one mode that must still bring up nothing on its own.</summary>
    [Fact]
    public void NoneMeansNone()
    {
        var settings = new AppSettings { EnableLazyWebViewLoading = false, StartupWarmMode = StartupWarmMode.Lazy };

        Assert.False(InstanceSessionManager.ShouldWarmAtStartup(Work("w"), settings));
        Assert.False(InstanceSessionManager.ShouldWarmAtStartup(Personal("p"), settings));
    }

    /// <summary>
    /// The owner has six professional accounts and the session cap defaults to six, so the default
    /// configuration fits exactly — no eviction, and nothing thrashes. If either number moves, the warm
    /// starts evicting the accounts it just brought up.
    /// </summary>
    [Fact]
    public void TheDefaultSessionCapFitsTheAccountsTheDefaultWarmBringsUp()
    {
        var settings = new AppSettings { EnableLazyWebViewLoading = true, StartupWarmMode = StartupWarmMode.VisibleOnly };
        var accounts = Enumerable.Range(0, 6).Select(i => Work($"w{i}")).ToList();

        var warmed = accounts.Count(a => InstanceSessionManager.ShouldWarmAtStartup(a, settings));

        Assert.True(
            warmed <= AppSettingsService.CreateDefaultSettings().MaxConcurrentWebViews,
            $"The default warm brings up {warmed} accounts against a cap of "
            + $"{AppSettingsService.CreateDefaultSettings().MaxConcurrentWebViews}; the cap would evict them.");
    }
}
