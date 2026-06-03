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
            Id = "generic",
            DisplayName = "Custom URL",
            DefaultUrl = string.Empty,
            IconGlyph = "\uE268",
            AccentColor = "#6B7280"
        }
    ];

    public static PlatformDefinition? FindById(string? platformId) =>
        All.FirstOrDefault(p => p.Id.Equals(platformId, StringComparison.OrdinalIgnoreCase));
}
