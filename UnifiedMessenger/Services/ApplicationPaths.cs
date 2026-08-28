namespace UnifiedMessenger.Services;

/// <summary>
/// Canonical per-user paths for unpackaged WinExe deployment (no MSIX container).
/// User data: %LocalAppData%\UnifiedMessenger. Install target: %LocalAppData%\Programs\UnifiedMessenger (see installer.iss).
/// </summary>
public static class ApplicationPaths
{
    public const string AppDataFolderName = "UnifiedMessenger";

    public const string ApplicationMutexName = "UnifiedMessenger_AppMutex";

    /// <summary>
    /// When set, every durable store writes here instead of the real user-data root. Set once by the test
    /// assembly's module initializer; null in every shipping build.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The singleton stores — <c>OversightChatSnapshotService.Instance</c>,
    /// <c>ResponseTimeTracker.Instance</c>, <c>ContactHistoryStore.Instance</c>,
    /// <c>MessageAnalyticsService.Instance</c> — resolve their paths from
    /// <see cref="UserDataRoot"/>, and the test suite uses those same singletons. So every run of
    /// <c>dotnet test</c> wrote fabricated chats into the developer's own live oversight data: a scan of
    /// the real store found a test account id (<c>inst-1</c>) sitting alongside the real ones.
    /// </para>
    /// <para>
    /// The damaging part was not the junk rows. <c>OversightChatSnapshotService.Update</c> calls
    /// <c>ResponseTimeTracker.Observe</c>, which stamps a per-account <i>watch start</i> the first time it
    /// sees an account — and first-response time is only ever measured for messages that arrive after it.
    /// Every suite run pushed that stamp to "now" for every account it touched, disqualifying every
    /// conversation already in flight. That is why the store could hold 761 KB of scraped snapshot and
    /// 218 KB of contact history and still contain <b>zero</b> reply-time samples: the measurement was
    /// being reset faster than it could accrue, and the dashboard reported the result as fact.
    /// </para>
    /// <para>
    /// <see cref="AppLogger.SuppressWritesForTests"/> fixed exactly this disease for <c>app.log</c> and
    /// stopped there. Redirecting the root instead of adding a per-store flag means a store written next
    /// month inherits the fix rather than needing to remember it.
    /// </para>
    /// </remarks>
    internal static string? UserDataRootOverrideForTests { get; set; }

    public static string UserDataRoot =>
        UserDataRootOverrideForTests
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppDataFolderName);

    /// <summary>Default per-user install folder for the unpackaged WinExe (binaries only).</summary>
    public static string DefaultInstallRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            AppDataFolderName);

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

    public static string? TryResolveBrandingAssetPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var baseDirectory = AppContext.BaseDirectory;
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return null;
        }

        var assetPath = Path.Combine(baseDirectory, "Assets", "Branding", fileName);
        return File.Exists(assetPath) ? assetPath : null;
    }

    public static string? TryResolveWordmarkHeroUri() =>
        TryResolveBrandingUri("wordmark-hero.png");

    public static string? TryResolveWordmarkInlineUri(bool useDarkTheme) =>
        TryResolveBrandingUri(useDarkTheme ? "wordmark-inline-dark.png" : "wordmark-inline-light.png");

    /// <summary>The 1024px app-logo master as a file:// URI (for a BitmapImage). Relative ms-appx Image
    /// sources don't resolve in this unpackaged app, so brand images load from the physical path instead.</summary>
    public static string? TryResolveIconMasterUri() =>
        TryResolveBrandingUri("icon-master.png");

    private static string? TryResolveBrandingUri(string fileName)
    {
        var path = TryResolveBrandingAssetPath(fileName);
        return string.IsNullOrWhiteSpace(path) ? null : new Uri(path).AbsoluteUri;
    }
}
