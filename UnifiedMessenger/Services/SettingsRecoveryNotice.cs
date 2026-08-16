namespace UnifiedMessenger.Services;

/// <summary>
/// What to tell the owner when their settings file could not be read and the app fell back to defaults.
///
/// <para>
/// <b>Closing F-DURA-01.</b> Since v4.99.4 the event is logged and the unreadable file is preserved as
/// <c>settings.json.corrupt-&lt;timestamp&gt;.bak</c>, and <see cref="AppSettingsService"/> exposes
/// <c>RecoveredFromCorruptFile</c> / <c>CorruptFileBackupPath</c> — but <b>nothing read either
/// property</b>. The owner of a salon does not read <c>app.log</c>, so in practice their reply-time
/// targets, business hours, notification choices and AI setting reverted to factory defaults with no
/// event they could connect it to. The finding recorded that as "mitigated, not closed"; this is the
/// missing half.
/// </para>
/// <para>
/// <b>What the copy may and may not say.</b> The file was unreadable, so the app genuinely cannot know
/// which values were lost — there is nothing to diff against. The wording therefore names the
/// <i>categories</i> of setting that live in that file and points at the preserved copy, and never
/// claims to list what changed. It also does not offer to restore automatically: the bytes it would
/// restore from are the ones that could not be parsed.
/// </para>
/// </summary>
public static class SettingsRecoveryNotice
{
    public const string Title = "Your settings were reset";

    /// <summary>Shown when a backup could not be written, so there is nothing to point the owner at.</summary>
    internal const string NoBackupLine =
        "The unreadable file could not be set aside, so there is no copy of your previous settings.";

    /// <summary>
    /// Whether to show the notice at all. Deliberately a straight read of the flag: it is set during
    /// <see cref="AppSettingsService.LoadAsync"/> and lives on the instance, so it is true only in the
    /// session where the corruption was actually found — which is exactly "once, when it happens".
    /// </summary>
    public static bool ShouldShow(bool recoveredFromCorruptFile) => recoveredFromCorruptFile;

    /// <summary>The body of the notice. <paramref name="backupPath"/> may be null when preservation failed.</summary>
    public static string BuildMessage(string? backupPath)
    {
        var lines = new List<string>
        {
            "Unified Messenger could not read your saved settings when it started, so it is running on " +
            "default settings.",

            "Anything you had changed is affected — reply-time targets and business hours for each " +
            "location, notification choices, and whether on-device AI is switched on. Your accounts and " +
            "message history are stored separately and are untouched.",

            // Called out by name because it is the one that changes what the app does without asking.
            // A silent revert here means an owner who chose to be consulted before updates stops being
            // consulted, and would have no way to know.
            "Worth checking first: whether updates install automatically, under Settings."
        };

        lines.Add(HasUsableBackup(backupPath)
            ? "Your previous settings file was not deleted. A copy was kept as:\n" + backupPath!.Trim()
            : NoBackupLine);

        return string.Join("\n\n", lines);
    }

    /// <summary>
    /// True when there is a preserved file worth offering to reveal. Checks the disk rather than trusting
    /// the recorded path: the notice appears at startup, and offering to show a file that is not there
    /// would replace one confusing moment with another.
    /// </summary>
    public static bool CanRevealBackup(string? backupPath) =>
        HasUsableBackup(backupPath) && File.Exists(backupPath!.Trim());

    private static bool HasUsableBackup(string? backupPath) => !string.IsNullOrWhiteSpace(backupPath);
}
