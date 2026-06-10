using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.PlatformModules;

namespace UnifiedMessenger.Tests;

[Collection(UnifiedMessengerSerialCollection.Name)]
public class PlatformModulePhase3Tests : IDisposable
{
    private readonly List<PlatformModuleSetting> _originalModules;

    public PlatformModulePhase3Tests()
    {
        PlatformModuleSettingsHelper.NormalizePlatformModules(AppSettingsService.Instance.Settings);
        _originalModules = AppSettingsService.Instance.Settings.PlatformModules
            .Select(item => new PlatformModuleSetting
            {
                PlatformId = item.PlatformId,
                IsEnabled = item.IsEnabled
            })
            .ToList();
    }

    public void Dispose()
    {
        AppSettingsService.Instance.Settings.PlatformModules = _originalModules
            .Select(item => new PlatformModuleSetting
            {
                PlatformId = item.PlatformId,
                IsEnabled = item.IsEnabled
            })
            .ToList();
    }

    [Fact]
    public void FilterEnabledInstances_ExcludesDisabledPlatformInstances()
    {
        var settings = AppSettingsService.Instance.Settings;
        PlatformModuleSettingsHelper.SetPlatformEnabled(settings, "telegram", false);

        var instances = new[]
        {
            new MessengerInstance { Id = "wa-1", Platform = "whatsapp", DisplayName = "WA" },
            new MessengerInstance { Id = "tg-1", Platform = "telegram", DisplayName = "TG" }
        };

        var filtered = PlatformModuleSettingsHelper
            .FilterEnabledInstances(instances, settings)
            .ToList();

        Assert.Single(filtered);
        Assert.Equal("wa-1", filtered[0].Id);
    }

    [Fact]
    public void IsDashboardScrapeCapable_ReturnsFalseWhenModuleDisabled()
    {
        PlatformModuleSettingsHelper.SetPlatformEnabled(AppSettingsService.Instance.Settings, "googlebusiness", false);

        var instance = new MessengerInstance
        {
            Id = "gb-1",
            Platform = "googlebusiness",
            DisplayName = "Google"
        };

        Assert.False(DashboardScrapeOrchestrator.IsDashboardScrapeCapable(instance));
    }

    [Fact]
    public void BuildPlatformHealth_OmitsDisabledPlatforms()
    {
        PlatformModuleSettingsHelper.SetPlatformEnabled(AppSettingsService.Instance.Settings, "metabusiness", false);

        var snapshot = UnifiedMessengerDashboardService.Instance.BuildSnapshot([
            new MessengerInstance
            {
                Id = "wa-1",
                Platform = "whatsappbusiness",
                DisplayName = "WA Pro",
                Category = WorkspaceCategory.Professional,
                ProfileName = "wa-pro"
            }
        ]);

        Assert.DoesNotContain(
            snapshot.PlatformHealth,
            indicator => indicator.PlatformId.Equals("metabusiness", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            snapshot.PlatformHealth,
            indicator => indicator.PlatformId.Equals("whatsappbusiness", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FormatPlatformBreakdown_OmitsDisabledPlatformCounts()
    {
        PlatformModuleSettingsHelper.SetPlatformEnabled(AppSettingsService.Instance.Settings, "metabusiness", false);

        var breakdown = BranchWorkspaceHelper.FormatPlatformBreakdown(
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["whatsapp"] = 2,
                ["metabusiness"] = 3
            });

        Assert.Contains("WA 2", breakdown, StringComparison.Ordinal);
        Assert.DoesNotContain("Meta", breakdown, StringComparison.Ordinal);
    }

    [Fact]
    public void WhatsAppDeliveryPresentation_HiddenWhenModuleDisabled()
    {
        PlatformModuleSettingsHelper.SetPlatformEnabled(AppSettingsService.Instance.Settings, "whatsappbusiness", false);

        Assert.False(WhatsAppDeliveryStatusPresentation.ShouldShow(
            "whatsappbusiness",
            "read"));
    }
}
