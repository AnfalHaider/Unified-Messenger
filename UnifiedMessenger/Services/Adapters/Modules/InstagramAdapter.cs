namespace UnifiedMessenger.Services.Adapters.Modules;

/// <summary>
/// Instagram (A13). Injects the Relay reader; the scan itself is driven by
/// <see cref="UnifiedMessenger.Services.InstagramSnapshotReader"/>.
/// </summary>
/// <remarks>
/// <para>
/// No inbound auto-draft and no custom message handling: this channel is read-only in the strictest
/// sense available to the app. Instagram pushes nothing to us — the adapter script exposes one function
/// that reads the client's own already-fetched Relay records when asked, and posts no messages of its
/// own.
/// </para>
/// </remarks>
public sealed class InstagramAdapter : BasePlatformAdapter
{
    public override string PlatformId => "instagram";

    protected override string ScriptFileName => "instagram-adapter.js";

    protected override bool SupportsInboundAutoDraft => false;
}
