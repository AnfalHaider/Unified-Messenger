using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using UnifiedMessenger.Services;

namespace UnifiedMessenger;

public sealed partial class MainWindow
{
    private void SidebarHost_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_shell.Chrome.PanePinned || SidebarColumn.Width.Value <= 0)
        {
            return;
        }

        _shell.Chrome.SidebarHoverExpanded = true;
        _shell.Chrome.ApplySidebarLayout(forceVisible: true);
    }

    private void SidebarHost_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_shell.Chrome.PanePinned || SidebarColumn.Width.Value <= 0)
        {
            return;
        }

        _shell.Chrome.SidebarHoverExpanded = false;
        _shell.Chrome.ApplySidebarLayout(forceVisible: true);
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        _shell.Chrome.IsAppInForeground = MainWindowShellLayout.IsAppInForeground(
            AppWindow.IsVisible,
            args.WindowActivationState != WindowActivationState.Deactivated);
        _shell.ApplyWindowVisibilityState();
        _shell.OnForegroundStateChanged();
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidVisibilityChange)
        {
            return;
        }

        _shell.Chrome.IsAppInForeground = MainWindowShellLayout.IsAppInForeground(
            sender.IsVisible,
            _shell.Chrome.IsAppInForeground);
        _shell.ApplyWindowVisibilityState();
        _shell.OnForegroundStateChanged();
    }
}
