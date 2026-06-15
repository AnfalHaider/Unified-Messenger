using System.Diagnostics;
using Microsoft.UI.Xaml;
using UnifiedMessenger.Services;
namespace UnifiedMessenger;

public partial class App : Application
{
    public static MainWindow? CurrentWindow { get; private set; }

    public static ApplicationServices Services => ApplicationServiceProvider.Current;

    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        LaunchAsync(args).ContinueWith(
            t => AppLogger.LogError("App.OnLaunched", t.Exception!),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    private async Task LaunchAsync(LaunchActivatedEventArgs args)
    {
        try
        {
            var services = ApplicationServices.CreateDefault();
            ApplicationServiceProvider.Set(services);

            await services.AppSettings.LoadAsync().ConfigureAwait(true);

            StartupTaskService.EnsureRegistrationMatchesPreference(
                services.AppSettings.Settings.LaunchAtStartup);

            // Application.RequestedTheme must be set before the first Window is created (WinUI COM 0x80131515).
            ThemeService.ApplyInitialLaunchTheme(services.AppSettings.Settings.ThemePreference);

            _window = new MainWindow();
            CurrentWindow = _window;

            services.AppNotification.Initialize();

            ThemeService.Apply(services.AppSettings.Settings.ThemePreference);

            services.AppNotification.TryHandleLaunchActivation();
            _window.Activate();

            await _window.RunInitializationAsync().ConfigureAwait(true);

            if (services.AppSettings.Settings.EnableAutoUpdate)
            {
                _ = services.GitHubUpdate.CheckForUpdatesAsync();
            }

            if (services.AppSettings.Settings.EnableLocalAi)
            {
                services.OllamaRuntime.WarmupInBackground();
            }

        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Application launch failed: {ex}");
            AppLogger.LogError("App.Launch", ex);
            NativeDialogService.ShowError(
                "Unified Messenger",
                $"The application could not start.\n\n{ex.Message}");
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
