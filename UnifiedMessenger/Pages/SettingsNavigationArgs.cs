using UnifiedMessenger.Services;

namespace UnifiedMessenger.Pages;

public sealed class SettingsNavigationArgs
{
    public required InstanceRegistryService Registry { get; init; }
}
