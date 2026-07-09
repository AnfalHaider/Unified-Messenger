using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnifiedMessenger.Services;

/// <summary>
/// Restores <see cref="ScrollViewer"/> offsets after responsive visual-state reflows.
/// </summary>
public static class ScrollOffsetPreservationHelper
{
    public static void Attach(VisualStateGroup group, ScrollViewer scrollViewer)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(scrollViewer);

        group.CurrentStateChanged += (_, _) => PreserveAfterLayout(scrollViewer);
    }

    internal static void PreserveAfterLayout(ScrollViewer scrollViewer)
    {
        var vertical = scrollViewer.VerticalOffset;
        var horizontal = scrollViewer.HorizontalOffset;

        scrollViewer.DispatcherQueue.TryEnqueue(() =>
        {
            scrollViewer.UpdateLayout();
            if (scrollViewer.ScrollableHeight > 0 || scrollViewer.ScrollableWidth > 0)
            {
                scrollViewer.ChangeView(horizontal, vertical, null, disableAnimation: true);
            }
        });
    }
}
