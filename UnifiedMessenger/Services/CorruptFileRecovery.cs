using System.Text.Json;

namespace UnifiedMessenger.Services;

/// <summary>
/// Shared handling for a durable store file that cannot be read.
/// </summary>
/// <remarks>
/// Every store that loads JSON from disk faces the same three questions, and they were being answered
/// inconsistently: whether to record the failure somewhere that survives a Release build, whether to
/// preserve the unreadable bytes, and which exceptions even count as "unreadable".
///
/// The concrete defects this centralises away:
/// <list type="bullet">
/// <item>Stores reported corruption with <c>Debug.WriteLine</c>, which the compiler strips from Release —
/// so in the build customers run, a reset left no trace at all.</item>
/// <item>Stores caught only <see cref="JsonException"/>, so a file locked by a backup tool or antivirus
/// threw <see cref="IOException"/> straight out of the load path.</item>
/// <item>Only the settings store moved the bad file aside. The others left it in place and then
/// overwrote it with empty state on the next flush, destroying any chance of recovery.</item>
/// </list>
/// </remarks>
internal static class CorruptFileRecovery
{
    /// <summary>
    /// True when <paramref name="ex"/> means "this file could not be read", as opposed to a bug.
    /// </summary>
    /// <remarks>
    /// Deliberately does NOT include <see cref="OperationCanceledException"/>: a cancelled load is not a
    /// corrupt file, and treating it as one would move a perfectly good file aside during shutdown.
    /// </remarks>
    public static bool IsUnreadable(Exception ex) =>
        ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException;

    /// <summary>
    /// Records the failure and moves the unreadable file aside so its bytes survive.
    /// </summary>
    /// <param name="path">The store file that could not be read.</param>
    /// <param name="scope">Log scope identifying the store, e.g. <c>"AwaitingOverrides"</c>.</param>
    /// <param name="ex">The failure.</param>
    /// <returns>Where the file was preserved, or null if it could not be preserved.</returns>
    public static string? Preserve(string path, string scope, Exception ex)
    {
        AppLogger.LogError($"{scope}.Load.Corrupt", ex);

        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            var backupPath = $"{path}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
            File.Move(path, backupPath, overwrite: true);
            return backupPath;
        }
        catch (Exception moveFailure)
        {
            // Best effort. Failing to preserve the file must never prevent the app from starting.
            AppLogger.LogError($"{scope}.Load.PreserveFailed", moveFailure);
            return null;
        }
    }
}
