using System.Text.Json.Serialization;

namespace UnifiedMessenger.Models;

/// <summary>
/// Describes a single isolated WebView2 messaging instance backed by a unique browser profile.
/// </summary>
public sealed class MessengerInstance
{
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Runtime connection state from the WebView handshake (not persisted to instances.json).
    /// </summary>
    [JsonIgnore]
    public InstanceConnectionStatus Status { get; set; } = InstanceConnectionStatus.Initializing;

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

    /// <summary>
    /// User-chosen built-in avatar icon (a Segoe Fluent glyph). When set, the avatar shows this glyph on
    /// <see cref="CustomIconColor"/> instead of initials. A cached avatar image (upload/import) takes
    /// precedence over this; empty means fall back to initials. Not reset by platform branding.
    /// </summary>
    public string? CustomIconGlyph { get; set; }

    /// <summary>Flat fill color (#RRGGBB) for the chosen built-in <see cref="CustomIconGlyph"/>.</summary>
    public string? CustomIconColor { get; set; }

    /// <summary>
    /// Font family for <see cref="CustomIconGlyph"/>. Null = the system Segoe Fluent icon font (general
    /// icons); a bundled brand font reference (e.g. the Font Awesome Brands ms-appx URI) for social logos.
    /// </summary>
    public string? CustomIconFontFamily { get; set; }

    /// <summary>Workspace grouping: Personal or Professional.</summary>
    public WorkspaceCategory Category { get; set; } = WorkspaceCategory.Personal;

    public bool IsProfessional => Category == WorkspaceCategory.Professional;

    public int SortOrder { get; set; }

    public bool NotificationsMuted { get; set; }

    public MemoryTierPreference MemoryTier { get; set; } = MemoryTierPreference.Normal;

    public string? Notes { get; set; }

    /// <summary>
    /// Optional canonical branch location key (e.g. DHA-2). When empty, inferred from <see cref="DisplayName"/>.
    /// </summary>
    public string? BranchKey { get; set; }

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

    /// <summary>
    /// Repairs invalid persisted values after load, import, or manual JSON edits.
    /// </summary>
    public void Normalize()
    {
        Id = Id?.Trim() ?? string.Empty;
        DisplayName = DisplayName?.Trim() ?? string.Empty;
        ProfileName = ProfileName?.Trim() ?? string.Empty;
        StartUrl = StartUrl?.Trim() ?? string.Empty;
        var rawPlatform = Platform?.Trim() ?? string.Empty;
        Platform = PlatformDefinition.NormalizePlatformId(Platform);

        if (PlatformDefinition.FindById(rawPlatform) is null &&
            !string.IsNullOrWhiteSpace(rawPlatform))
        {
            var def = PlatformDefinition.FindById(Platform);
            if (def is not null)
            {
                StartUrl = def.DefaultUrl;
            }
        }
        Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();
        BranchKey = string.IsNullOrWhiteSpace(BranchKey) ? null : BranchKey.Trim();

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            DisplayName = PlatformDefinition.FindById(Platform)?.DisplayName ?? "Account";
        }

        if (!Enum.IsDefined(Category))
        {
            Category = WorkspaceCategory.Personal;
        }

        if (!Enum.IsDefined(MemoryTier))
        {
            MemoryTier = MemoryTierPreference.Normal;
        }

        if (string.IsNullOrWhiteSpace(ProfileName) && !string.IsNullOrWhiteSpace(Id))
        {
            ProfileName = $"{Platform}-{Id}";
            if (ProfileName.Length > 64)
            {
                ProfileName = ProfileName[..64].TrimEnd('.', ' ');
            }
        }

        ApplyPlatformBranding();
    }
}
