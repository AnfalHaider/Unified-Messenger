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
        _ = LaunchAsync(args);
    }

    private async Task LaunchAsync(LaunchActivatedEventArgs args)
    {
        try
        {
            var services = ApplicationServices.CreateDefault();
            ApplicationServiceProvider.Set(services);

            // Create the window on the UI thread before any await — WinRT/file IO resumes on pool threads.
            _window = new MainWindow();
            CurrentWindow = _window;

            await services.AppSettings.LoadAsync().ConfigureAwait(true);

            StartupTaskService.EnsureRegistrationMatchesPreference(
                services.AppSettings.Settings.LaunchAtStartup);

            ThemeService.ApplyInitialLaunchTheme(services.AppSettings.Settings.ThemePreference);

            services.AppNotification.Initialize();

            ThemeService.Apply(services.AppSettings.Settings.ThemePreference);

            services.AppNotification.TryHandleLaunchActivation();
            _window.Activate();

            await _window.RunInitializationAsync().ConfigureAwait(true);

            if (services.AppSettings.Settings.EnableAutoUpdate)
            {
                _ = services.GitHubUpdate.CheckForUpdatesAsync();
            }

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
