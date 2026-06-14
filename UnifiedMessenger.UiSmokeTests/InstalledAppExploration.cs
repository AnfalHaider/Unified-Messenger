using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace UnifiedMessenger.UiSmokeTests;

/// <summary>
/// Timed navigation of the installed app with emphasis on OCC dashboard behavior.
/// </summary>
internal static class InstalledAppExploration
{
    private static readonly List<ExplorationFinding> Findings = [];

    public static int Run(string exePath, int durationMinutes)
    {
        Findings.Clear();
        var endUtc = DateTime.UtcNow.AddMinutes(durationMinutes);
        Console.WriteLine($"=== Installed app exploration ({durationMinutes} min) ===");
        Console.WriteLine($"Executable: {exePath}");
        Console.WriteLine($"Ends at: {endUtc:HH:mm:ss} UTC");
        Console.WriteLine();

        FlaUI.Core.Application? app = null;
        try
        {
            app = FlaUI.Core.Application.Launch(exePath);
            using var automation = new FlaUI.UIA3.UIA3Automation();
            var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(60));
            if (window is null)
            {
                Record("Launch", "Critical", "Main window not found within 60s");
                PrintFindings();
                return 2;
            }

            Record("Launch", "Pass", $"Main window: {window.Name}");

            while (DateTime.UtcNow < endUtc)
            {
                ExploreOccDeep(window);
                if (DateTime.UtcNow >= endUtc)
                {
                    break;
                }

                ExploreSettingsAi(window);
                if (DateTime.UtcNow >= endUtc)
                {
                    break;
                }

                ExploreShellSurfaces(window);
                if (DateTime.UtcNow >= endUtc)
                {
                    break;
                }

                ExplorePersonalOverview(window);
                if (DateTime.UtcNow >= endUtc)
                {
                    break;
                }

                ExploreInstances(window);
                Thread.Sleep(800);
            }
        }
        catch (Exception ex)
        {
            Record("Harness", "Critical", ex.Message);
        }
        finally
        {
            try
            {
                app?.Close();
            }
            catch
            {
                // Tray hide.
            }

            foreach (var process in Process.GetProcessesByName("UnifiedMessenger"))
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(3000);
                    }
                }
                catch
                {
                    // Best effort.
                }
            }
        }

        PrintFindings();
        return Findings.Any(f => f.Severity == "Critical") ? 3 : 0;
    }

    private static void ExploreOccDeep(AutomationElement window)
    {
        UiAutomationHelpers.FocusWindow(window);
        UiAutomationHelpers.ClickByName(window, "Sidebar Dashboard");
        Thread.Sleep(600);

        var occTab = UiAutomationHelpers.EnsureDashboardOperationsTab(window);
        Record(
            "OCC.Tab",
            occTab ? "Pass" : "Warn",
            occTab ? "Operations Command Center tab selected" : "Could not select OCC tab via UIA");

        var ready = UiAutomationHelpers.WaitForDashboardOccReady(window, TimeSpan.FromSeconds(12));
        Record(
            "OCC.Ready",
            ready ? "Pass" : "Warn",
            ready
                ? "OCC markers visible (date range / kanban / refresh)"
                : $"OCC not ready; sample={string.Join(" | ", UiAutomationHelpers.SampleNames(window, 12))}");

        UiAutomationHelpers.ScrollDashboardOccIntoView(window);
        Thread.Sleep(500);

        ProbeOccMarkers(window);
        TryToggle(window, "Live workload", "OCC.ViewMode");
        TryToggle(window, "Historical report", "OCC.ViewMode");
        TryClick(window, "Refresh", "OCC.Refresh");
        TryClick(window, "Clear Filter", "OCC.DateRangeClear");
        TryClick(window, "All branches", "OCC.BranchFilter");

        foreach (var kpi in new[] { "Open threads", "Hanging leads", "Immediate action", "SLA breaches" })
        {
            if (UiAutomationHelpers.FindByNameContains(window, kpi) is not null)
            {
                Record("OCC.KPI", "Pass", $"KPI region exposes '{kpi}'");
            }
        }

        foreach (var col in new[] { "Kanban column: New inquiries", "Kanban column: Hanging leads", "Kanban column: Resolved" })
        {
            var found = UiAutomationHelpers.FindByName(window, col) is not null;
            Record(
                "OCC.Kanban",
                found ? "Pass" : "Info",
                found ? $"Column visible: {col}" : $"Column not in UIA: {col}");
        }

        if (UiAutomationHelpers.FindByNameContains(window, "AI ready") is not null ||
            UiAutomationHelpers.FindByNameContains(window, "AI offline") is not null)
        {
            Record("OCC.AiChip", "Pass", "AI status chip visible in OCC header area");
        }
        else
        {
            Record("OCC.AiChip", "Info", "AI ready/offline chip not found via UIA (may be off-screen or unlabeled)");
        }

        if (UiAutomationHelpers.FindByNameContains(window, "Welcome back") is not null)
        {
            Record("OCC.Header", "Pass", "Welcome back header visible on dashboard");
        }

        if (UiAutomationHelpers.FindByNameContains(window, "Updated") is not null ||
            UiAutomationHelpers.FindByNameContains(window, "Backfill") is not null)
        {
            Record("OCC.StatusRow", "Pass", "Updated / backfill status metadata visible");
        }

        Thread.Sleep(1200);
    }

    private static void ExploreSettingsAi(AutomationElement window)
    {
        if (!UiAutomationHelpers.ClickByName(window, "Settings"))
        {
            Record("Settings", "Warn", "Settings sidebar button not clickable");
            return;
        }

        Thread.Sleep(900);
        Record("Settings", "Pass", "Settings page opened");

        if (UiAutomationHelpers.ClickByName(window, "AI") ||
            UiAutomationHelpers.ClickByNameContains(window, "Local AI"))
        {
            Thread.Sleep(700);
            Record("Settings.AI", "Pass", "Navigated to AI settings section");
        }

        foreach (var marker in new[]
                 {
                     "Enable local AI",
                     "Download AI runtime",
                     "Test connection",
                     "Pull selected model",
                     "phi3"
                 })
        {
            if (UiAutomationHelpers.FindByNameContains(window, marker) is not null)
            {
                Record("Settings.AI.Controls", "Pass", $"Control/text visible: {marker}");
            }
        }

        UiAutomationHelpers.ClickByName(window, "About");
        Thread.Sleep(500);
        if (UiAutomationHelpers.WaitForMarker(window, "About", TimeSpan.FromSeconds(3)))
        {
            var versionText = UiAutomationHelpers.SampleNames(window, 30)
                .FirstOrDefault(n => n.Contains("3.7", StringComparison.Ordinal));
            Record(
                "About.Version",
                versionText is not null ? "Pass" : "Info",
                versionText ?? "About page open; version string not matched in UIA sample");
        }

        UiAutomationHelpers.ClickByName(window, "Back to Settings");
        Thread.Sleep(400);
    }

    private static void ExploreShellSurfaces(AutomationElement window)
    {
        UiAutomationHelpers.FocusWindow(window);
        UiAutomationHelpers.SendChord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_K);
        Thread.Sleep(600);
        var palette = UiAutomationHelpers.FindByName(window, "Command Palette Search") is not null ||
                      UiAutomationHelpers.FindByNameContains(window, "Search instances") is not null;
        Record(
            "CommandPalette",
            palette ? "Pass" : "Warn",
            palette ? "Ctrl+K palette opened" : "Palette not detected");
        UiAutomationHelpers.SendEscape();
        Thread.Sleep(300);

        if (UiAutomationHelpers.ClickByName(window, "Notification Hub"))
        {
            Thread.Sleep(600);
            Record(
                "Notifications",
                UiAutomationHelpers.WaitForMarker(window, "Notifications", TimeSpan.FromSeconds(2))
                    ? "Pass"
                    : "Warn",
                "Notification hub toggled");
            UiAutomationHelpers.ClickByName(window, "Notification Hub");
        }
    }

    private static void ExplorePersonalOverview(AutomationElement window)
    {
        UiAutomationHelpers.ClickByName(window, "Sidebar Dashboard");
        Thread.Sleep(400);
        UiAutomationHelpers.EnsurePersonalOverviewTab(window);
        Thread.Sleep(800);
        var personal = UiAutomationHelpers.FindByNameContains(window, "Personal") is not null ||
                       UiAutomationHelpers.FindByAutomationId(window, "PersonalGlobalSearch") is not null;
        Record(
            "PersonalOverview",
            personal ? "Pass" : "Warn",
            personal ? "Personal Overview tab content reachable" : "Personal tab not confirmed via UIA");
    }

    private static void ExploreInstances(AutomationElement window)
    {
        var instance = window.FindAllDescendants(window.ConditionFactory.ByControlType(ControlType.Text))
            .Select(e => e.Name)
            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n) &&
                                 n.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase));

        if (instance is null)
        {
            Record("Instances", "Info", "No WhatsApp instance row text in UIA tree");
            return;
        }

        if (UiAutomationHelpers.ClickByName(window, instance))
        {
            Thread.Sleep(1500);
            Record("Instances", "Pass", $"Switched to instance '{instance}'");
        }
    }

    private static void ProbeOccMarkers(AutomationElement window)
    {
        foreach (var marker in new[] { "DATE RANGE", "From date", "To date", "Showing:", "Live workload", "Historical report" })
        {
            if (UiAutomationHelpers.FindByNameContains(window, marker) is not null ||
                UiAutomationHelpers.FindByName(window, marker) is not null)
            {
                Record("OCC.Markers", "Pass", marker);
            }
        }
    }

    private static void TryToggle(AutomationElement window, string label, string category)
    {
        if (UiAutomationHelpers.ClickByNameContains(window, label))
        {
            Thread.Sleep(500);
            Record(category, "Pass", $"Clicked '{label}'");
        }
    }

    private static void TryClick(AutomationElement window, string label, string category)
    {
        if (UiAutomationHelpers.ClickByName(window, label) || UiAutomationHelpers.ClickByNameContains(window, label))
        {
            Thread.Sleep(400);
            Record(category, "Pass", $"Clicked '{label}'");
        }
    }

    private static void Record(string area, string severity, string detail)
    {
        Findings.Add(new ExplorationFinding(DateTime.UtcNow, area, severity, detail));
        Console.WriteLine($"[{severity}] {area}: {detail}");
    }

    private static void PrintFindings()
    {
        Console.WriteLine();
        Console.WriteLine("=== Exploration summary ===");
        var grouped = Findings
            .GroupBy(f => f.Area)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in grouped)
        {
            var best = group.OrderByDescending(f => SeverityRank(f.Severity)).First();
            Console.WriteLine($"  {group.Key}: {best.Severity} — {best.Detail}");
        }

        Console.WriteLine();
        Console.WriteLine(
            $"Totals: Pass={Findings.Count(f => f.Severity == "Pass")}, " +
            $"Warn={Findings.Count(f => f.Severity == "Warn")}, " +
            $"Info={Findings.Count(f => f.Severity == "Info")}, " +
            $"Critical={Findings.Count(f => f.Severity == "Critical")}");
    }

    private static int SeverityRank(string severity) =>
        severity switch
        {
            "Critical" => 4,
            "Warn" => 3,
            "Pass" => 2,
            _ => 1
        };

    private sealed record ExplorationFinding(DateTime Utc, string Area, string Severity, string Detail);
}
