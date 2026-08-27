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
                AppLogger.LogInfo("SingleInstance", "Already running; restored the existing window.");
            }
            else
            {
                // The symptom the owner sees is "I clicked the icon and nothing happened". Worth a real
                // log line: it is also what a hung shutdown holding the mutex looks like from outside.
                AppLogger.LogWarning(
                    "SingleInstance",
                    "Already running, but the existing window could not be restored; this launch exited with no window.");
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
                // The last thing written before the process dies. Reporting it only to a debugger meant a
                // crash on a customer's machine left no trace at all in the one file support asks for.
                AppLogger.LogError(
                    $"AppDomain.Unhandled(terminating={eventArgs.IsTerminating})",
                    exception);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            // LogError, not LogWarning: this is a full exception with a stack trace, and warnings are
            // flattened to a single line on the way out.
            AppLogger.LogError("UnobservedTask", eventArgs.Exception);
            eventArgs.SetObserved();
        };
    }
}
