using System.Diagnostics;
using FlaUI.Core;
using FlaUI.UIA3;

namespace UnifiedMessenger.UiSmokeTests;

internal static class Program
{
    public static int Main(string[] args)
    {
        var repoRoot = FindRepoRoot();
        var exePath = ResolveExecutablePath(args, repoRoot);
        if (!File.Exists(exePath))
        {
            Console.Error.WriteLine($"FAIL: executable not found at {exePath}");
            return 1;
        }

        Console.WriteLine("=== Unified Messenger — Full Module Validation ===");
        Console.WriteLine($"Executable: {exePath}");
        Console.WriteLine();

        var allResults = new List<ModuleValidationResult>();

        Console.WriteLine("[Step 1] Structural audit — see report sections below.");
        Console.WriteLine("[Step 2] Unit test suite (background services)...");
        allResults.Add(ModuleValidationHarness.RunUnitTestSuite(repoRoot));
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

        var hardFailures = allResults.Count(result => !result.Passed);
        return hardFailures == 0 ? 0 : 3;
    }

    private static void PrintReport(IReadOnlyList<ModuleValidationResult> results)
    {
        Console.WriteLine();
        Console.WriteLine("=== Validation Report ===");
        foreach (var result in results)
        {
            var status = result.Passed ? "PASS" : "FAIL";
            Console.WriteLine($"[{status}] {result.Layer}/{result.Module}: {result.Detail}");
        }

        var passed = results.Count(result => result.Passed);
        Console.WriteLine();
        Console.WriteLine($"Summary: {passed}/{results.Count} modules passed");

        if (passed == results.Count)
        {
            Console.WriteLine("[ALL MODULES VALIDATED: AWAITING STATUS APPROVAL]");
        }
    }

    private static string ResolveExecutablePath(string[] args, string repoRoot)
    {
        if (args.Length > 0 && File.Exists(args[0]))
        {
            return Path.GetFullPath(args[0]);
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
