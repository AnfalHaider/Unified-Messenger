using System.Diagnostics;

namespace UnifiedMessenger.Services;

internal static class StartupDiagnostics
{
    public static void Log(string message)
    {
        try
        {
            var line = $"{DateTimeOffset.Now:u} {message}{Environment.NewLine}";
            File.AppendAllText(GetLogPath(), line);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Startup log write failed: {ex.Message}");
        }
    }

    private static string GetLogPath() =>
        Path.Combine(ApplicationPaths.UserDataRoot, "startup.log");
}
