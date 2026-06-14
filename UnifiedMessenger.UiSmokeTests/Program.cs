using System.Diagnostics;
using FlaUI.Core;
using FlaUI.UIA3;

namespace UnifiedMessenger.UiSmokeTests;

internal static class Program
{
    public static int Main(string[] args)
    {
        var repoRoot = FindRepoRoot();
        var exploreMinutes = ParseExploreMinutes(args, out var occOnly, out var fullApp, out var postImpl, out var detailed, out var filteredArgs);
        var exePath = ResolveExecutablePath(filteredArgs, repoRoot);
        if (!File.Exists(exePath))
        {
            Console.Error.WriteLine($"FAIL: executable not found at {exePath}");
            return 1;
        }

        if (fullApp && exploreMinutes > 0)
        {
            StopExistingInstances();
            string? logName = null;
            string? summaryName = null;
            if (detailed)
            {
                logName = "full-app-10min-detailed-log.txt";
                summaryName = "full-app-10min-detailed-summary.md";
            }
            else if (postImpl)
            {
                logName = "full-app-post-implementation-log.txt";
            }

            return FullAppExploration.Run(exePath, exploreMinutes, logName, summaryName);
        }

        if (occOnly && exploreMinutes > 0)
        {
            StopExistingInstances();
            return OccDetailedExploration.Run(exePath, exploreMinutes);
        }

        if (exploreMinutes > 0)
        {
            StopExistingInstances();
            return InstalledAppExploration.Run(exePath, exploreMinutes);
        }

        Console.WriteLine("=== Unified Messenger 3.7.1 — Release Validation ===");
        Console.WriteLine($"Executable: {exePath}");
        Console.WriteLine();

        var allResults = new List<ModuleValidationResult>();

        Console.WriteLine("[Step 1] Structural audit — see report sections below.");
        StopExistingInstances();

        Console.WriteLine("[Step 2] Domain unit tests (full Release suite)...");
        allResults.AddRange(ModuleValidationHarness.RunDomainUnitTests(repoRoot));

        Console.WriteLine("[Step 3–4] Live UI automation + layout stress...");
        StopExistingInstances();

        FlaUI.Core.Application? app = null;
        try
        {
            app = FlaUI.Core.Application.Launch(exePath);
            using var automation = new UIA3Automation();
            var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(45));
            if (window is null)
            {
                Console.Error.WriteLine("FAIL: main window not found");
                return 2;
            }

            allResults.AddRange(ModuleValidationHarness.RunUiModules(window));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL: UI harness exception: {ex}");
            return 4;
        }
        finally
        {
            try
            {
                app?.Close();
            }
            catch
            {
                // WinUI may minimize to tray.
            }

            StopExistingInstances();
        }

        PrintReport(allResults);

        var hardFailures = allResults.Count(result => result.Severity == ModuleValidationSeverity.Fail);
        return hardFailures == 0 ? 0 : 3;
    }

    private static void PrintReport(IReadOnlyList<ModuleValidationResult> results)
    {
        Console.WriteLine();
        Console.WriteLine("=== Validation Report ===");
        foreach (var result in results)
        {
            var status = result.Severity switch
            {
                ModuleValidationSeverity.Pass => "PASS",
                ModuleValidationSeverity.Warn => "WARN",
                _ => "FAIL"
            };
            Console.WriteLine($"[{status}] {result.Layer}/{result.Module}: {result.Detail}");
        }

        var passed = results.Count(result => result.Severity == ModuleValidationSeverity.Pass);
        var warnings = results.Count(result => result.Severity == ModuleValidationSeverity.Warn);
        var failed = results.Count(result => result.Severity == ModuleValidationSeverity.Fail);
        Console.WriteLine();
        Console.WriteLine(
            $"Summary: {passed} passed, {warnings} warnings, {failed} failed ({results.Count} total)");

        if (failed == 0)
        {
            if (warnings == 0)
            {
                Console.WriteLine("[ALL MODULES VALIDATED: AWAITING STATUS APPROVAL]");
            }
            else
            {
                Console.WriteLine($"[VALIDATION COMPLETE WITH {warnings} WARNING(S)]");
            }
        }
    }

    private static int ParseExploreMinutes(
        string[] args,
        out bool occOnly,
        out bool fullApp,
        out bool postImpl,
        out bool detailed,
        out string[] filteredArgs)
    {
        var list = new List<string>();
        var minutes = 0;
        occOnly = false;
        fullApp = false;
        postImpl = false;
        detailed = false;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--full-app", StringComparison.OrdinalIgnoreCase))
            {
                fullApp = true;
                continue;
            }

            if (args[i].Equals("--detailed", StringComparison.OrdinalIgnoreCase))
            {
                detailed = true;
                continue;
            }

            if (args[i].Equals("--post-impl", StringComparison.OrdinalIgnoreCase))
            {
                postImpl = true;
                continue;
            }

            if (args[i].Equals("--occ-only", StringComparison.OrdinalIgnoreCase))
            {
                occOnly = true;
                continue;
            }

            if (args[i].Equals("--explore", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length &&
                int.TryParse(args[i + 1], out var parsed) && parsed > 0)
            {
                minutes = parsed;
                i++;
                continue;
            }

            list.Add(args[i]);
        }

        filteredArgs = list.ToArray();
        return minutes;
    }

    private static string ResolveExecutablePath(string[] args, string repoRoot)
    {
        if (args.Length > 0 && File.Exists(args[0]))
        {
            return Path.GetFullPath(args[0]);
        }

        var installed = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "UnifiedMessenger",
            "UnifiedMessenger.exe");
        if (File.Exists(installed))
        {
            return installed;
        }

        return Path.Combine(
            repoRoot,
            "UnifiedMessenger",
            "bin",
            "Release",
            "net8.0-windows10.0.19041.0",
            "win-x64",
            "publish",
            "UnifiedMessenger.exe");
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "UnifiedMessenger.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        return Directory.GetCurrentDirectory();
    }

    private static void StopExistingInstances()
    {
        foreach (var process in Process.GetProcessesByName("UnifiedMessenger"))
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            catch
            {
                // Best effort.
            }
        }
    }
}
