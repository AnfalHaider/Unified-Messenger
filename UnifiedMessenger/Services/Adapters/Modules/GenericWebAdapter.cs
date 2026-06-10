namespace UnifiedMessenger.Services.Adapters.Modules;

public sealed class GenericWebAdapter : BasePlatformAdapter
{
    protected override string ScriptFileName => "generic-adapter.js";

    public override string PlatformId => "generic";
}
