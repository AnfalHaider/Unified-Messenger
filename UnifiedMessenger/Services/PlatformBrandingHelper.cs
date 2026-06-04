using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Models;
using Windows.UI;

namespace UnifiedMessenger.Services;

public static class PlatformBrandingHelper
{
    public const string DefaultAccentHex = "#6B7280";

    private static readonly Color DefaultAccentColor = Color.FromArgb(255, 107, 114, 128);

    public static SolidColorBrush GetAccentBrush(MessengerInstance? instance)
    {
        return new SolidColorBrush(ParseAccentColor(instance?.AccentColor));
    }

    public static SolidColorBrush GetAccentBrush(string? accentColorHex)
    {
        return new SolidColorBrush(ParseAccentColor(accentColorHex));
    }

    public static UIElement CreatePlatformIcon(MessengerInstance instance, double size = 16)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Icon size must be greater than zero.");
        }

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

    public static string GetInitials(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "?";
        }

        var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2)
        {
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
        }

        var single = parts[0];
        return single.Length >= 2
            ? single[..2].ToUpperInvariant()
            : single.ToUpperInvariant();
    }

    internal static Color ParseAccentColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return DefaultAccentColor;
        }

        hex = hex.Trim().TrimStart('#');
        if (hex.Length == 3)
        {
            hex = string.Concat(hex.Select(character => $"{character}{character}"));
        }

        if (hex.Length == 6)
        {
            hex = "FF" + hex;
        }

        if (hex.Length != 8
            || !uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var value))
        {
            return DefaultAccentColor;
        }

        return Color.FromArgb(
            (byte)((value >> 24) & 0xFF),
            (byte)((value >> 16) & 0xFF),
            (byte)((value >> 8) & 0xFF),
            (byte)(value & 0xFF));
    }
}
