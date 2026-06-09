using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace UnifiedMessenger.Controls.Occ;

internal static class OccBrushHelper
{
    public static SolidColorBrush ResolveThemeBrush(string resourceKey, Color fallback) =>
        Application.Current.Resources.TryGetValue(resourceKey, out var resource) && resource is SolidColorBrush brush
            ? brush
            : new SolidColorBrush(fallback);

    public static SolidColorBrush CreateBrush(string hex) =>
        new(ColorFromHex(hex));

    public static Color ColorFromHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6)
        {
            return Colors.Gray;
        }

        return Color.FromArgb(
            255,
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16));
    }
}
