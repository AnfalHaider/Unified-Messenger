namespace UnifiedMessenger.Services.Adapters.Modules;

public sealed class TeamsAdapter : BasePlatformAdapter
{
    protected override string ScriptFileName => "teams-adapter.js";

    public override string PlatformId => "teams";
}
