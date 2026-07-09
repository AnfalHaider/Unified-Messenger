using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services.Adapters;

public static class PlatformAdapterFactory
{
    public static IPlatformAdapter Resolve(string platformId) =>
        PlatformAdapterInternals.ResolveEnabledAdapter(
            PlatformDefinition.NormalizePlatformId(platformId));
}
