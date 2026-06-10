namespace UnifiedMessenger.Services.Adapters.Modules;

public sealed class TelegramAdapter : BasePlatformAdapter
{
    protected override string ScriptFileName => "telegram-adapter.js";

    public override string PlatformId => "telegram";
}
