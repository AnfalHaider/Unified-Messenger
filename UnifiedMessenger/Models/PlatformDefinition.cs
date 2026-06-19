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
        },
        new PlatformDefinition
        {
            // Google Business reviews, embedded as a first-class channel. Routes to the NullPlatformAdapter
            // for now (no metric scraping yet \u2014 a GoogleBusinessAdapter that reads rating / % responded /
            // unanswered from the embedded DOM is future work that needs a live logged-in account to tune).
            Id = "googlebusiness",
            DisplayName = "Google Business",
            Description = "Google Business reviews \u2014 embedded. (Review metrics scraping is planned.)",
            DefaultUrl = "https://business.google.com/",
            IconGlyph = "",
            AccentColor = "#4285F4"
        },
        new PlatformDefinition
        {
            // A generic web page monitored in its own tab. No adapter scraping and no oversight data \u2014
            // ResolveEnabledAdapter routes "generic" to the NullPlatformAdapter. DefaultUrl is intentionally
            // empty so the user-supplied URL isn't host-restricted (ResolveStartUrl skips the host-match
            // guard when DefaultUrl is blank) and so a custom URL is required.
            Id = "generic",
            DisplayName = "Web page",
            Description = "Any website \u2014 monitored in a tab. No oversight metrics.",
            DefaultUrl = string.Empty,
            IconGlyph = "\uE774",
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
            return "whatsapp";
        }

        var match = All.FirstOrDefault(p => p.Id.Equals(platformId.Trim(), StringComparison.OrdinalIgnoreCase));
        return match?.Id ?? "whatsapp";
    }
}
