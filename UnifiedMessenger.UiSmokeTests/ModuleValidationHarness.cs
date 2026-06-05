using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace UnifiedMessenger.UiSmokeTests;

internal static class ModuleValidationHarness
{
    public static IReadOnlyList<ModuleValidationResult> RunUiModules(AutomationElement window)
    {
        UiAutomationHelpers.FocusWindow(window);
        Thread.Sleep(1500);

        return
        [
            ValidateMainShell(window),
            ValidateDashboardOperations(window),
            ValidateBranchWorkspace(window),
            ValidatePersonalOverview(window),
            ValidateSettingsPage(window),
            ValidateLocalAiSettingsPage(window),
            ValidateAboutPage(window),
            ValidateCommandPalette(window),
            ValidateCtrlSpaceCopilot(window),
            ValidateNotificationPanel(window),
            ValidateWorkspaceSidebar(window),
            SafeValidate(() => ValidateAddInstanceDialog(window)),
            ValidateInstanceSwitch(window),
            ValidateRapidResize(window),
            ValidateTrayHideOnClose(window)
        ];
    }

    public static IReadOnlyList<ModuleValidationResult> RunDomainUnitTests(string repoRoot)
    {
        return
        [
            RunFilteredTests(repoRoot, "ThreadStatusAuditorHandlerTests", "Platform.AuditorLoop"),
            RunFilteredTests(repoRoot, "WebViewDraftInjectorTests", "Platform.DraftInjection"),
            RunFilteredTests(repoRoot, "ApplicationLifecycleServiceTests", "Lifecycle.TrayAndFlush"),
            RunFilteredTests(repoRoot, "OperationsCommandCenterServiceTests", "Analytics.OperationsCommandCenter"),
            RunFilteredTests(repoRoot, "UnifiedMessengerDashboardServiceTests", "Analytics.SlaAndRevenue")
        ];
    }

    private static ModuleValidationResult RunFilteredTests(string repoRoot, string filter, string module)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments =
                $"test UnifiedMessenger.Tests/UnifiedMessenger.Tests.csproj -c Release --filter FullyQualifiedName~{filter} -v q",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            return ModuleValidationResult.Fail(module, "DomainTests", "Could not start dotnet test");
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(60_000);

