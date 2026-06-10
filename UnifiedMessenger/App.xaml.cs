using System.Diagnostics;
using Microsoft.UI.Xaml;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Ollama;

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
        _ = LaunchAsync(args);
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

            ThemeService.ApplyInitialLaunchTheme(services.AppSettings.Settings.ThemePreference);

            services.AppNotification.Initialize();

            _window = new MainWindow();
            CurrentWindow = _window;

            ThemeService.Apply(services.AppSettings.Settings.ThemePreference);

            services.AppNotification.TryHandleLaunchActivation();
            _window.Activate();

            await _window.RunInitializationAsync().ConfigureAwait(true);

            if (services.AppSettings.Settings.EnableAutoUpdate)
            {
                _ = services.GitHubUpdate.CheckForUpdatesAsync();
            }

            services.Ollama.WarmupInBackground();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Application launch failed: {ex}");
            StartupDiagnostics.Log($"Application launch failed: {ex}");
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
