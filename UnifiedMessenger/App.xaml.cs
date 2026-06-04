using System.Diagnostics;
using Microsoft.UI.Xaml;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Ollama;

namespace UnifiedMessenger;

public partial class App : Application
{
    public static MainWindow? CurrentWindow { get; private set; }

    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _ = LaunchAsync(args);
    }

    private async Task LaunchAsync(LaunchActivatedEventArgs args)
    {
        try
        {
            await AppSettingsService.Instance.LoadAsync().ConfigureAwait(true);

            StartupTaskService.EnsureRegistrationMatchesPreference(
                AppSettingsService.Instance.Settings.LaunchAtStartup);

            ThemeService.ApplyInitialLaunchTheme(AppSettingsService.Instance.Settings.ThemePreference);

            var notificationService = AppNotificationService.Instance;
            notificationService.Initialize();

            _window = new MainWindow();
            CurrentWindow = _window;

            ThemeService.Apply(AppSettingsService.Instance.Settings.ThemePreference);

            notificationService.TryHandleLaunchActivation();
            _window.Activate();

            await _window.RunInitializationAsync().ConfigureAwait(true);

            if (AppSettingsService.Instance.Settings.EnableAutoUpdate)
            {
                _ = GitHubUpdateService.Instance.CheckForUpdatesAsync();
            }

            OllamaOrchestrationService.Instance.WarmupInBackground();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Application launch failed: {ex}");
            CurrentWindow?.Close();
            Exit();
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs eventArgs)
    {
        Debug.WriteLine($"Unhandled XAML exception: {eventArgs.Message}");

        if (eventArgs.Exception is null)
        {
            return;
        }

        Debug.WriteLine(eventArgs.Exception.ToString());

        // Leave Handled=false so the process can terminate on non-recoverable faults.
    }
}
