using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace UnifiedMessenger.Services;

/// <summary>
/// Semantic brushes resolved from <c>Themes/Tokens.xaml</c> resource keys.
/// </summary>
public static class UmSemanticBrushes
{
    public const string StatusSuccessBrushKey = "UmStatusSuccessBrush";
    public const string StatusWarningBrushKey = "UmStatusWarningBrush";
    public const string StatusDangerBrushKey = "UmStatusDangerBrush";
    public const string StatusNeutralBrushKey = "UmStatusNeutralBrush";
    public const string StatusMutedBrushKey = "UmStatusMutedBrush";
    public const string TransparentBrushKey = "UmTransparentBrush";

    public static SolidColorBrush StatusSuccess => Get(StatusSuccessBrushKey);
    public static SolidColorBrush StatusWarning => Get(StatusWarningBrushKey);
    public static SolidColorBrush StatusDanger => Get(StatusDangerBrushKey);
    public static SolidColorBrush StatusNeutral => Get(StatusNeutralBrushKey);
    public static SolidColorBrush StatusMuted => Get(StatusMutedBrushKey);
    public static SolidColorBrush Transparent => Get(TransparentBrushKey);

    public static SolidColorBrush Get(string resourceKey)
    {
        if (Application.Current.Resources.TryGetValue(resourceKey, out var resource) &&
            resource is SolidColorBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Colors.Gray);
    }
}
