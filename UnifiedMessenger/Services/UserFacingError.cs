using System.Text.RegularExpressions;

namespace UnifiedMessenger.Services;

/// <summary>
/// Turns an exception into one line the owner can act on, and puts the full detail in <c>app.log</c>.
/// </summary>
/// <remarks>
/// <para>
/// Two separate defects lived in the failure paths this replaces, at roughly twenty sites across
/// <c>ShellController</c>, <c>SettingsPage</c> and <c>App.LaunchAsync</c>.
/// </para>
/// <para>
/// <b>Nothing was logged.</b> Every one of those catches showed a dialog and returned. When the owner
/// reports "renaming an account failed", <c>app.log</c> — the file support asks them to send — was silent
/// about it. That is the exact inversion of what the v4.99.47 logging sweep established: make the failure
/// visible first, then go looking.
/// </para>
/// <para>
/// <b>Raw <c>ex.Message</c> was the text.</b> A .NET message routinely carries the full store path
/// (<c>C:\Users\…\AppData\Local\UnifiedMessenger\instances.json</c>) and, where the message was empty, the
/// old <c>ShellErrorFormatter</c> fell back to printing the exception's type name. The most visible string
/// in the whole product — <c>App.LaunchAsync</c>'s "The application could not start." — showed a file path
/// from inside <c>%LOCALAPPDATA%</c> to a person who has no use for it.
/// </para>
/// <para>
/// What is deliberately <i>not</i> done here: the message body is not rewritten or paraphrased. .NET's own
/// sentences ("Access to the path is denied.", "The process cannot access the file because it is being used
/// by another process.") are plain English and are the most accurate thing anyone can say. Only the two
/// concrete leaks are removed — absolute paths, and a bare type name standing in for a sentence. Inventing
/// a friendlier explanation would be guessing at a cause the app does not know, which is the mistake
/// recorded in <c>remaining-work.md</c> §0.2b: where the app can state something correct, do not put a
/// generator in front of it.
/// </para>
/// </remarks>
internal static class UserFacingError
{
    /// <summary>Stands in for a redacted absolute path.</summary>
    internal const string RedactedPath = "a file on this PC";

    /// <summary>Used when an exception carries no message worth showing.</summary>
    internal const string NoDetail = "Windows did not say why. The details are in the log file.";

    /// <summary>
    /// Matches a Windows absolute path (drive-letter or UNC) up to the first character that cannot be part
    /// of one. Quotes and brackets around a path in a .NET message are left in place, so the sentence still
    /// reads as a sentence once the path inside them is replaced.
    /// </summary>
    private static readonly Regex AbsolutePath = new(
        @"(?:[A-Za-z]:\\|\\\\)[^""'<>|\r\n]*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Logs <paramref name="ex"/> in full, then returns the line to show the owner.
    /// </summary>
    /// <param name="scope">Log scope, e.g. <c>"Shell.RenameAccount"</c>. Appears in <c>app.log</c>.</param>
    public static string Describe(string scope, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        AppLogger.LogError(scope, ex);
        return Format(ex);
    }

    /// <summary>
    /// Formats without logging. Use only where the caller has already logged the same exception.
    /// </summary>
    public static string Format(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        if (ex is AggregateException aggregate)
        {
            var parts = aggregate.Flatten().InnerExceptions
                .Select(static inner => Sanitize(inner.Message))
                .Where(static part => !string.IsNullOrWhiteSpace(part))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (parts.Count > 0)
            {
                return string.Join(Environment.NewLine, parts);
            }
        }

        var message = Sanitize(ex.Message);
        if (ex.InnerException is { } inner)
        {
            var innerMessage = Sanitize(inner.Message);
            if (!string.IsNullOrWhiteSpace(innerMessage) &&
                !string.Equals(message, innerMessage, StringComparison.Ordinal))
            {
                return string.IsNullOrWhiteSpace(message)
                    ? innerMessage
                    : $"{message}{Environment.NewLine}{innerMessage}";
            }
        }

        // The type name was what the old formatter fell back to. "COMException" tells the owner nothing and
        // reads as the app leaking its own internals at the moment it is already failing them.
        return string.IsNullOrWhiteSpace(message) ? NoDetail : message;
    }

    private static string Sanitize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var redacted = AbsolutePath.Replace(message, RedactedPath);
        return string.Join(
            " ",
            redacted.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
