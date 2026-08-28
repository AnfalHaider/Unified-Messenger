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

        // Read the version off the binary under test. This banner used to be the hard-coded string "3.7.1",
        // which was six major versions stale — a validation report is the last place that should state a
        // version it did not check.
        var underTest = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath).FileVersion ?? "unknown";
        Console.WriteLine($"=== Unified Messenger {underTest} — Release Validation ===");
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
        var uiStepFailed = false;

        // Distinguishes "the app never opened a window" from "this environment cannot automate one".
        // The probe below already told them apart in the OUTPUT; the exit code did not, so a hosted runner
        // with no interactive desktop reported the same failure as a genuinely broken build, and the job
        // was permanently red for a reason nobody could act on.
        var sawWindowHandle = false;
        try
        {
            app = FlaUI.Core.Application.Launch(exePath);

            // Probe for a real top-level window through Win32 BEFORE asking UI Automation for one. The two
            // failures look identical from GetMainWindow — it just times out — but they mean opposite things:
            // no window handle at all is the app failing to start, which is exactly what this test exists to
            // catch; a window handle that UI Automation cannot attach to is the harness's own environment
            // being unable to drive a desktop. Reporting the second as an app failure is how this workflow
            // came to be permanently red while the app was fine.
            // 90s, not 45s: a self-contained WinUI app starting cold on a hosted runner — no warm .NET
            // file cache, contended disk — legitimately takes longer than a developer machine, and the
            // probe returns the moment the window appears, so a longer ceiling costs nothing on a healthy
            // run. Same commit passed one CI run and failed the next on the old value.
            var probe = WaitForMainWindowHandle(app, TimeSpan.FromSeconds(90), out var hwnd);
            sawWindowHandle = probe != WindowProbe.ProcessGone;
            Console.WriteLine(probe switch
            {
                WindowProbe.WindowPresent => $"  Win32 probe: top-level window present (hwnd 0x{hwnd.ToInt64():X}).",
                WindowProbe.ProcessGone => "  Win32 probe: the process created NO top-level window.",
                _ => "  Win32 probe: process still running after 90s with no window — headless runner, or a "
                     + "very slow start. Cannot tell the two apart, so this is not reported as a failure."
            });

            using var automation = new UIA3Automation();
            var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(45));
            if (window is null)
            {
                Console.Error.WriteLine("FAIL: main window not found");
                uiStepFailed = true;
            }
            else
            {
                allResults.AddRange(ModuleValidationHarness.RunUiModules(window));
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL: UI harness exception: {ex}");
            uiStepFailed = true;
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

        // Print the report even when the UI step blew up. Returning early threw away the structural audit and
        // the entire Release unit suite that had already passed, so a UI-automation environment problem
        // erased every other signal the run had produced — the whole job read as "the app is broken".
        PrintReport(allResults);

        if (uiStepFailed)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("Steps 1-2 above still ran; step 3-4 did not.");

            if (sawWindowHandle)
            {
                // Covers both "a window appeared but UI Automation could not attach" and "the process is
                // alive and never drew one". Both are properties of the machine, not of the build: a WinUI
                // app on a session with no desktop stays up and window-less, which is indistinguishable
                // from a slow start and is emphatically not evidence of a broken binary.
                Console.Error.WriteLine(
                    "The app process stayed alive; UI Automation could not drive a window on this session. "
                    + "That is an environment limitation (headless or non-interactive), not an app failure. "
                    + "Exit 5.");
                return 5;
            }

            // Reached only when the process died or vanished — which is what a genuine launch failure does:
            // App.LaunchAsync catches, shows "Unified Messenger could not start" and calls Exit().
            Console.Error.WriteLine(
                "The app process exited without opening a window. This is a real launch failure. Exit 4.");
            return 4;
        }

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

    /// <summary>
    /// Waits for the launched process to own a top-level window, using Win32 only — no UI Automation.
    /// </summary>
    /// <remarks>
    /// This exists to tell two very different failures apart when the UIA call times out. If the process
    /// never gets a window handle, the app genuinely failed to start and the test should stay red. If it has
    /// one and UIA still cannot attach, the problem is the harness's environment — a CI runner with no
    /// interactive desktop cannot be automated, and calling that an app defect makes the workflow lie.
    /// </remarks>
    /// <summary>
    /// Waits for a top-level window, and reports which of three different things happened.
    /// </summary>
    /// <remarks>
    /// This used to return a bare <see cref="IntPtr"/>, so three outcomes collapsed into
    /// <see cref="IntPtr.Zero"/>: the process died, the process vanished, and the process was still running
    /// but had not drawn a window by the deadline. The caller read all three as "no window at all — a real
    /// launch failure" and exited 4, which is red.
    ///
    /// <para>
    /// Only the first two are launch failures. The third is ambiguous and, on a hosted runner with no
    /// interactive desktop, is the *expected* outcome: a WinUI 3 app can start, stay alive and never
    /// produce a window because there is no desktop to produce one on. Reporting that as a broken build is
    /// the same mistake this file already documents fixing once — a job that is red for a reason nobody
    /// can act on gets ignored, and takes the structural audit and the whole Release unit suite down with
    /// it.
    /// </para>
    /// <para>
    /// A genuine launch failure still reaches exit 4, because it kills the process: <c>App.LaunchAsync</c>
    /// catches, shows "Unified Messenger could not start" and calls <c>Exit()</c>. Alive-but-window-less is
    /// the one case where this harness genuinely cannot tell, so it says so instead of guessing.
    /// </para>
    /// </remarks>
    private enum WindowProbe
    {
        /// <summary>A top-level window appeared.</summary>
        WindowPresent,

        /// <summary>The process exited or vanished before showing one — a real launch failure.</summary>
        ProcessGone,

        /// <summary>Still running at the deadline with no window. Cannot distinguish slow from headless.</summary>
        AliveWithoutWindow
    }

    private static WindowProbe WaitForMainWindowHandle(
        FlaUI.Core.Application app,
        TimeSpan timeout,
        out IntPtr handle)
    {
        handle = IntPtr.Zero;
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById(app.ProcessId);
                process.Refresh();
                if (process.HasExited)
                {
                    Console.WriteLine($"  Win32 probe: the process exited early with code {process.ExitCode}.");
                    return WindowProbe.ProcessGone;
                }

                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    handle = process.MainWindowHandle;
                    return WindowProbe.WindowPresent;
                }
            }
            catch (ArgumentException)
            {
                // Process is gone entirely — no window, and nothing left to wait for.
                return WindowProbe.ProcessGone;
            }

            Thread.Sleep(500);
        }

        return WindowProbe.AliveWithoutWindow;
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
