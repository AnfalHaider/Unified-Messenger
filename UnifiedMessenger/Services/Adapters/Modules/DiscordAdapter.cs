namespace UnifiedMessenger.Services.Adapters.Modules;

public sealed class DiscordAdapter : BasePlatformAdapter
{
    protected override string ScriptFileName => "discord-adapter.js";

    public override string PlatformId => "discord";
}
