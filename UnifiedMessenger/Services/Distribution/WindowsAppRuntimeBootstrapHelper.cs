using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace UnifiedMessenger.Services;

/// <summary>
/// Ensures the Windows App SDK native runtime is loaded for self-contained unpackaged WinUI builds.
/// Framework-dependent dev builds rely on the SDK's auto-bootstrap module initializer instead.
/// </summary>
internal static class WindowsAppRuntimeBootstrapHelper
{
    public static bool TryEnsureInitialized()
    {
        if (!IsSelfContainedDeployment())
        {
            return true;
        }

        try
        {
            Environment.SetEnvironmentVariable(
                "MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY",
                AppContext.BaseDirectory);

            var hr = WindowsAppRuntimeNative.EnsureIsLoaded();
            if (hr < 0)
            {
                Debug.WriteLine($"WindowsAppRuntime_EnsureIsLoaded failed: 0x{hr:X8}");
                StartupDiagnostics.Log($"WindowsAppRuntime_EnsureIsLoaded failed: 0x{hr:X8}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Windows App Runtime load failed: {ex}");
            StartupDiagnostics.Log($"Windows App Runtime load failed: {ex}");
            return false;
        }
    }

    public static void ShutdownIfNeeded()
    {
        // Self-contained unpackaged apps do not use MddBootstrap shutdown.
    }

    internal static bool IsSelfContainedDeployment() =>
        IsSelfContainedDeployment(AppContext.BaseDirectory);

    internal static bool IsSelfContainedDeployment(string baseDirectory)
    {
        var configPath = Path.Combine(baseDirectory, "UnifiedMessenger.runtimeconfig.json");
        if (!File.Exists(configPath))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(configPath));
            return document.RootElement
                .GetProperty("runtimeOptions")
                .TryGetProperty("includedFrameworks", out _);
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"Could not read runtime config: {ex.Message}");
            return false;
        }
    }

    private static class WindowsAppRuntimeNative
    {
        [DllImport("Microsoft.WindowsAppRuntime.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal static extern int WindowsAppRuntime_EnsureIsLoaded();

        internal static int EnsureIsLoaded() => WindowsAppRuntime_EnsureIsLoaded();
    }
}
