using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// F-DURA-01, closing half — the settings reset is now told to the user, not just to <c>app.log</c>.
///
/// <para>
/// The recovery plumbing has existed since v4.99.4 (log the event, preserve the unreadable file, expose
/// <c>RecoveredFromCorruptFile</c> / <c>CorruptFileBackupPath</c>) and <b>nothing read either
/// property</b>. The finding was explicit that this left it "mitigated, not closed".
/// </para>
/// <para>
/// Most of these tests are about what the notice must <b>not</b> say. The app cannot know which settings
/// were lost — the file was unreadable, so there is nothing to diff against — and a notice that implies
/// otherwise, or that offers a restore it cannot perform, would be worse than the silence it replaces.
/// </para>
/// </summary>
public class SettingsRecoveryNoticeTests
{
    private const string BackupPath = @"C:\Users\someone\AppData\Local\UnifiedMessenger\settings.json.corrupt-20260814120750.bak";

    [Fact]
    public void TheNoticeAppearsOnlyWhenARecoveryActuallyHappened()
    {
        Assert.True(SettingsRecoveryNotice.ShouldShow(true));
        Assert.False(SettingsRecoveryNotice.ShouldShow(false));
    }

    [Fact]
    public void ItSaysPlainlyThatSettingsAreBackToDefaults()
    {
        var message = SettingsRecoveryNotice.BuildMessage(BackupPath);

        Assert.Contains("could not read your saved settings", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("default settings", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ItNamesThePreservedFileSoTheOwnerCanFindIt()
    {
        var message = SettingsRecoveryNotice.BuildMessage(BackupPath);

        Assert.Contains(BackupPath, message, StringComparison.Ordinal);
        Assert.Contains("not deleted", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WhenNothingCouldBePreservedItSaysSoRatherThanPointingAtNothing()
    {
        foreach (var missing in new[] { null, "", "   " })
        {
            var message = SettingsRecoveryNotice.BuildMessage(missing);

            Assert.Contains("could not be set aside", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("A copy was kept", message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ItNeverClaimsToKnowWhichSettingsChanged()
    {
        // The file could not be parsed, so there is no before-state to compare against. Any wording that
        // implies a list — "the following settings were reset" — would be inventing information.
        var message = SettingsRecoveryNotice.BuildMessage(BackupPath);

        Assert.DoesNotContain("the following", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("these settings were", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("were changed to", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ItNeverOffersARestoreItCannotPerform()
    {
        // The only bytes available are the ones that failed to parse. Offering to restore them would be
        // a promise the app cannot keep.
        var message = SettingsRecoveryNotice.BuildMessage(BackupPath);

        Assert.DoesNotContain("restore your settings", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("click to restore", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("undo", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ItReassuresThatAccountsAndHistoryAreNotAffected()
    {
        // Without this the reasonable fear is "have I lost my conversations?" — a much bigger worry than
        // the one that actually happened. Accounts live in instances.json, history in its own stores.
        var message = SettingsRecoveryNotice.BuildMessage(BackupPath);

        Assert.Contains("accounts and message history", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("untouched", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ItCallsOutTheAutoUpdateSettingSpecifically()
    {
        // The consent-relevant one. PromptBeforeAutoUpdate reverting from true to false means an owner who
        // asked to be consulted before updates silently stops being consulted — see the F-DURA-01 table.
        var message = SettingsRecoveryNotice.BuildMessage(BackupPath);

        Assert.Contains("updates install automatically", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ItSpeaksAboutSettingsNotAboutJson()
    {
        // The reader runs a chain of salons. "Deserialize", "JsonException" and file-format vocabulary
        // belong in app.log, which already has them.
        //
        // Checked against the PROSE, not the whole message: the preserved file is genuinely called
        // settings.json.corrupt-<timestamp>.bak, and the owner needs that name verbatim to find it. A
        // blanket scan flagged the filename and would have pushed the fix towards hiding the one piece
        // of actionable detail in the notice.
        var prose = SettingsRecoveryNotice.BuildMessage(BackupPath).Replace(BackupPath, string.Empty);

        foreach (var jargon in new[] { "JSON", "deserial", "parse", "exception", "token", "null" })
        {
            Assert.DoesNotContain(jargon, prose, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void RevealIsOfferedOnlyWhenTheFileIsReallyThere()
    {
        Assert.False(SettingsRecoveryNotice.CanRevealBackup(null));
        Assert.False(SettingsRecoveryNotice.CanRevealBackup("   "));

        // A path that was recorded but whose file has since gone — the notice appears at startup and the
        // folder is user-writable, so this is not hypothetical.
        Assert.False(SettingsRecoveryNotice.CanRevealBackup(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".bak")));

        var real = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".bak");
        File.WriteAllText(real, "{ truncated");
        try
        {
            Assert.True(SettingsRecoveryNotice.CanRevealBackup(real));
        }
        finally
        {
            File.Delete(real);
        }
    }

    [Fact]
    public void TheTitleDoesNotSoundLikeACrash()
    {
        // It is a recoverable, already-handled condition. "Error", "failed" and "problem" would make a
        // handled fallback read like a fault, which is how support tickets get opened for non-events.
        foreach (var alarming in new[] { "error", "failed", "failure", "problem", "warning" })
        {
            Assert.DoesNotContain(alarming, SettingsRecoveryNotice.Title, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("settings", SettingsRecoveryNotice.Title, StringComparison.OrdinalIgnoreCase);
    }
}
