namespace UnifiedMessenger.Services.PlatformModules;

public sealed class PlatformModuleDescriptor
{
    public required string PlatformId { get; init; }

    public required string DisplayName { get; init; }

    public PlatformCapability Capabilities { get; init; }

    /// <summary>
    /// When false the module cannot be disabled (e.g. generic fallback).
    /// </summary>
    public bool CanDisable { get; init; } = true;

    public bool IsInstalled { get; init; } = true;
}