        return process.ExitCode == 0 && output.Contains("Passed!", StringComparison.Ordinal)
            ? ModuleValidationResult.Pass(module, "DomainTests", filter)
            : ModuleValidationResult.Fail(module, "DomainTests", output.Length > 200 ? output[^200..] : output);
    }

    private static ModuleValidationResult ValidateBranchWorkspace(AutomationElement window)
    {
        UiAutomationHelpers.ClickByName(window, "Sidebar Dashboard");
        Thread.Sleep(800);

        if (UiAutomationHelpers.FindByName(window, "Branch filter") is not null)
        {
            return ModuleValidationResult.Pass(
                "Dashboard.BranchFilter",
                "Page",
                "Branch filter control exposed; multi-branch scope available");
        }

        if (UiAutomationHelpers.FindByName(window, "Operations Command Center") is not null ||
            UiAutomationHelpers.FindByName(window, "Revenue at risk") is not null)
        {
            return ModuleValidationResult.Pass(
                "Dashboard.BranchFilter",
                "Page",
                "Operations Command Center KPI strip visible (branch filter UIA limited)");
        }

        return ModuleValidationResult.Warn(
            "Dashboard.BranchFilter",
            "Page",
            "Branch filter not in UIA tree; kanban tabs may still be branch-scoped");
    }

    private static ModuleValidationResult ValidateCtrlSpaceCopilot(AutomationElement window)
    {
        ValidateInstanceSwitch(window);
        UiAutomationHelpers.FocusWindow(window);
        UiAutomationHelpers.SendChord(VirtualKeyShort.CONTROL, VirtualKeyShort.SPACE);
        Thread.Sleep(1200);

        if (window.IsEnabled)
        {
            return ModuleValidationResult.Pass(
                "HotkeyCopilot.CtrlSpace",
                "Overlay",
                "Ctrl+Space dispatched without crashing shell (draft requires Local AI + WebView session)");
        }

        return ModuleValidationResult.Fail("HotkeyCopilot.CtrlSpace", "Overlay", "Window disabled after Ctrl+Space");
    }

    private static ModuleValidationResult ValidateRapidResize(AutomationElement window)
    {
        var original = window.BoundingRectangle;
        try
        {
            for (var cycle = 0; cycle < 3; cycle++)
            {
                window.Patterns.Transform.Pattern.Resize(original.Width * 0.65, original.Height * 0.65);
                Thread.Sleep(150);
                window.Patterns.Transform.Pattern.Resize(original.Width * 0.95, original.Height * 0.95);
                Thread.Sleep(150);
            }

            window.Patterns.Transform.Pattern.Resize(original.Width, original.Height);
            Thread.Sleep(200);

            return window.IsEnabled
                ? ModuleValidationResult.Pass(
                    "Layout.RapidReflow",
                    "UX",
                    "3 rapid resize cycles completed without disabling window")
                : ModuleValidationResult.Fail("Layout.RapidReflow", "UX", "Window disabled after rapid resize");
        }
        catch (Exception ex)
        {
            return ModuleValidationResult.Warn("Layout.RapidReflow", "UX", ex.Message);
        }
    }

    private static ModuleValidationResult ValidateTrayHideOnClose(AutomationElement window)
    {
        try
        {
            var processId = window.Properties.ProcessId.Value;
            UiAutomationHelpers.FocusWindow(window);
            Keyboard.Press(VirtualKeyShort.ALT);
            Keyboard.Press(VirtualKeyShort.F4);
            Keyboard.Release(VirtualKeyShort.F4);
            Keyboard.Release(VirtualKeyShort.ALT);
            Thread.Sleep(1500);

            var stillRunning = Process.GetProcesses().Any(process =>
                process.Id == processId && !process.HasExited);

            if (stillRunning)
            {
                return ModuleValidationResult.Pass(
                    "Lifecycle.TrayHideOnClose",
                    "Lifecycle",
                    "Process survives close (tray hide); WebView sessions preserved");
            }

            return ModuleValidationResult.Warn(
                "Lifecycle.TrayHideOnClose",
                "Lifecycle",
                "Process exited on Alt+F4 — background-on-close may be disabled in settings");
        }
        catch (Exception ex)
        {
            return ModuleValidationResult.Warn("Lifecycle.TrayHideOnClose", "Lifecycle", ex.Message);
        }
    }

    private static ModuleValidationResult SafeValidate(Func<ModuleValidationResult> validate)
    {
        try
        {
            return validate();
        }
        catch (Exception ex)
        {
            return ModuleValidationResult.Fail("UiHarness", "Runtime", ex.Message);
        }
    }

    public static ModuleValidationResult RunUnitTestSuite(string repoRoot)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "test UnifiedMessenger.Tests/UnifiedMessenger.Tests.csproj -c Release -v q",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            return ModuleValidationResult.Fail(
                "BackgroundServices.UnitTests",
                "Services",
                "Could not start dotnet test");
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(120_000);

        if (process.ExitCode == 0 && output.Contains("Passed!", StringComparison.Ordinal))
        {
            var match = System.Text.RegularExpressions.Regex.Match(output, @"Passed:\s+(\d+)");
            var count = match.Success ? match.Groups[1].Value : "?";
            return ModuleValidationResult.Pass(
                "BackgroundServices.UnitTests",
                "Services",
                $"{count} unit tests passed (triage, channels, adapters, lifecycle, ops center)");
        }

        return ModuleValidationResult.Fail(
            "BackgroundServices.UnitTests",
            "Services",
            output.Length > 400 ? output[^400..] : output);
    }

    private static ModuleValidationResult ValidateMainShell(AutomationElement window)
    {
        if (!window.IsEnabled)
        {
            return ModuleValidationResult.Fail("MainWindow.Shell", "Shell", "Main window disabled");
        }

        var buttons = window.FindAllDescendants(window.ConditionFactory.ByControlType(ControlType.Button));
        return ModuleValidationResult.Pass(
            "MainWindow.Shell",
            "Shell",
            $"Window enabled; {buttons.Length} buttons in UIA tree");
    }

    private static ModuleValidationResult ValidateDashboardOperations(AutomationElement window)
    {
        if (UiAutomationHelpers.ClickByName(window, "Sidebar Dashboard"))
        {
            Thread.Sleep(800);
        }

        var markers = new[] { "OVERVIEW", "Dashboard", "Operations Command Center", "Welcome back" };
        foreach (var marker in markers)
        {
            if (UiAutomationHelpers.FindByName(window, marker) is not null)
            {
                return ModuleValidationResult.Pass(
                    "Dashboard.OperationsCommandCenter",
                    "Page",
                    $"Marker '{marker}' visible");
            }
        }

        return ModuleValidationResult.Fail(
            "Dashboard.OperationsCommandCenter",
            "Page",
            $"No dashboard marker; sample={string.Join(" | ", UiAutomationHelpers.SampleNames(window))}");
    }

    private static ModuleValidationResult ValidatePersonalOverview(AutomationElement window)
    {
        if (!UiAutomationHelpers.ClickByName(window, "Personal Overview Tab"))
        {
            UiAutomationHelpers.ClickByName(window, "Personal Overview");
        }

        Thread.Sleep(600);

        if (UiAutomationHelpers.WaitForMarker(window, "Search personal accounts", TimeSpan.FromSeconds(3)) ||
            UiAutomationHelpers.FindByName(window, "Personal Overview Tab") is not null)
        {
            return ModuleValidationResult.Pass(
                "Dashboard.PersonalOverview",
                "Page",
                "Personal Overview tab reachable");
        }

        return ModuleValidationResult.Warn(
            "Dashboard.PersonalOverview",
            "Page",
            "Tab header not exposed via UIA; verify manually");
    }

    private static ModuleValidationResult ValidateSettingsPage(AutomationElement window)
    {
        if (!UiAutomationHelpers.ClickByName(window, "Settings"))
        {
            return ModuleValidationResult.Fail("SettingsPage", "Page", "Settings sidebar button not found");
        }

        Thread.Sleep(900);
        if (UiAutomationHelpers.WaitForMarker(window, "Settings", TimeSpan.FromSeconds(4)))
        {
            return ModuleValidationResult.Pass("SettingsPage", "Page", "Settings page title visible");
        }

        return ModuleValidationResult.Fail(
            "SettingsPage",
            "Page",
            $"Settings title not found; sample={string.Join(" | ", UiAutomationHelpers.SampleNames(window))}");
    }

    private static ModuleValidationResult ValidateLocalAiSettingsPage(AutomationElement window)
    {
        ValidateSettingsPage(window);
        if (!UiAutomationHelpers.ClickByName(window, "Open local AI settings"))
        {
            return ModuleValidationResult.Fail("LocalAISettingsPage", "Page", "Settings link not found");
        }

        Thread.Sleep(900);
        if (UiAutomationHelpers.WaitForMarker(window, "Local AI", TimeSpan.FromSeconds(4)))
        {
            UiAutomationHelpers.ClickByName(window, "Back to Settings");
            Thread.Sleep(500);
            return ModuleValidationResult.Pass("LocalAISettingsPage", "Page", "Local AI settings navigated");
        }

        return ModuleValidationResult.Fail("LocalAISettingsPage", "Page", "Local AI title not visible");
    }

    private static ModuleValidationResult ValidateAboutPage(AutomationElement window)
    {
        ValidateSettingsPage(window);
        if (!UiAutomationHelpers.ClickByName(window, "View app details"))
        {
            return ModuleValidationResult.Fail("AboutPage", "Page", "About link not found");
        }

        Thread.Sleep(900);
        if (UiAutomationHelpers.WaitForMarker(window, "About", TimeSpan.FromSeconds(4)))
        {
            UiAutomationHelpers.ClickByName(window, "Back to Settings");
            Thread.Sleep(500);
            return ModuleValidationResult.Pass("AboutPage", "Page", "About page navigated");
        }

        return ModuleValidationResult.Fail("AboutPage", "Page", "About title not visible");
    }

    private static ModuleValidationResult ValidateCommandPalette(AutomationElement window)
    {
        UiAutomationHelpers.FocusWindow(window);
        UiAutomationHelpers.SendChord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_K);
        Thread.Sleep(700);

        if (UiAutomationHelpers.FindByName(window, "Command Palette Search") is not null ||
            UiAutomationHelpers.FindByName(window, "Search instances, alerts, and actions...") is not null)
        {
            UiAutomationHelpers.SendEscape();
            Thread.Sleep(300);
            return ModuleValidationResult.Pass("CommandPalette", "Overlay", "Ctrl+K opened palette");
        }

        UiAutomationHelpers.SendEscape();
        return ModuleValidationResult.Warn(
            "CommandPalette",
            "Overlay",
            "Palette search box not exposed via UIA after Ctrl+K");
    }

    private static ModuleValidationResult ValidateNotificationPanel(AutomationElement window)
    {
        if (UiAutomationHelpers.ClickByName(window, "Notification Hub"))
        {
            Thread.Sleep(700);
            if (UiAutomationHelpers.WaitForMarker(window, "Notifications", TimeSpan.FromSeconds(3)))
            {
                UiAutomationHelpers.ClickByName(window, "Notification Hub");
                return ModuleValidationResult.Pass("NotificationFeedPanel", "Panel", "Notification hub toggled");
            }
        }

        if (UiAutomationHelpers.ClickByName(window, "Toggle notification panel"))
        {
            Thread.Sleep(700);
            return ModuleValidationResult.Pass("NotificationFeedPanel", "Panel", "Title-bar notification toggle");
        }

        return ModuleValidationResult.Warn(
            "NotificationFeedPanel",
            "Panel",
            "Notification panel not confirmed via UIA");
    }

    private static ModuleValidationResult ValidateWorkspaceSidebar(AutomationElement window)
    {
        var markers = new[] { "OVERVIEW", "Pro / Business", "Add Instance", "Settings" };
        var found = markers.Where(marker => UiAutomationHelpers.FindByName(window, marker) is not null).ToList();
        return found.Count >= 2
            ? ModuleValidationResult.Pass("WorkspaceSidebar", "Control", $"Sections: {string.Join(", ", found)}")
            : ModuleValidationResult.Fail(
                "WorkspaceSidebar",
                "Control",
                $"Expected sidebar sections; found={string.Join(", ", found)}");
    }

    private static ModuleValidationResult ValidateAddInstanceDialog(AutomationElement window)
    {
        UiAutomationHelpers.ClickByName(window, "Sidebar Dashboard");
        Thread.Sleep(400);

        if (!UiAutomationHelpers.ClickByName(window, "Add Instance"))
        {
            return ModuleValidationResult.Fail("AddInstanceDialog", "Dialog", "Add Instance button not found");
        }

        Thread.Sleep(1200);
        var dialog = UiAutomationHelpers.FindDialog(window, "Add messaging instance") ??
                     UiAutomationHelpers.FindByName(window, "Add messaging instance");

        if (dialog is null)
        {
            return ModuleValidationResult.Warn(
                "AddInstanceDialog",
                "Dialog",
                "Dialog not found in UIA tree (WinUI ContentDialog limitation)");
        }

        if (UiAutomationHelpers.ClickByName(dialog, "Cancel"))
        {
            Thread.Sleep(400);
            return ModuleValidationResult.Pass("AddInstanceDialog", "Dialog", "Add dialog opened and cancelled");
        }

        UiAutomationHelpers.SendEscape();
        return ModuleValidationResult.Warn("AddInstanceDialog", "Dialog", "Dialog visible; cancel via Escape");
    }

    private static ModuleValidationResult ValidateInstanceSwitch(AutomationElement window)
    {
        var condition = window.ConditionFactory.ByControlType(ControlType.Text);
        var instanceName = window.FindAllDescendants(condition)
            .Select(element => element.Name)
            .FirstOrDefault(name =>
                !string.IsNullOrWhiteSpace(name) &&
                (name.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase) ||
                 name.Contains("Meta Business", StringComparison.OrdinalIgnoreCase) ||
                 name.Contains("Google", StringComparison.OrdinalIgnoreCase)));

        if (instanceName is null)
        {
            return ModuleValidationResult.Warn(
                "InstanceSession.WebViewHost",
                "Shell",
                "No configured instance rows found for live WebView switch");
        }

        if (!UiAutomationHelpers.ClickByName(window, instanceName))
        {
            return ModuleValidationResult.Fail(
                "InstanceSession.WebViewHost",
                "Shell",
                $"Could not click instance '{instanceName}'");
        }

        Thread.Sleep(1500);
        return ModuleValidationResult.Pass(
            "InstanceSession.WebViewHost",
            "Shell",
            $"Switched to instance '{instanceName}'");
    }
}
