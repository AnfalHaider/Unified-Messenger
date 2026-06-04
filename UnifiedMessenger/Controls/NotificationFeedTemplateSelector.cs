using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Controls;

public sealed class NotificationFeedTemplateSelector : DataTemplateSelector
{
    public DataTemplate? HeaderTemplate { get; set; }

    public DataTemplate? AlertTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item) =>
        item switch
        {
            NotificationFeedItem { IsGroupHeader: true } => HeaderTemplate!,
            NotificationFeedAlertRow => AlertTemplate!,
            _ => AlertTemplate!
        };
}
