using Microsoft.UI.Xaml;
using UnifiedMessenger.Services;

namespace UnifiedMessenger;

public partial class App : Application
{
    public static MainWindow? CurrentWindow { get; private set; }

    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        await AppSettingsService.Instance.LoadAsync();
        ThemeService.Apply(AppSettingsService.Instance.Settings.ThemePreference);

        var notificationService = AppNotificationService.Instance;
        notificationService.Initialize();

        _window = new MainWindow();
        CurrentWindow = _window;
        notificationService.TryHandleLaunchActivation();
        _window.Activate();

        _ = GitHubUpdateService.Instance.CheckForUpdatesAsync();
    }
}
