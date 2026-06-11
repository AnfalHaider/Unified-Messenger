using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using UnifiedMessenger.Services;

namespace UnifiedMessenger;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        RegisterGlobalExceptionHandlers();

        if (!WindowsAppRuntimeBootstrapHelper.TryEnsureInitialized())
        {
            var logPath = Path.Combine(ApplicationPaths.UserDataRoot, "startup.log");
            NativeDialogService.ShowError(
                "Unified Messenger",
                "Could not load the Windows App SDK runtime bundled with this install. " +
                $"Reinstall using the latest UnifiedMessengerSetup.exe. Details: {logPath}");
            return;
        }

        if (!SingleInstanceGuard.TryAcquire())
        {
            if (SecondInstanceActivator.TryActivateExistingInstance())
            {
                Debug.WriteLine("Unified Messenger is already running; restored existing window.");
            }
            else
            {
                Debug.WriteLine("Unified Messenger is already running; could not restore existing window.");
            }

            WindowsAppRuntimeBootstrapHelper.ShutdownIfNeeded();
            return;
        }

        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(_ =>
            {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }
        finally
        {
            SingleInstanceGuard.Release();
            WindowsAppRuntimeBootstrapHelper.ShutdownIfNeeded();
        }
    }

    private static void RegisterGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
            {
                Debug.WriteLine(
                    $"AppDomain unhandled exception (terminating={eventArgs.IsTerminating}): {exception}");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            Debug.WriteLine($"Unobserved task exception: {eventArgs.Exception}");
            eventArgs.SetObserved();
        };
    }
}
