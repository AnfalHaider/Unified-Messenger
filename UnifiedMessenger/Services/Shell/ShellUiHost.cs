using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Controls;

namespace UnifiedMessenger.Services.Shell;

/// <summary>
/// UI surface the shell coordinators mutate. Implemented by <see cref="MainWindow"/>.
/// </summary>
public interface IShellUiHost
{
    DispatcherQueue DispatcherQueue { get; }

    XamlRoot XamlRoot { get; }

    Grid InstanceWebViewHost { get; }

    Grid ShellLayoutGrid { get; }

    Frame ContentFrame { get; }

    WorkspaceSidebar WorkspaceSidebar { get; }

    NotificationFeedPanel NotificationPanel { get; }

    TitleBar AppTitleBar { get; }

    Button NotificationToggleButton { get; }

    ColumnDefinition SidebarColumn { get; }

    ColumnDefinition NotificationColumn { get; }

    RowDefinition NotificationRow { get; }

    StackPanel InstanceLoadingPanel { get; }

    ProgressBar StartupWarmProgressBar { get; }

    TextBlock InstanceLoadingText { get; }

    void ActivateWindow();

    void ShowAppWindow();
}
