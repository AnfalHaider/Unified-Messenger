using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Offline behaviour — what the product does when the machine has no internet.
///
/// <para>
/// This matters more here than in most apps: every input the product has is a web client. The handoff
/// listed it as the most conspicuous gap in the state matrix, with four questions — does the app stay
/// responsive, do accounts report "can't read" or something alarming, does the auto-updater fail quietly,
/// and does anything hang on a network timeout.
/// </para>
/// <para>
/// The answers are in <c>docs/audit/findings/offline.md</c>. These tests pin the ones that are decidable
/// without a network, which is most of them: the user-facing strings, and the update integrity gate.
/// </para>
/// </summary>
public class OfflineBehaviourTests
{
    // ---- The update integrity gate ------------------------------------------------------------------

    /// <summary>
    /// An unsigned PE from this build — the same shape as the released installer, which was separately
    /// confirmed unsigned with <c>Get-AuthenticodeSignature</c> (Status: NotSigned) and has no signing
    /// step in either <c>installer.iss</c> or the CI workflow.
    /// </summary>
    private static string UnsignedBinaryPath => typeof(LocalDayBoundary).Assembly.Location;

    [Fact]
    public void TheBuildProducesAnUnsignedBinary()
    {
        // The premise the whole finding rests on, kept executable. Nothing in this repository signs
        // anything — no SignTool directive in installer.iss, no signing step in the CI workflow — and
        // Get-AuthenticodeSignature on the built installer reports NotSigned. Before the fix that made
        // the update gate unsatisfiable: supplied with a correct digest, it still answered "Downloaded
        // installer is not Authenticode-signed."
        //
        // If this ever starts failing, someone has added code signing. That is good news, and the right
        // response is to make Authenticode mandatory again in InstallerIntegrityVerifier.
        var signedAndTrusted = InstallerIntegrityVerifier.TryVerifyDownloadedInstaller(
            UnsignedBinaryPath,
            expectedSha256: InstallerIntegrityVerifier.ComputeSha256Hex(UnsignedBinaryPath),
            out _);

        // Accepted now, but only on the digest — assert the signature itself is genuinely absent.
        Assert.True(signedAndTrusted);
        Assert.False(
            InstallerIntegrityVerifier.TryVerifyDownloadedInstaller(UnsignedBinaryPath, null, out _),
            "an unsigned binary with no digest must not verify");
    }

