using UnifiedMessenger.Services.Adapters.Modules;

namespace UnifiedMessenger.Services.Adapters;

internal static class PlatformAdapterInternals
{
    private static readonly WhatsAppAdapter WhatsApp = new();
    private static readonly WhatsAppBusinessAdapter WhatsAppBusiness = new();
    private static readonly TelegramAdapter Telegram = new();
    private static readonly MessengerAdapter Messenger = new();
    private static readonly MetaBusinessAdapter MetaBusiness = new();
    private static readonly GoogleBusinessAdapter GoogleBusiness = new();
    private static readonly SlackAdapter Slack = new();
    private static readonly DiscordAdapter Discord = new();
    private static readonly SignalAdapter Signal = new();
    private static readonly TeamsAdapter Teams = new();
    private static readonly GenericWebAdapter GenericWeb = new();

    public static IPlatformAdapter ResolveEnabledAdapter(string normalizedPlatformId) =>
        normalizedPlatformId switch
        {
            "whatsapp" => WhatsApp,
            "whatsappbusiness" => WhatsAppBusiness,
            "telegram" => Telegram,
            "messenger" => Messenger,
            "metabusiness" => MetaBusiness,
            "googlebusiness" => GoogleBusiness,
            "slack" => Slack,
            "discord" => Discord,
            "signal" => Signal,
            "teams" => Teams,
            _ => GenericWeb
        };
}
