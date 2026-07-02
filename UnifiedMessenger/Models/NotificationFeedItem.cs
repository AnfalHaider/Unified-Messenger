using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Models;

public sealed class NotificationFeedItem
{
    /// <summary>Account accent as a brush for the section-header avatar.</summary>
    public SolidColorBrush AccentBrush => PlatformBrandingHelper.GetAccentBrush(AccentColor);

    public bool IsGroupHeader { get; init; }

    public string? GroupTitle { get; init; }

    public int GroupUnreadCount { get; init; }

    public string GroupUnreadLabel => GroupUnreadCount > 0 ? $"{GroupUnreadCount} unread" : string.Empty;

    /// <summary>Two-letter avatar initials for the account section header (per-account identity).</summary>
    public string GroupInitials { get; init; } = string.Empty;

    public NotificationAlert? Alert { get; init; }

    public string IconGlyph { get; init; } = "\uE8BD";

    public string AccentColor { get; init; } = "#6B7280";

    public bool IsRead => Alert?.IsRead ?? true;

    public static NotificationFeedItem Header(string title, int unread) =>
        Header(title, unread, "#6B7280", string.Empty);

    public static NotificationFeedItem Header(string title, int unread, string accentColor, string initials) =>
        new()
        {
            IsGroupHeader = true,
            GroupTitle = string.IsNullOrWhiteSpace(title) ? "Account" : title.Trim(),
            GroupUnreadCount = Math.Max(0, unread),
            AccentColor = string.IsNullOrWhiteSpace(accentColor) ? "#6B7280" : accentColor,
            GroupInitials = initials ?? string.Empty
        };

    public static NotificationFeedItem FromAlert(NotificationAlert alert, MessengerInstance? instance) =>
        new()
        {
            Alert = alert,
            IconGlyph = instance?.IconGlyph ?? alert.IconGlyph,
            AccentColor = instance?.AccentColor ?? "#6B7280"
        };
}