    [Fact]
    public void TheRejectionMessageDoesNotSpeakAuthenticodeAtTheCustomer()
    {
        // "Downloaded installer is not Authenticode-signed" is a correct sentence addressed to the wrong
        // person. The reader is a salon owner whose only available action is to retry.
        InstallerIntegrityVerifier.TryVerifyDownloadedInstaller(UnsignedBinaryPath, null, out var error);

        Assert.NotNull(error);
        Assert.DoesNotContain("Authenticode", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("could not be verified", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AVerifiedSha256AdmitsAnUnsignedInstallerSoUpdatesCanActuallyApply()
    {
        // The fix. The digest is published beside the asset and fetched over HTTPS from the same origin,
        // so it is a real integrity check — it just is not the same check as Authenticode, and the
        // findings doc says so plainly.
        var sha = InstallerIntegrityVerifier.ComputeSha256Hex(UnsignedBinaryPath);

        var accepted = InstallerIntegrityVerifier.TryVerifyDownloadedInstaller(
            UnsignedBinaryPath, sha, out var error);

        Assert.True(accepted, error);
        Assert.Null(error);
    }

    [Fact]
    public void AWrongSha256IsStillRejected()
    {
        var accepted = InstallerIntegrityVerifier.TryVerifyDownloadedInstaller(
            UnsignedBinaryPath,
            expectedSha256: new string('a', 64),
            out var error);

        Assert.False(accepted);
        Assert.Contains("SHA-256", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NeitherASignatureNorADigestFailsClosed()
    {
        // The one thing that must never happen: accepting an installer on no evidence at all. A dropped
        // connection is exactly how the sidecar goes missing, so this is an offline case, not a
        // hypothetical one.
        var accepted = InstallerIntegrityVerifier.TryVerifyDownloadedInstaller(
            UnsignedBinaryPath, expectedSha256: null, out _);

        Assert.False(accepted);
    }

    [Fact]
    public void AMissingDownloadIsRejectedRatherThanTreatedAsVerified()
    {
        var accepted = InstallerIntegrityVerifier.TryVerifyDownloadedInstaller(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".exe"),
            expectedSha256: null,
            out var error);

        Assert.False(accepted);
        Assert.Contains("not found", error!, StringComparison.OrdinalIgnoreCase);
    }

    // ---- What the user is told when the network is down ---------------------------------------------

    [Theory]
    // The real messages .NET produces when there is no network, verbatim.
    [InlineData("No such host is known. (api.github.com:443)")]
    [InlineData("A socket operation was attempted to an unreachable network. (api.github.com:443)")]
    [InlineData("The SSL connection could not be established, see inner exception.")]
    [InlineData("A task was canceled.")]
    public void AnOfflineUpdateCheckExplainsItselfInsteadOfQuotingWinsock(string exceptionMessage)
    {
        // Before: the dialog printed ex.Message straight through, so the owner clicking "Check for
        // updates" on a dropped connection read "No such host is known. (api.github.com:443)". That is a
        // developer's diagnostic, not an answer, and it does not say the one thing that would help.
        var result = new UpdateCheckResult(
            UpdateCheckStatus.Failed,
            new Version(4, 99, 21),
            ErrorMessage: exceptionMessage);

        var message = SettingsPageHelper.BuildUpdateCheckMessage(result);

        Assert.DoesNotContain("api.github.com:443", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("socket", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SSL", message, StringComparison.Ordinal);
        Assert.DoesNotContain("task was canceled", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("internet connection", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnUpdateFailureThatIsNotANetworkProblemStillSaysWhatWentWrong()
    {
        // The offline wording must not swallow genuinely different failures — a corrupt release or a
        // missing asset still needs to say so, or the fix trades one unhelpful message for another.
        var result = new UpdateCheckResult(
            UpdateCheckStatus.Failed,
            new Version(4, 99, 21),
            ErrorMessage: "Release v5.0.0 is missing installer asset 'UnifiedMessengerSetup.exe'.");

        var message = SettingsPageHelper.BuildUpdateCheckMessage(result);

        Assert.Contains("missing installer asset", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheUpToDateAndAvailableMessagesAreUnchanged()
    {
        // Control: the offline wording must only touch the failure branch.
        Assert.Contains(
            "latest version",
            SettingsPageHelper.BuildUpdateCheckMessage(
                new UpdateCheckResult(UpdateCheckStatus.UpToDate, new Version(4, 99, 21))),
            StringComparison.OrdinalIgnoreCase);

        Assert.Contains(
            "is available",
            SettingsPageHelper.BuildUpdateCheckMessage(
                new UpdateCheckResult(
                    UpdateCheckStatus.UpdateAvailable, new Version(4, 99, 21), new Version(5, 0, 0))),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheNoReleaseMessageDoesNotHandTheCustomerTheDevelopersToDoList()
    {
        // Shipped text read: "Publish a GitHub release with asset 'UnifiedMessengerSetup.exe', or verify
        // the token in UNIFIED_MESSENGER_GITHUB_TOKEN." That is an instruction to the person who builds
        // the product, rendered in a dialog to the person who bought it.
        foreach (var tokenConfigured in new[] { true, false })
        {
            var message = GitHubUpdateService.DescribeUnavailableReleaseSource(
                "AnfalHaider", "Unified-Messenger", tokenConfigured);

            Assert.DoesNotContain("UNIFIED_MESSENGER_GITHUB_TOKEN", message, StringComparison.Ordinal);
            Assert.DoesNotContain("Publish a GitHub release", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("attach", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(".exe", message, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---- What an account looks like when the network is down ----------------------------------------

    private static InstanceResourceTile Tile() =>
        new()
        {
            InstanceId = "acct-1",
            DisplayName = "Depilex F-11 WhatsApp",
            Platform = "whatsapp",
            IsVisible = false,
            UnreadCount = 0,
            HealthState = AdapterHealthState.Stale,
            AccentColor = "#25D366",
            IconGlyph = ""
        };

    [Theory]
    // CoreWebView2WebErrorStatus names, exactly as ToString() produces them offline.
    [InlineData("HostNameNotResolved")]
    [InlineData("Disconnected")]
    [InlineData("ConnectionAborted")]
    [InlineData("ServerUnreachable")]
    [InlineData("Timeout")]
    public void AnAccountThatCannotReachTheInternetSaysSoInEnglish(string webErrorStatus)
    {
        // Before: the raw WebView2 enum name was appended to the tile's detail line, so an owner whose
        // wifi had dropped saw their WhatsApp account labelled "HostNameNotResolved". It is alarming,
        // unexplained, and points at nothing they can act on.
        var line = DashboardPageHelper.BuildPersonalTileDetailLine(
            Tile(), InstanceConnectionStatus.Error, notificationsMuted: false, connectionDetail: webErrorStatus);

        Assert.DoesNotContain(webErrorStatus, line, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("internet", line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ANonNetworkConnectionDetailIsStillShown()
    {
        // Control: only the network error codes get translated. A real, already-readable detail must
        // survive, or the fix hides information instead of clarifying it.
        var line = DashboardPageHelper.BuildPersonalTileDetailLine(
            Tile(),
            InstanceConnectionStatus.Error,
            notificationsMuted: false,
            connectionDetail: "Session failed to start");

        Assert.Contains("Session failed to start", line, StringComparison.Ordinal);
    }

    [Fact]
    public void AHealthyAccountsDetailLineIsUnaffected()
    {
        var line = DashboardPageHelper.BuildPersonalTileDetailLine(
            Tile(), InstanceConnectionStatus.Connected, notificationsMuted: false, connectionDetail: null);

        Assert.DoesNotContain("internet", line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MutedStillWinsOverEverything()
    {
        var line = DashboardPageHelper.BuildPersonalTileDetailLine(
            Tile(), InstanceConnectionStatus.Error, notificationsMuted: true, connectionDetail: "HostNameNotResolved");

        Assert.Equal("Notifications muted", line);
    }
}
