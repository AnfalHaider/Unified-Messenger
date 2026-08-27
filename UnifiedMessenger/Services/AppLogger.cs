namespace UnifiedMessenger.Services;

/// <summary>
/// Lightweight rolling log to %LocalAppData%\UnifiedMessenger\app.log.
/// Rotates to app.old.log when the file exceeds 256 KB. Thread-safe, never throws.
/// </summary>
internal static class AppLogger
{
    private const long MaxFileSizeBytes = 256 * 1024;

    private static readonly string LogPath =
        Path.Combine(ApplicationPaths.UserDataRoot, "app.log");

    private static readonly string ArchivePath =
        Path.Combine(ApplicationPaths.UserDataRoot, "app.old.log");

    private static readonly object WriteLock = new();

    private static readonly object ThrottleLock = new();

    private static readonly TimeSpan ThrottleWindow = TimeSpan.FromMinutes(1);

    private static readonly Dictionary<string, (DateTimeOffset LastWritten, int Suppressed)> Throttled =
        new(StringComparer.Ordinal);

    /// <summary>
    /// When true, nothing is written to disk. Set once by the test assembly.
    /// </summary>
    /// <remarks>
    /// The log path is fixed under the real user-data root, so any test that exercises production code
    /// containing a log call appends to the developer's own app.log. Real examples found in a live log
    /// during this audit — all of them fabricated by the test suite, none of which happened to any user:
    /// <code>
    /// [ERR] [Lifecycle.Flush.third] IOException: third could not be written
    /// [ERR] [Settings.Load.Corrupt] JsonException: x
    /// [ERR] [AwaitingOverrides.Load.Corrupt] JsonException: truncated
    /// [WRN] [ChatEntryParser] Skipped 1 of 2 conversation rows as unparseable.
    /// </code>
    /// app.log is the product's only diagnostic surface, and several increments of this audit were spent
    /// routing genuine failures into it. Seeding it with invented ones defeats that.
    ///
    /// This is a single global switch rather than per-call-site injection because the first attempt at this
    /// fix threaded a callback through one method (<c>FlushStoresAsync</c>) and missed every other logging
    /// call in the codebase — the entries above are from suites that fix did not touch.
    /// </remarks>
    internal static bool SuppressWritesForTests { get; set; }

    public static void LogError(string context, Exception ex) =>
        Write("ERR", context, ex.ToString());

    public static void LogError(string context, string message) =>
        Write("ERR", context, message);

    public static void LogWarning(string context, string message) =>
        Write("WRN", context, message);

    public static void LogInfo(string context, string message) =>
        Write("INF", context, message);

    /// <summary>
    /// Logs a warning at most once per minute per <paramref name="throttleKey"/>, counting what it skipped.
    /// </summary>
    /// <remarks>
    /// For failures that arrive per scraped message rather than per operation. The ingest funnel sees every
    /// badge, heartbeat and chat update from every account, so routing its failures straight to
    /// <see cref="LogWarning"/> would blow through the 256 KB rotation in seconds and destroy the log's
    /// value as a diagnostic — the opposite of the point. Reporting "and N more since" keeps the rate
    /// visible, which is the part that actually matters: one parse failure is noise, four hundred a minute
    /// is a broken scraper.
    /// </remarks>
    public static void LogWarningThrottled(string context, string message, string throttleKey)
    {
        int suppressed;

        lock (ThrottleLock)
        {
            var now = DateTimeOffset.Now;
            if (Throttled.TryGetValue(throttleKey, out var state))
            {
                if (now - state.LastWritten < ThrottleWindow)
                {
                    Throttled[throttleKey] = (state.LastWritten, state.Suppressed + 1);
                    return;
                }

                suppressed = state.Suppressed;
            }
            else
            {
                suppressed = 0;
            }

            Throttled[throttleKey] = (now, 0);
        }

        Write("WRN", context, suppressed > 0 ? $"{message} (and {suppressed} more since)" : message);
    }

    private static void Write(string level, string context, string message)
    {
        if (SuppressWritesForTests)
        {
            return;
        }

        try
        {
            var line = $"{DateTimeOffset.Now:u} [{level}] [{context}] {message}{Environment.NewLine}";
            lock (WriteLock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                RotateIfNeeded();
                File.AppendAllText(LogPath, line);
            }
        }
        catch
        {
            // Never throw from the logger.
        }
    }

    private static void RotateIfNeeded()
    {
        try
        {
            if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaxFileSizeBytes)
            {
                File.Move(LogPath, ArchivePath, overwrite: true);
            }
        }
        catch
        {
            // Rotation failure is non-fatal.
        }
    }
}
