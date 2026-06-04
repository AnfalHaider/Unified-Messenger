using UnifiedMessenger.Services;

namespace UnifiedMessenger.Pages;

/// <summary>
/// Navigation payload for pages that need the live instance registry.
/// </summary>
public sealed class RegistryNavigationArgs
{
    public required InstanceRegistryService Registry { get; init; }
}
