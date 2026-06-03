namespace UnifiedMessenger.Models;

/// <summary>
/// Describes a single isolated WebView2 messaging instance backed by a unique browser profile.
/// </summary>
public sealed class MessengerInstance
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Maps to <see cref="Microsoft.Web.WebView2.Core.CoreWebView2ControllerOptions.ProfileName"/>.
    /// Must be unique per account and stable across sessions.
    /// </summary>
    public string ProfileName { get; set; } = string.Empty;

    public string StartUrl { get; set; } = string.Empty;

    public string Platform { get; set; } = "generic";

    public string IconGlyph { get; set; } = "\uE8BD";

    /// <summary>Platform accent color as #RRGGBB for sidebar identity.</summary>
    public string AccentColor { get; set; } = "#6B7280";

    /// <summary>Workspace grouping: Personal or Professional.</summary>
    public WorkspaceCategory Category { get; set; } = WorkspaceCategory.Personal;

    public bool IsProfessional => Category == WorkspaceCategory.Professional;

    public int SortOrder { get; set; }

    public bool NotificationsMuted { get; set; }

    public MemoryTierPreference MemoryTier { get; set; } = MemoryTierPreference.Normal;

    public string? Notes { get; set; }

    public void ApplyPlatformBranding()
    {
        var platform = PlatformDefinition.FindById(Platform);
        if (platform is null)
        {
            return;
        }

        IconGlyph = platform.IconGlyph;
        AccentColor = platform.AccentColor;
    }
}
