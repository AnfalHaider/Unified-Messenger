namespace UnifiedMessenger.Services.Adapters.Modules;

public sealed class SignalAdapter : BasePlatformAdapter
{
    protected override string ScriptFileName => "signal-adapter.js";

    public override string PlatformId => "signal";
}
