using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

public sealed class NotificationFeedTemplateSelector : DataTemplateSelector
{
    public DataTemplate? HeaderTemplate { get; set; }

    public DataTemplate? AlertTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item) =>
        item is NotificationFeedItem { IsGroupHeader: true }
            ? HeaderTemplate!
            : AlertTemplate!;
}
