namespace UnifiedMessenger.Models;

public sealed class PlatformDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public string Description { get; init; } = string.Empty;

    public required string DefaultUrl { get; init; }

    public string IconGlyph { get; init; } = "\uE774";

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
            return "whatsapp";
        }

        var match = All.FirstOrDefault(p => p.Id.Equals(platformId.Trim(), StringComparison.OrdinalIgnoreCase));
        return match?.Id ?? "whatsapp";
    }
}
