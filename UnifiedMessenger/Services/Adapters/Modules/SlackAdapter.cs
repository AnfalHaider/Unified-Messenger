namespace UnifiedMessenger.Services.Adapters.Modules;

public sealed class SlackAdapter : BasePlatformAdapter
{
    protected override string ScriptFileName => "slack-adapter.js";

    public override string PlatformId => "slack";
}
