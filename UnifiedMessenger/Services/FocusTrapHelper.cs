using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace UnifiedMessenger.Services;

/// <summary>
/// Keeps keyboard Tab navigation within a modal surface and restores focus when disposed.
/// </summary>
public sealed class FocusTrapHelper : IDisposable
{
    private readonly FrameworkElement _root;
    private readonly Control? _previousFocus;
    private readonly KeyEventHandler _keyDownHandler;
    private bool _disposed;

    private FocusTrapHelper(FrameworkElement root, Control? previousFocus)
    {
        _root = root;
        _previousFocus = previousFocus;
        _keyDownHandler = OnKeyDown;
        _root.AddHandler(UIElement.KeyDownEvent, _keyDownHandler, true);
    }

    public static FocusTrapHelper Activate(FrameworkElement root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var previous = FocusManager.GetFocusedElement(root.XamlRoot) as Control;
        return new FocusTrapHelper(root, previous);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _root.RemoveHandler(UIElement.KeyDownEvent, _keyDownHandler);

        if (_previousFocus is { IsEnabled: true, Visibility: Visibility.Visible })
        {
            _ = _previousFocus.Focus(FocusState.Programmatic);
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Tab)
        {
            return;
        }

        var focusables = CollectTabFocusableElements(_root);
        if (focusables.Count == 0)
        {
            return;
        }

        var current = FocusManager.GetFocusedElement(_root.XamlRoot) as Control;
        var currentIndex = current is null ? -1 : focusables.IndexOf(current);
        var shiftHeld = IsShiftHeld();

        Control? nextFocus = null;
        if (currentIndex < 0)
        {
            nextFocus = shiftHeld ? focusables[^1] : focusables[0];
        }
        else if (shiftHeld)
        {
            nextFocus = currentIndex == 0 ? focusables[^1] : focusables[currentIndex - 1];
        }
        else
        {
            nextFocus = currentIndex >= focusables.Count - 1 ? focusables[0] : focusables[currentIndex + 1];
        }

        if (nextFocus is null || ReferenceEquals(nextFocus, current))
        {
            return;
        }

        _ = nextFocus.Focus(FocusState.Keyboard);
        e.Handled = true;
    }

    private static bool IsShiftHeld()
    {
        var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
        return (shiftState & Windows.UI.Core.CoreVirtualKeyStates.Down) ==
               Windows.UI.Core.CoreVirtualKeyStates.Down;
    }

    private static List<Control> CollectTabFocusableElements(DependencyObject root)
    {
        var elements = new List<Control>();
        WalkFocusableTree(root, elements);
        return elements
            .Where(element =>
                element.IsEnabled &&
                element.Visibility == Visibility.Visible &&
                element.IsTabStop)
            .OrderBy(element => element.TabIndex)
            .ThenBy(element => element.GetHashCode())
            .ToList();
    }

    private static void WalkFocusableTree(DependencyObject node, ICollection<Control> elements)
    {
        if (node is Control control)
        {
            elements.Add(control);
        }

        var childCount = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(node);
        for (var index = 0; index < childCount; index++)
        {
            WalkFocusableTree(
                Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(node, index),
                elements);
        }
    }
}
