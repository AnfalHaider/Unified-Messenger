using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace UnifiedMessenger.Controls.Occ;

internal static class OccTeachingTipHelper
{
    public static void ShowTeachingTip(FrameworkElement target, string title, string subtitle)
    {
        ArgumentNullException.ThrowIfNull(target);

        var host = FindTeachingTipHost(target);
        if (host is null)
        {
            return;
        }

        var tip = new TeachingTip
        {
            Title = title,
            Subtitle = subtitle,
            Target = target,
            IsOpen = true
        };

        tip.Closed += (_, _) => host.Children.Remove(tip);
        host.Children.Add(tip);
    }

    private static Panel? FindTeachingTipHost(FrameworkElement target)
    {
        var current = target;
        while (current is not null)
        {
            if (current is Grid grid && current.Parent is UserControl)
            {
                return grid;
            }

            current = VisualTreeHelper.GetParent(current) as FrameworkElement;
        }

        return null;
    }
}
