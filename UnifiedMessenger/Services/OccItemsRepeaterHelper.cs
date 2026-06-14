using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using UnifiedMessenger.Controls;

namespace UnifiedMessenger.Services;

/// <summary>
/// Wires thread-card interaction and scroll bubbling for OCC <see cref="ItemsRepeater"/> lists.
/// </summary>
public static class OccItemsRepeaterHelper
{
    public static void WireThreadCards(
        ItemsRepeater repeater,
        ScrollViewer? scrollParent,
        Action<OperationsThreadCardViewModel> onCardActivated)
    {
        ArgumentNullException.ThrowIfNull(repeater);
        ArgumentNullException.ThrowIfNull(onCardActivated);

        repeater.ElementPrepared += (_, args) =>
        {
            if (args.Element is not FrameworkElement element)
            {
                return;
            }

            if (scrollParent is not null)
            {
                ScrollInputHelper.EnableVerticalScrollBubbling(element, scrollParent);
            }

            element.IsTapEnabled = true;
            element.Tapped -= OnCardTapped;
            element.Tapped += OnCardTapped;

            void OnCardTapped(object sender, TappedRoutedEventArgs e)
            {
                if (sender is FrameworkElement source &&
                    source.DataContext is OperationsThreadCardViewModel card)
                {
                    onCardActivated(card);
                }
            }
        };
    }
}
