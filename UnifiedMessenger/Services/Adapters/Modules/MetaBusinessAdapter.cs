using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Adapters;

namespace UnifiedMessenger.Services.Adapters.Modules;

public sealed class MetaBusinessAdapter : BasePlatformAdapter
{
    protected override string ScriptFileName => "meta_business_scraper.js";

    protected override bool SupportsInboundAutoDraft => true;

    public override string PlatformId => "metabusiness";

    protected override IReadOnlyList<string> AdditionalScriptFileNames => ["thread-status-auditor.js"];

    protected override bool HandleCustomMessage(
        string? type,
        JsonElement root,
        NotificationHub hub,
        MessengerInstance instance)
    {
        if (AdapterMessageTypes.DashboardScrapeStatus.Equals(type, StringComparison.OrdinalIgnoreCase))
        {
            HandleDashboardScrapeStatus(root, instance);
            return true;
        }

        if (AdapterMessageTypes.MetaTelemetrySnapshot.Equals(type, StringComparison.OrdinalIgnoreCase))
        {
            var avgMinutes = WebMessageParser.ReadOptionalDouble(root, "averageResponseMinutes");
            var slaHints = WebMessageParser.ReadNonNegativeInt(root, "slaBreachHints");
            var unread = WebMessageParser.ReadNonNegativeInt(root, "unreadCount");
            ProfessionalWorkspaceService.Instance.HandleMetaTelemetrySnapshot(
                instance.Id,
                avgMinutes,
                slaHints,
                unread);
            return true;
        }

        if (!AdapterMessageTypes.MetaInboundMessage.Equals(type, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        ProfessionalWorkspaceService.Instance.HandleMetaInboundMessage(
            instance.Id,
            ParseMessageTimestamp(root),
            WebMessageParser.ReadNonNegativeInt(root, "unreadCount"));

        return true;
    }
}
