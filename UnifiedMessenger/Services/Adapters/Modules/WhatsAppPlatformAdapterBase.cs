using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Adapters;

namespace UnifiedMessenger.Services.Adapters.Modules;

public abstract class WhatsAppPlatformAdapterBase : BasePlatformAdapter
{
    protected override string ScriptFileName => "whatsapp-adapter.js";

    protected override bool SupportsInboundAutoDraft => false;

    // The store bridge reads WhatsApp Web's in-memory (already-decrypted) model collections. It is
    // additive: when it can't find the collections it reports so and the IndexedDB scan stays in charge.
    protected override IReadOnlyList<string> AdditionalScriptFileNames =>
        ["thread-status-auditor.js", "whatsapp-store-bridge.js"];

    protected override bool HandleCustomMessage(
        string? type,
        JsonElement root,
        NotificationHub hub,
        MessengerInstance instance) =>
        WhatsAppIngressHandler.TryHandle(type, root, instance);
}
