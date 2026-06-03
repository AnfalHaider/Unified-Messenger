using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Models;
using Windows.UI;

namespace UnifiedMessenger.Services;

public static class PlatformBrandingHelper
{
    public static SolidColorBrush GetAccentBrush(MessengerInstance instance)
    {
        return new SolidColorBrush(ParseColor(instance.AccentColor));
    }

    public static SolidColorBrush GetAccentBrush(string accentColorHex)
    {
        return new SolidColorBrush(ParseColor(accentColorHex));
    }

    public static UIElement CreatePlatformIcon(MessengerInstance instance, double size = 16)
    {
        var brush = GetAccentBrush(instance);
        var host = new Microsoft.UI.Xaml.Controls.Grid
        {
            Width = size,
            Height = size
        };

        var circle = new Microsoft.UI.Xaml.Shapes.Ellipse
        {
            Width = size,
            Height = size,
            Fill = brush
        };

        var glyph = new Microsoft.UI.Xaml.Controls.FontIcon
        {
            Glyph = instance.IconGlyph,
            FontSize = size * 0.55,
            Foreground = new SolidColorBrush(Colors.White),
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center
        };

        host.Children.Add(circle);
        host.Children.Add(glyph);
        return host;
    }

    public static string GetInitials(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "?";
        }

        var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
        }

        return displayName.Length >= 2
            ? displayName[..2].ToUpperInvariant()
            : displayName.ToUpperInvariant();
    }

    private static Color ParseColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return Color.FromArgb(255, 107, 114, 128);
        }

        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            hex = "FF" + hex;
        }

        if (uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var value))
        {
            return Color.FromArgb(
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF));
        }

        return Color.FromArgb(255, 107, 114, 128);
    }
}
