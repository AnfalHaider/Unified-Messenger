using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace UnifiedMessenger.UiSmokeTests;

/// <summary>
/// Deep OCC-only navigation and UIA probing for detailed QA reports.
/// </summary>
internal static class OccDetailedExploration
{
    private static readonly List<ExplorationFinding> Findings = [];

    public static int Run(string exePath, int durationMinutes)
    {
        Findings.Clear();
        var endUtc = DateTime.UtcNow.AddMinutes(durationMinutes);
        Console.WriteLine($"=== OCC deep exploration ({durationMinutes} min) ===");
        Console.WriteLine($"Executable: {exePath}");
        Console.WriteLine();

        FlaUI.Core.Application? app = null;
        try
        {
            StopProcesses();
            app = FlaUI.Core.Application.Launch(exePath);
            using var automation = new FlaUI.UIA3.UIA3Automation();
            var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(60));
            if (window is null)
            {
                Record("Launch", "Critical", "Main window not found");
                PrintReport();
                return 2;
            }

            Record("Launch", "Pass", window.Name);
            var cycle = 0;

            while (DateTime.UtcNow < endUtc)
            {
                cycle++;
                Console.WriteLine($"--- OCC cycle {cycle} ---");
                RunOccCycle(window, cycle);
                Thread.Sleep(500);
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
                // Tray.
            }

            StopProcesses();
        }

