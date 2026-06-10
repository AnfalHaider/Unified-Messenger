namespace UnifiedMessenger.Services;

/// <summary>
/// Holds the application composition root set once at launch.
/// </summary>
public static class ApplicationServiceProvider
{
    private static ApplicationServices? _current;

    public static ApplicationServices Current =>
        _current ?? throw new InvalidOperationException(
            "Application services have not been initialized. Call Set() during app launch.");

    public static bool IsInitialized => _current is not null;

    public static void Set(ApplicationServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _current = services;
    }
}
