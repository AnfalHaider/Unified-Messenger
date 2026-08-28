using System.Text.RegularExpressions;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// F-OFFLINE-08 and F-SNAP-02 — two things the app knew and did not say on screen.
///
/// <para>
/// <b>F-OFFLINE-08.</b> When an account's numbers went cold the dashboard said "out of date — click
/// Re-sync", in the card, its tooltip and its accessible name. Re-sync reloads the account's page, which
/// cannot succeed with no connection, so the single instruction the owner was given was the one thing that
/// could not work — and it read as though the staleness were something they had failed to do. The join
/// that tells "not loaded yet" from "no internet" was already being made, correctly, in <c>app.log</c> by
/// <see cref="ScanBlockedMessage"/> (that was F-OFFLINE-06). It had simply never reached the screen, which
/// is the surface the owner actually reads.
/// </para>
/// <para>
/// <b>F-SNAP-02.</b> A store-bridge failure falls soft to the IndexedDB reader, so metrics keep flowing and
/// nothing looks wrong. That reader cannot read WhatsApp's <c>callOutcome</c>, so an answered inbound call
/// stays counted as missed. Settings named the live reader; the account card the owner looks at did not.
/// </para>
/// </summary>
public class OfflineAdviceOnScreenTests
{
    /// <summary>The WebView status string that means "this machine cannot reach the network".</summary>
    private const string OfflineDetail = "ConnectionAborted";

    private static string PanelSource(string fileName) =>
        File.ReadAllText(Path.Combine(WcagContrast.RepoRoot(), "UnifiedMessenger", "Controls", fileName));

    // ---- The join itself ------------------------------------------------------------------------------

    [Fact]
    public void AnAccountWhoseNavigationFailedForConnectivityReadsAsOffline()
    {
        var id = $"offline-{Guid.NewGuid():N}";
        InstanceConnectionStatusService.Instance.SetError(id, OfflineDetail);

        Assert.True(OfflineState.IsOffline(id));
    }

    [Fact]
    public void AConnectedAccountDoesNotReadAsOffline()
    {
        var id = $"online-{Guid.NewGuid():N}";
        InstanceConnectionStatusService.Instance.SetConnected(id);

        Assert.False(OfflineState.IsOffline(id));
    }

    /// <summary>
    /// A logged-out account is not an offline one. Conflating them would put "no connection" on a card
    /// whose real problem is that nobody has scanned the QR code, which is worse advice than the bug.
    /// </summary>
    [Fact]
    public void ALoggedOutAccountDoesNotReadAsOffline()
    {
        var id = $"loggedout-{Guid.NewGuid():N}";
        InstanceConnectionStatusService.Instance.SetLoggedOut(id);

        Assert.False(OfflineState.IsOffline(id));
    }

    [Fact]
    public void AnUnknownAccountDoesNotReadAsOffline()
    {
        Assert.False(OfflineState.IsOffline($"never-seen-{Guid.NewGuid():N}"));
        Assert.False(OfflineState.IsOffline(null));
        Assert.False(OfflineState.IsOffline("   "));
    }

    /// <summary>
    /// Any, not all. A location card covers several accounts, and one unreachable member is already enough
    /// to make "click Re-sync" the wrong thing to say about that card.
    /// </summary>
    [Fact]
    public void OneOfflineMemberIsEnoughForALocationCard()
    {
        var healthy = $"online-{Guid.NewGuid():N}";
        var broken = $"offline-{Guid.NewGuid():N}";
        InstanceConnectionStatusService.Instance.SetConnected(healthy);
        InstanceConnectionStatusService.Instance.SetError(broken, OfflineDetail);

        Assert.True(OfflineState.AnyOffline([healthy, broken]));
        Assert.False(OfflineState.AnyOffline([healthy]));
        Assert.False(OfflineState.AnyOffline([]));
        Assert.False(OfflineState.AnyOffline(null));
    }

    /// <summary>
    /// The screen and the log must not disagree about whether the machine is online. Two surfaces giving
    /// different answers to that question would be its own defect, so both go through the same predicate.
    /// </summary>
    [Fact]
    public void TheScreenAgreesWithTheLogAboutBeingOffline()
    {
        var id = $"agree-{Guid.NewGuid():N}";
        InstanceConnectionStatusService.Instance.SetError(id, OfflineDetail);

        var logSaysOffline = ScanBlockedMessage.LooksOffline(
            InstanceConnectionStatusService.Instance.GetStatus(id),
            InstanceConnectionStatusService.Instance.GetDetail(id),
            ReconnectState.None);

        Assert.Equal(logSaysOffline, OfflineState.IsOffline(id));
    }

    // ---- Source guards: no new surface may ship the advice without the join ---------------------------

    /// <summary>
    /// Every user-visible "Re-sync" instruction has to sit behind an offline check. Pinned at the source
    /// level because the defect is not in any one string — it is in writing the next one without the
    /// check, which is exactly how these five sites accumulated.
    /// </summary>
    [Theory]
    [InlineData("CommandCenterPanel.xaml.cs")]
    [InlineData("ActivityPatternsPanel.xaml.cs")]
    public void EveryReSyncInstructionSitsBehindAnOfflineCheck(string fileName)
    {
        var source = PanelSource(fileName);

        Assert.Contains("OfflineState", source, StringComparison.Ordinal);

        // Every line that tells the owner to Re-sync, minus the ones that are commentary about the defect.
        var adviceLines = source
            .Split('\n')
            .Select((line, index) => (Line: line.Trim(), Number: index + 1))
            .Where(entry => Regex.IsMatch(entry.Line, @"[Cc]lick Re-sync"))
            .Where(entry => !entry.Line.StartsWith("//", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(adviceLines);

        // Each must be a branch of a conditional whose other arm is the offline wording. Checking that the
        // file's offline wording exists once per advice site is the closest structural proxy that does not
        // depend on how the ternary happens to be line-broken.
        var offlineArms = Regex.Matches(source, "no connection|cannot reach the internet").Count;

        Assert.True(
            offlineArms > 0,
            $"{fileName} tells the owner to click Re-sync but never says what to do when there is no "
            + "connection, which is when Re-sync cannot work.");
    }

    /// <summary>
    /// The card has to say when an account is on the fallback reader. Without it the only place that
    /// admits the missed-call count may be over-stated is Settings, which the owner has no reason to open.
    /// </summary>
    [Fact]
    public void TheAccountCardNamesTheFallbackReader()
    {
        var source = PanelSource("CommandCenterPanel.xaml.cs");

        Assert.Contains("StoreBridgeHealth.TryGet", source, StringComparison.Ordinal);
        Assert.Contains("fallback reader", source, StringComparison.Ordinal);
    }
}
