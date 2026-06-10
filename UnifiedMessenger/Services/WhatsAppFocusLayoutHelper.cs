using UnifiedMessenger.Models;
using UnifiedMessenger.Services.PlatformModules;

namespace UnifiedMessenger.Services;

/// <summary>
/// Applies WhatsApp-only OCC layout and sidebar simplification when non-WhatsApp modules are disabled.
/// </summary>
public static class WhatsAppFocusLayoutHelper
{
    public static readonly IReadOnlyList<string> WhatsAppFocusKpiOrder =
        ["sla", "open", "immediate", "hanging", "revenue"];

    public static readonly IReadOnlyList<string> WhatsAppFocusHiddenPanels =
    [
        OccLayoutDefaults.PlatformIntelligencePanelId,
        OccLayoutDefaults.AnalyticsPanelId
    ];

    public static bool IsWhatsAppOnlyMode(AppSettings? settings = null)
    {
        settings ??= AppSettingsService.Instance.Settings;
        PlatformModuleSettingsHelper.NormalizePlatformModules(settings);

        var enabledDisableable = settings.PlatformModules
            .Where(item => item.IsEnabled && IsDisableableModule(item.PlatformId))
            .ToList();

        if (enabledDisableable.Count == 0)
        {
            return false;
        }

        return enabledDisableable.All(item => IsWhatsAppFamily(item.PlatformId));
    }

    public static bool TryApplyRecommendedLayout(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!IsWhatsAppOnlyMode(settings) || settings.OccWhatsAppFocusLayoutApplied)
        {
            return false;
        }

        OccLayoutService.ApplyPreset(settings, OccLayoutPresets.WhatsAppFocus);
        settings.OccHiddenPanels = WhatsAppFocusHiddenPanels.ToList();
        settings.OccKpiMetricOrder = WhatsAppFocusKpiOrder.ToList();
        settings.OccWhatsAppFocusLayoutApplied = true;
        return true;
    }

    private static bool IsDisableableModule(string platformId)
    {
        var module = PlatformModuleCatalog.Find(PlatformDefinition.NormalizePlatformId(platformId));
        return module?.CanDisable == true;
    }

    private static bool IsWhatsAppFamily(string platformId)
    {
        var normalized = PlatformDefinition.NormalizePlatformId(platformId);
        return normalized.Equals("whatsapp", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("whatsappbusiness", StringComparison.OrdinalIgnoreCase);
    }
}
