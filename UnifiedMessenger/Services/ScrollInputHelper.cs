using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace UnifiedMessenger.Services;

/// <summary>
/// Bubbles vertical mouse-wheel input to a parent <see cref="ScrollViewer"/> when nested
/// controls (ListView, horizontal ScrollViewer) cannot consume the scroll delta.
/// </summary>
public static class ScrollInputHelper
{
    public static void EnableVerticalScrollBubbling(FrameworkElement child, ScrollViewer parentScrollViewer)
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(parentScrollViewer);

        child.AddHandler(
            UIElement.PointerWheelChangedEvent,
            new PointerEventHandler((sender, args) => OnPointerWheelChanged(sender, args, parentScrollViewer)),
            handledEventsToo: true);
    }

    internal static bool ShouldBubbleToParent(FrameworkElement source, int wheelDelta)
    {
        if (wheelDelta == 0)
        {
            return false;
        }

        var innerScrollViewer = FindNearestScrollViewer(source);
        if (innerScrollViewer is null || innerScrollViewer.ScrollableHeight <= 0)
        {
            return true;
        }

        var scrollingUp = wheelDelta > 0;
        var atTop = innerScrollViewer.VerticalOffset <= 0;
        var atBottom = innerScrollViewer.VerticalOffset >= innerScrollViewer.ScrollableHeight;

        return (scrollingUp && atTop) || (!scrollingUp && atBottom);
    }

    internal static double ComputeBubbledOffset(ScrollViewer parent, int wheelDelta) =>
        ComputeBubbledOffset(parent.VerticalOffset, parent.ScrollableHeight, wheelDelta);

    internal static double ComputeBubbledOffset(double currentOffset, double scrollableHeight, int wheelDelta) =>
        Math.Clamp(currentOffset - wheelDelta, 0, scrollableHeight);

    private static void OnPointerWheelChanged(
        object sender,
        PointerRoutedEventArgs args,
        ScrollViewer parentScrollViewer)
    {
        if (args.Handled)
        {
            return;
        }

        var delta = args.GetCurrentPoint(null).Properties.MouseWheelDelta;
        if (delta == 0)
        {
            return;
        }

        if (sender is not FrameworkElement element)
        {
            return;
        }

        if (!ShouldBubbleToParent(element, delta))
        {
            return;
        }

        if (parentScrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        parentScrollViewer.ChangeView(
            null,
            ComputeBubbledOffset(parentScrollViewer.VerticalOffset, parentScrollViewer.ScrollableHeight, delta),
            null,
            disableAnimation: true);
        args.Handled = true;
    }

    private static ScrollViewer? FindNearestScrollViewer(DependencyObject start)
    {
        for (var current = start; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is ScrollViewer scrollViewer)
            {
                return scrollViewer;
            }
        }

        return null;
    }
}
