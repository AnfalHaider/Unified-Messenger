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

    public static void LogError(string context, Exception ex) =>
        Write("ERR", context, ex.ToString());

    public static void LogError(string context, string message) =>
        Write("ERR", context, message);

    public static void LogWarning(string context, string message) =>
        Write("WRN", context, message);

    public static void LogInfo(string context, string message) =>
        Write("INF", context, message);

    private static void Write(string level, string context, string message)
    {
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
