using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Adapters;
using UnifiedMessenger.Services.Adapters.Modules;
using UnifiedMessenger.Services.Contracts;

namespace UnifiedMessenger.Services.PlatformModules;

public sealed class PlatformModuleRegistry : IPlatformModuleRegistry
{
    private static readonly Lazy<PlatformModuleRegistry> LazyInstance = new(() => new PlatformModuleRegistry());

    private readonly IAppSettingsService _settingsService;

    private PlatformModuleRegistry()
        : this(AppSettingsService.Instance)
    {
    }

    internal PlatformModuleRegistry(IAppSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public static PlatformModuleRegistry Instance => LazyInstance.Value;

    internal static PlatformModuleRegistry CreateForTests(IAppSettingsService settingsService) =>
        new(settingsService);

    public bool IsInstalled(string platformId) =>
        PlatformModuleCatalog.Find(platformId)?.IsInstalled == true;

    public bool IsEnabled(string platformId) =>
        PlatformModuleSettingsHelper.IsPlatformEnabled(platformId, _settingsService.Settings);

    public PlatformCapability GetCapabilities(string platformId)
    {
        if (!IsEnabled(platformId))
        {
            return PlatformCapability.None;
        }

        return PlatformModuleCatalog.Find(platformId)?.Capabilities ?? PlatformCapability.None;
    }

    public IReadOnlyList<PlatformModuleDescriptor> GetInstalledModules() =>
        PlatformModuleCatalog.AllInstalled;

    public IReadOnlyList<PlatformModuleDescriptor> GetEnabledModules() =>
        PlatformModuleCatalog.AllInstalled
            .Where(module => IsEnabled(module.PlatformId))
            .ToList();

    public IPlatformAdapter ResolveAdapter(string platformId)
    {
        var normalized = PlatformDefinition.NormalizePlatformId(platformId);
        if (!IsEnabled(normalized))
        {
            return NullPlatformAdapter.Instance;
        }

        return PlatformAdapterInternals.ResolveEnabledAdapter(normalized);
    }
}
