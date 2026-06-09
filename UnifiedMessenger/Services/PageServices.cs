using UnifiedMessenger.Pages;

namespace UnifiedMessenger.Services;

/// <summary>
/// Factory helpers for page navigation payloads that share the composition root.
/// </summary>
public static class PageServices
{
    public static RegistryNavigationArgs CreateRegistryArgs(ApplicationServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return new RegistryNavigationArgs
        {
            Registry = services.Registry,
            Services = services
        };
    }
}
