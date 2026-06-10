using UnifiedMessenger.Services.PlatformModules;

namespace UnifiedMessenger.Services.Adapters;

public static class PlatformAdapterFactory
{
    public static IPlatformAdapter Resolve(string platformId) =>
        PlatformModuleRegistry.Instance.ResolveAdapter(platformId);
}
