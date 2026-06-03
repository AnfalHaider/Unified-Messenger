using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.DynamicDependency;

namespace UnifiedMessenger;

public static class Program
{
    private const uint WindowsAppSdkVersion = 0x00020001;

    [STAThread]
    public static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Bootstrap.Initialize(WindowsAppSdkVersion);

        try
        {
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
            Bootstrap.Shutdown();
        }
    }
}
