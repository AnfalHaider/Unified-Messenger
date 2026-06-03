using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using UnifiedMessenger.Models;
using Windows.UI;

namespace UnifiedMessenger.Services;

public static class ProfileAvatarService
{
    public static FrameworkElement CreateAvatar(MessengerInstance instance, double size = 28)
    {
        var brush = PlatformBrandingHelper.GetAccentBrush(instance);
        var host = new Grid
        {
            Width = size,
            Height = size
        };

        host.Children.Add(new Ellipse
        {
            Width = size,
            Height = size,
            Fill = brush
        });

        host.Children.Add(new TextBlock
        {
            Text = PlatformBrandingHelper.GetInitials(instance.DisplayName),
            FontSize = size * 0.38,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });

        return host;
    }
}
