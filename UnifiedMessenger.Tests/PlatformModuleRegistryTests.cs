using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Adapters;
using UnifiedMessenger.Services.PlatformModules;

namespace UnifiedMessenger.Tests;

public class PlatformModuleRegistryTests : IDisposable
{
    private readonly string _settingsPath;
    private readonly AppSettingsService _settingsService;

    public PlatformModuleRegistryTests()
    {
        _settingsPath = Path.Combine(Path.GetTempPath(), $"um-platform-modules-{Guid.NewGuid():N}.json");
        _settingsService = new AppSettingsService(_settingsPath);
    }

    public void Dispose()
    {
        if (File.Exists(_settingsPath))
        {
            File.Delete(_settingsPath);
        }
    }

    [Fact]
    public async Task NormalizePlatformModules_SeedsAllDisableablePlatforms()
    {
        await _settingsService.LoadAsync();

        PlatformModuleSettingsHelper.NormalizePlatformModules(_settingsService.Settings);

        Assert.Equal(10, _settingsService.Settings.PlatformModules.Count);
        Assert.All(_settingsService.Settings.PlatformModules, item => Assert.True(item.IsEnabled));
    }

    [Fact]
    public void ResolveAdapter_WhenDisabled_ReturnsNullAdapter()
    {
        var registry = PlatformModuleRegistry.CreateForTests(_settingsService);
        PlatformModuleSettingsHelper.SetPlatformEnabled(_settingsService.Settings, "whatsapp", false);

        var adapter = registry.ResolveAdapter("whatsapp");

        Assert.IsType<NullPlatformAdapter>(adapter);
    }

    [Fact]
    public void ResolveAdapter_WhenEnabled_ReturnsPlatformAdapter()
    {
        var registry = PlatformModuleRegistry.CreateForTests(_settingsService);
        PlatformModuleSettingsHelper.NormalizePlatformModules(_settingsService.Settings);

        var adapter = registry.ResolveAdapter("whatsapp");

        Assert.Equal("whatsapp", adapter.PlatformId);
    }

    [Fact]
    public void GetSelectablePlatforms_ExcludesDisabledPlatforms()
    {
        PlatformModuleSettingsHelper.SetPlatformEnabled(_settingsService.Settings, "metabusiness", false);

        var selectable = PlatformModuleSettingsHelper.GetSelectablePlatforms(_settingsService.Settings);

        Assert.DoesNotContain(selectable, platform => platform.Id.Equals("metabusiness", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(selectable, platform => platform.Id.Equals("generic", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GenericPlatform_IsAlwaysEnabled()
    {
        var registry = PlatformModuleRegistry.CreateForTests(_settingsService);
        PlatformModuleSettingsHelper.SetPlatformEnabled(_settingsService.Settings, "generic", false);

        Assert.True(registry.IsEnabled("generic"));
    }
}
