using UnifiedMessenger.Services;

namespace UnifiedMessenger.Pages;

/// <summary>
/// Navigation payload for pages that need the live instance registry.
/// </summary>
public sealed class RegistryNavigationArgs
{
    public required IInstanceRegistryService Registry { get; init; }

    public ApplicationServices? Services { get; init; }

    public string? SettingsSectionKey { get; init; }
}
