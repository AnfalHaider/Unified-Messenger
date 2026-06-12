using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Adapters;

namespace UnifiedMessenger.Services.Adapters.Modules;

public abstract class WhatsAppPlatformAdapterBase : BasePlatformAdapter
{
    protected override string ScriptFileName => "whatsapp-adapter.js";

    protected override bool SupportsInboundAutoDraft => false;

    protected override IReadOnlyList<string> AdditionalScriptFileNames => ["thread-status-auditor.js"];

    protected override bool HandleCustomMessage(
        string? type,
        JsonElement root,
        NotificationHub hub,
        MessengerInstance instance) =>
        WhatsAppIngressHandler.TryHandle(type, root, instance);
}
