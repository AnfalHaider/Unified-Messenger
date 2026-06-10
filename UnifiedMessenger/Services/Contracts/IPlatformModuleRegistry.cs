using UnifiedMessenger.Services.Adapters;
using UnifiedMessenger.Services.PlatformModules;

namespace UnifiedMessenger.Services.Contracts;

public interface IPlatformModuleRegistry
{
    bool IsInstalled(string platformId);

    bool IsEnabled(string platformId);

    PlatformCapability GetCapabilities(string platformId);

    IReadOnlyList<PlatformModuleDescriptor> GetInstalledModules();

    IReadOnlyList<PlatformModuleDescriptor> GetEnabledModules();

    IPlatformAdapter ResolveAdapter(string platformId);
}
