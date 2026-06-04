namespace UnifiedMessenger.Services;

/// <summary>
/// Canonical per-user paths for unpackaged WinExe deployment (no MSIX container).
/// Install target: %LocalAppData%\UnifiedMessenger (see installer.iss).
/// </summary>
public static class ApplicationPaths
{
    public const string AppDataFolderName = "UnifiedMessenger";

    public const string ApplicationMutexName = "UnifiedMessenger_AppMutex";

    public static string UserDataRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppDataFolderName);

    public static string DefaultInstallRoot => UserDataRoot;

    public static string SettingsFilePath => Path.Combine(UserDataRoot, "settings.json");

    public static string InstancesFilePath => Path.Combine(UserDataRoot, "instances.json");

    public static string? TryResolveAppIconUri()
    {
        var iconPath = TryResolveAppIconFilePath();
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return null;
        }

        return new Uri(iconPath).AbsoluteUri;
    }

    public static string? TryResolveAppIconFilePath()
    {
        var baseDirectory = AppContext.BaseDirectory;
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return null;
        }

        var iconPath = Path.Combine(baseDirectory, "Assets", "AppIcon.ico");
        return File.Exists(iconPath) ? iconPath : null;
    }
}
