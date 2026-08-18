namespace UnifiedMessenger.Services;

/// <summary>
/// What to tell the owner when the app could not read their account list.
///
/// <para>
/// <b>The failure this replaces.</b> The registry treated <c>File.Exists == false</c> as "first run", and
/// <see cref="File.Exists(string)"/> returns false for a denied folder or a locked file just as readily as
/// for a missing one. An owner with nine connected accounts therefore opened the app to
/// <i>"Welcome to Unified Messenger — add an account to start receiving unified notifications"</i> and a
/// single demo account. Their data was untouched on disk the whole time, but nothing on screen said so,
/// and the most natural reading of that screen is that a business's entire message history is gone.
/// </para>
/// <para>
/// <b>What the copy has to do.</b> Lead with the reassurance, because that is the question actually being
/// asked. Say plainly that this session cannot see the data rather than that the data is missing — the app
/// knows the difference and must not blur it. Name the file, so the owner can confirm with their own eyes
/// that it is still there. And offer the two things that actually resolve it: try again, or restart.
/// </para>
/// </summary>
public static class AccountsUnavailableNotice
{
    public const string Title = "Your accounts could not be opened";

    public const string RetryButtonText = "Try again";

    /// <summary>
    /// Shown in place of the first-run greeting on the dashboard. Short enough for a subtitle line, and it
    /// must contradict the welcome copy it replaces rather than merely soften it.
    /// </summary>
    public const string DashboardSubtitle =
        "Your accounts are still saved — this session could not read them. Try again or restart the app.";

    /// <summary>
    /// Whether to show the notice. A plain read of the outcome: it is set once per session during the load,
    /// so this is true exactly in the session where the read failed.
    /// </summary>
    public static bool ShouldShow(RegistryLoadOutcome outcome) => outcome == RegistryLoadOutcome.Failed;

    /// <summary>The body of the notice.</summary>
    /// <param name="storePath">The file the app tried to read. Shown so the owner can verify it exists.</param>
    /// <param name="failureDetail">The underlying error, appended verbatim for support purposes.</param>
    public static string BuildMessage(string? storePath, string? failureDetail)
    {
        var lines = new List<string>
        {
            // First, and unhedged. Everything else is detail.
            "Nothing has been lost. Your accounts, conversations and history are still saved on this " +
            "computer — Unified Messenger just could not open the file that lists them when it started.",

            "Until it can, the app is showing no accounts. It has not changed or replaced anything, and it " +
            "will not save over your account list while it cannot read it.",

            "This usually clears on its own. It is most often caused by security software checking the " +
            "file, or by the folder being briefly unavailable — after a Windows update, or when the app " +
            "starts before a drive is ready."
        };

        if (!string.IsNullOrWhiteSpace(storePath))
        {
            lines.Add("The file it looks for is:\n" + storePath.Trim());
        }

        if (!string.IsNullOrWhiteSpace(failureDetail))
        {
            // Verbatim rather than paraphrased: this is the line that identifies the cause, and softening
            // it would cost the one person who can act on it the only clue they have.
            lines.Add("Reported reason:\n" + failureDetail.Trim());
        }

        return string.Join("\n\n", lines);
    }

    /// <summary>What to say after a retry that also failed, so the button does not appear to do nothing.</summary>
    public const string RetryFailedMessage =
        "Still not readable. Closing Unified Messenger and opening it again usually resolves it. Your " +
        "accounts remain saved either way.";
}
