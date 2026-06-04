using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace UnifiedMessenger;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        RegisterGlobalExceptionHandlers();

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            var context = new DispatcherQueueSynchronizationContext(
                DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
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
