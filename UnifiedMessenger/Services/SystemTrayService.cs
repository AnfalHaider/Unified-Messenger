using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using UnifiedMessenger.Services.Ollama;

namespace UnifiedMessenger.Services;

public sealed class SystemTrayService : ISystemTrayService
{
    private static readonly Lazy<SystemTrayService> LazyInstance = new(() => new SystemTrayService());

    private TaskbarIcon? _trayIcon;
    private MainWindow? _window;
    private bool _disposed;
    private bool _copilotBusy;

    private SystemTrayService()
    {
    }

    public static SystemTrayService Instance => LazyInstance.Value;

    public void Attach(MainWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (_trayIcon is not null)
        {
            _window = window;
            return;
        }

        _window = window;

        var defaultIcon = CreateIconSource("AppIcon.ico");
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Unified Messenger",
            IconSource = defaultIcon
        };

        var menu = new MenuFlyout();
        var openItem = new MenuFlyoutItem { Text = "Open" };
        openItem.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(openItem);

        var quitItem = new MenuFlyoutItem { Text = "Quit" };
        quitItem.Click += (_, _) => TrayMenu_Quit();
        menu.Items.Add(quitItem);

        _trayIcon.ContextFlyout = menu;
        _trayIcon.DoubleClickCommand = new TrayActionCommand(ShowMainWindow);

        OllamaInferenceCoordinator.Instance.ActivityChanged += OnInferenceActivityChanged;
        UpdateTrayPresentation();
        _trayIcon.ForceCreate(enablesEfficiencyMode: false);
    }

    /// <summary>
    /// Full process exit — the only path that bypasses hide-on-close.
    /// </summary>
    public void TrayMenu_Quit()
    {
        if (_window is null)
        {
            return;
        }

        _window.RequestTrayQuit();
    }

    public void ShowMainWindow()
    {
        if (_window is null)
        {
            return;
        }

        _window.ShowFromTray();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        OllamaInferenceCoordinator.Instance.ActivityChanged -= OnInferenceActivityChanged;

        if (_trayIcon is not null)
        {
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        _window = null;
    }

    private void OnInferenceActivityChanged(object? sender, OllamaInferenceActivity activity)
    {
        _copilotBusy = activity == OllamaInferenceActivity.InteractiveStreaming;
        UpdateTrayPresentation();
    }

    private void UpdateTrayPresentation()
    {
        if (_trayIcon is null)
        {
            return;
        }

        var iconFile = _copilotBusy ? "AppIconTrayAi.ico" : "AppIcon.ico";
        _trayIcon.IconSource = CreateIconSource(iconFile);
        _trayIcon.ToolTipText = _copilotBusy
            ? "Unified Messenger — AI drafting…"
            : "Unified Messenger";
    }

    private static ImageSource CreateIconSource(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
        if (!File.Exists(path))
        {
            path = ApplicationPaths.TryResolveAppIconFilePath() ?? path;
        }

        return new BitmapImage(new Uri(path));
    }
}
