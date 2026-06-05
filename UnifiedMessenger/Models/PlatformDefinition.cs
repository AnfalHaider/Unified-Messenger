namespace UnifiedMessenger.Models;

public sealed class PlatformDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string DefaultUrl { get; init; }

    public string IconGlyph { get; init; } = "\uE774";

    /// <summary>Hex accent color used for sidebar identity (e.g. #25D366).</summary>
    public string AccentColor { get; init; } = "#6B7280";

    public static IReadOnlyList<PlatformDefinition> All { get; } =
    [
        new PlatformDefinition
        {
            Id = "whatsapp",
            DisplayName = "WhatsApp",
            DefaultUrl = "https://web.whatsapp.com/",
            IconGlyph = "\uE8BD",
            AccentColor = "#25D366"
        },
        new PlatformDefinition
        {
            Id = "whatsappbusiness",
            DisplayName = "WhatsApp Business",
            DefaultUrl = "https://web.whatsapp.com/",
            IconGlyph = "\uE8BD",
            AccentColor = "#128C7E"
        },
        new PlatformDefinition
        {
            Id = "telegram",
            DisplayName = "Telegram",
            DefaultUrl = "https://web.telegram.org/",
            IconGlyph = "\uE939",
            AccentColor = "#229ED9"
        },
        new PlatformDefinition
        {
            Id = "messenger",
            DisplayName = "Messenger",
            DefaultUrl = "https://www.messenger.com/",
            IconGlyph = "\uE8F2",
            AccentColor = "#0084FF"
        },
        new PlatformDefinition
        {
            Id = "slack",
            DisplayName = "Slack",
            DefaultUrl = "https://app.slack.com/client",
            IconGlyph = "\uE715",
            AccentColor = "#4A154B"
        },
        new PlatformDefinition
        {
            Id = "discord",
            DisplayName = "Discord",
            DefaultUrl = "https://discord.com/app",
            IconGlyph = "\uE716",
            AccentColor = "#5865F2"
        },
        new PlatformDefinition
        {
            Id = "signal",
            DisplayName = "Signal",
            DefaultUrl = "https://signal.org/",
            IconGlyph = "\uE722",
            AccentColor = "#3A76F0"
        },
        new PlatformDefinition
        {
            Id = "teams",
            DisplayName = "Microsoft Teams",
            DefaultUrl = "https://teams.microsoft.com/",
            IconGlyph = "\uE715",
            AccentColor = "#6264A7"
        },
        new PlatformDefinition
        {
            Id = "metabusiness",
            DisplayName = "Meta Business Suite",
            DefaultUrl = "https://business.facebook.com/latest/inbox/",
            IconGlyph = "\uE717",
            AccentColor = "#0866FF"
        },
        new PlatformDefinition
        {
            Id = "googlebusiness",
            DisplayName = "Google Business Profile",
            DefaultUrl = "https://business.google.com/locations",
            IconGlyph = "\uE774",
            AccentColor = "#4285F4"
        },
        new PlatformDefinition
        {
            Id = "generic",
            DisplayName = "Custom URL",
            DefaultUrl = string.Empty,
            IconGlyph = "\uE268",
            AccentColor = "#6B7280"
        }
    ];

    public static PlatformDefinition? FindById(string? platformId)
    {
        if (string.IsNullOrWhiteSpace(platformId))
        {
            return null;
        }

        return All.FirstOrDefault(p => p.Id.Equals(platformId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static string NormalizePlatformId(string? platformId)
    {
        if (string.IsNullOrWhiteSpace(platformId))
        {
            return "generic";
        }

        var match = All.FirstOrDefault(p => p.Id.Equals(platformId.Trim(), StringComparison.OrdinalIgnoreCase));
        return match?.Id ?? "generic";
    }
}