        PrintReport();
        return Findings.Any(f => f.Severity is "Critical" or "Fail") ? 3 : 0;
    }

    private static void RunOccCycle(AutomationElement window, int cycle)
    {
        UiAutomationHelpers.FocusWindow(window);
        UiAutomationHelpers.ClickByName(window, "Sidebar Dashboard");
        Thread.Sleep(700);
        UiAutomationHelpers.EnsureDashboardOperationsTab(window);
        UiAutomationHelpers.WaitForDashboardOccReady(window, TimeSpan.FromSeconds(15));
        Thread.Sleep(600);

        CaptureTextSamples(window, "OCC.Scope", "Showing:");
        CaptureTextSamples(window, "OCC.AiChip", "AI ");
        CaptureTextSamples(window, "OCC.Backfill", "Backfill");
        CaptureTextSamples(window, "OCC.Updated", "Updated");

        AnalyzeViewMode(window);
        AnalyzeDateRange(window);
        AnalyzeKpiRow(window, cycle);
        AnalyzeImmediateQueue(window);
        AnalyzeKanban(window);
        AnalyzeChart(window);
        AnalyzeBranchPills(window);
        ScrollOccFull(window);
    }

    private static void AnalyzeViewMode(AutomationElement window)
    {
        var toggle = UiAutomationHelpers.FindByName(window, "Operations view mode") ??
                     UiAutomationHelpers.FindByAutomationId(window, "OccViewModeToggle");

        if (toggle is null)
        {
            // OffContent/OnContent may appear as separate text.
            if (UiAutomationHelpers.ClickByNameContains(window, "Live workload"))
            {
                Thread.Sleep(600);
                Record("OCC.ViewMode", "Pass", "Switched toward Live workload via label click");
                CaptureTextSamples(window, "OCC.Scope.Live", "Showing:");
            }

            if (UiAutomationHelpers.ClickByNameContains(window, "Historical report"))
            {
                Thread.Sleep(600);
                Record("OCC.ViewMode", "Pass", "Switched toward Historical report via label click");
                CaptureTextSamples(window, "OCC.Scope.Historical", "Showing:");
            }

            return;
        }

        try
        {
            if (toggle.Patterns.Toggle.IsSupported)
            {
                var state = toggle.Patterns.Toggle.Pattern.ToggleState;
                Record("OCC.ViewMode.Toggle", "Info", $"Toggle state before flip: {state}");
                toggle.Patterns.Toggle.Pattern.Toggle();
                Thread.Sleep(800);
                var after = toggle.Patterns.Toggle.Pattern.ToggleState;
                Record("OCC.ViewMode.Toggle", "Pass", $"Toggled view mode → {after}");
                CaptureTextSamples(window, "OCC.Scope.AfterToggle", "Showing:");
                toggle.Patterns.Toggle.Pattern.Toggle();
                Thread.Sleep(600);
            }
        }
        catch (Exception ex)
        {
            Record("OCC.ViewMode.Toggle", "Warn", ex.Message);
        }
    }

    private static void AnalyzeDateRange(AutomationElement window)
    {
        var hasFrom = UiAutomationHelpers.FindByName(window, "From date") is not null;
        var hasTo = UiAutomationHelpers.FindByName(window, "To date") is not null;
        Record(
            "OCC.DateRange.Pickers",
            hasFrom && hasTo ? "Pass" : "Warn",
            $"From={hasFrom}, To={hasTo}");

        var caption = UiAutomationHelpers.FindByNameContains(window, "Chart uses the date range");
        Record(
            "OCC.DateRange.Help",
            caption is not null ? "Pass" : "Info",
            caption?.Name ?? "Caption explaining chart vs KPI scope not found in UIA");

        if (UiAutomationHelpers.ClickByName(window, "Clear Filter") ||
            UiAutomationHelpers.ClickByNameContains(window, "Clear Filter"))
        {
            Thread.Sleep(500);
            Record("OCC.DateRange", "Pass", "Clear Filter clicked");
        }

        if (UiAutomationHelpers.ClickByName(window, "Refresh"))
        {
            Thread.Sleep(800);
            Record("OCC.Refresh", "Pass", "Refresh clicked — snapshot reload");
        }
    }

    private static void AnalyzeKpiRow(AutomationElement window, int cycle)
    {
        var kpiIds = new[]
        {
            ("OccKpiOpenThreads", "Open threads"),
            ("OccKpiHangingLeads", "Hanging leads"),
            ("OccKpiNeedsAction", "Needs action"),
            ("OccKpiSlaBreaches", "SLA breaches")
        };

        foreach (var (autoId, label) in kpiIds)
        {
            var card = UiAutomationHelpers.FindByAutomationId(window, autoId);
            if (card is null)
            {
                card = UiAutomationHelpers.FindByNameContains(window, label);
            }

            if (card is null)
            {
                Record($"OCC.KPI.{label}", "Warn", "KPI card not found via UIA");
                continue;
            }

            var valueHint = TryReadNearbyValue(card);
            Record(
                $"OCC.KPI.{label}",
                "Pass",
                $"Visible; value hint='{valueHint ?? "?"}'; enabled={card.IsEnabled}");

            if (cycle == 1 && card.IsEnabled)
            {
                if (UiAutomationHelpers.FindByAutomationId(window, autoId) is { } target &&
                    ClickSafe(target))
                {
                    Thread.Sleep(1200);
                    Record($"OCC.KPI.{label}.Click", "Pass", "Activated KPI card — navigation attempted");
                    UiAutomationHelpers.ClickByName(window, "Sidebar Dashboard");
                    Thread.Sleep(500);
                    UiAutomationHelpers.EnsureDashboardOperationsTab(window);
                    Thread.Sleep(400);
                }
            }
        }
    }

    private static void AnalyzeImmediateQueue(AutomationElement window)
    {
        var section = UiAutomationHelpers.FindByNameContains(window, "Immediate queue");
        Record(
            "OCC.ImmediateQueue.Section",
            section is not null ? "Pass" : "Warn",
            section is not null ? "Immediate queue section label found" : "Section label missing");

        var emptyHint = UiAutomationHelpers.FindByNameContains(window, "No urgent threads");
        var cards = window.FindAllDescendants(window.ConditionFactory.ByControlType(ControlType.ListItem));
        Record(
            "OCC.ImmediateQueue.Items",
            "Info",
            emptyHint is not null
                ? "Empty state: no urgent threads in current scope"
                : $"List items in tree: {cards.Length}");
    }

    private static void AnalyzeKanban(AutomationElement window)
    {
        var columns = new[]
        {
            "Kanban column: New inquiries",
            "Kanban column: Hanging leads",
            "Kanban column: Resolved"
        };

        var visible = 0;
        foreach (var col in columns)
        {
            if (UiAutomationHelpers.FindByName(window, col) is not null)
            {
                visible++;
                Record("OCC.Kanban.Column", "Pass", col);
            }
        }

        if (visible < 3)
        {
            Record(
                "OCC.Kanban",
                visible >= 2 ? "Warn" : "Fail",
                $"Only {visible}/3 kanban columns exposed via UIA (Resolved often off-screen — scroll issue?)");
        }
        else
        {
            Record("OCC.Kanban", "Pass", "All 3 kanban columns visible");
        }

        // Horizontal scroll kanban region
        UiAutomationHelpers.FocusWindow(window);
        for (var i = 0; i < 4; i++)
        {
            Keyboard.Press(VirtualKeyShort.RIGHT);
            Keyboard.Release(VirtualKeyShort.RIGHT);
            Thread.Sleep(100);
        }

        if (UiAutomationHelpers.FindByName(window, "Kanban column: Resolved") is not null)
        {
            Record("OCC.Kanban.Resolved", "Pass", "Resolved column visible after horizontal scroll");
        }
    }

    private static void AnalyzeChart(AutomationElement window)
    {
        var chartMarkers = new[] { "Message volume", "Sent", "Received", "No message volume", "messages in range" };
        var found = chartMarkers.Where(m => UiAutomationHelpers.FindByNameContains(window, m) is not null).ToList();
        Record(
            "OCC.Chart",
            found.Count > 0 ? "Pass" : "Info",
            found.Count > 0
                ? $"Chart markers: {string.Join(", ", found)}"
                : "Chart region not labeled in UIA (may still render visually)");
    }

    private static void AnalyzeBranchPills(AutomationElement window)
    {
        var pills = window.FindAllDescendants(window.ConditionFactory.ByControlType(ControlType.Button))
            .Select(UiAutomationHelpers.SafeName)
            .Where(n => !string.IsNullOrWhiteSpace(n) &&
                        (n.Contains("branches", StringComparison.OrdinalIgnoreCase) ||
                         n.Contains("DHA", StringComparison.OrdinalIgnoreCase) ||
                         n.Contains("F-11", StringComparison.OrdinalIgnoreCase) ||
                         n.Contains("Branch", StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Record(
            "OCC.BranchPills",
            pills.Count > 0 ? "Pass" : "Info",
            pills.Count > 0 ? $"Pills: {string.Join(", ", pills)}" : "No branch pills matched heuristics");

        if (pills.Count > 1)
        {
            var target = pills.First(p => p is not null && !p.Contains("All", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(target) && UiAutomationHelpers.ClickByName(window, target))
            {
                Thread.Sleep(600);
                Record("OCC.BranchPills.Filter", "Pass", $"Selected branch pill '{target}'");
                CaptureTextSamples(window, "OCC.Scope.Branch", "Showing:");
                UiAutomationHelpers.ClickByName(window, "All branches");
                Thread.Sleep(400);
            }
        }
    }

    private static void ScrollOccFull(AutomationElement window)
    {
        UiAutomationHelpers.ScrollDashboardOccIntoView(window);
        for (var i = 0; i < 8; i++)
        {
            Keyboard.Press(VirtualKeyShort.END);
            Keyboard.Release(VirtualKeyShort.END);
            Thread.Sleep(120);
        }

        Record("OCC.Scroll", "Pass", "Scrolled OCC content (PageDown/End sequence)");
    }

    private static void CaptureTextSamples(AutomationElement window, string category, string prefix)
    {
        var matches = window.FindAllDescendants(window.ConditionFactory.ByControlType(ControlType.Text))
            .Select(e => e.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n) &&
                        n.Contains(prefix, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .Take(4)
            .ToList();

        if (matches.Count > 0)
        {
            Record(category, "Info", string.Join(" | ", matches));
        }
    }

    private static string? TryReadNearbyValue(AutomationElement card)
    {
        try
        {
            var texts = card.FindAllDescendants(card.ConditionFactory.ByControlType(ControlType.Text))
                .Select(t => t.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n) && n.Any(char.IsDigit))
                .ToList();
            return texts.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static bool ClickSafe(AutomationElement target)
    {
        try
        {
            target.Focus();
            if (target.Patterns.Invoke.IsSupported)
            {
                target.Patterns.Invoke.Pattern.Invoke();
                return true;
            }

            target.Click();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void StopProcesses()
    {
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

    private static void Record(string area, string severity, string detail)
    {
        Findings.Add(new ExplorationFinding(DateTime.UtcNow, area, severity, detail));
        Console.WriteLine($"[{severity}] {area}: {detail}");
    }

    private static void PrintReport()
    {
        Console.WriteLine();
        Console.WriteLine("=== OCC deep exploration summary ===");
        foreach (var group in Findings.GroupBy(f => f.Area).OrderBy(g => g.Key))
        {
            var best = group.OrderByDescending(f => SeverityRank(f.Severity)).First();
            Console.WriteLine($"  {group.Key}: [{best.Severity}] {best.Detail}");
        }

        Console.WriteLine();
        Console.WriteLine(
            $"Pass={Findings.Count(f => f.Severity == "Pass")}, " +
            $"Warn={Findings.Count(f => f.Severity == "Warn")}, " +
            $"Info={Findings.Count(f => f.Severity == "Info")}, " +
            $"Fail={Findings.Count(f => f.Severity == "Fail")}, " +
            $"Critical={Findings.Count(f => f.Severity == "Critical")}");
    }

    private static int SeverityRank(string s) =>
        s switch { "Critical" => 5, "Fail" => 4, "Warn" => 3, "Pass" => 2, _ => 1 };

    private sealed record ExplorationFinding(DateTime Utc, string Area, string Severity, string Detail);
}
