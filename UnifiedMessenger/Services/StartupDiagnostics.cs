namespace UnifiedMessenger.Services;

internal static class StartupDiagnostics
{
    public static void Log(string message) =>
        AppLogger.LogInfo("Startup", message);
}
