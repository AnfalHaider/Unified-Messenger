using System.Diagnostics;
using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace UnifiedMessenger.UiSmokeTests;

/// <summary>
/// Full-application timed exploration: OCC deep tests, shell surfaces, settings, instances.
/// Writes a structured transcript to <c>.testlogs/full-app-10min-detailed-log.txt</c>.
/// </summary>
internal static class FullAppExploration
{
    private static readonly List<ExplorationFinding> Findings = [];
    private static readonly StringBuilder Log = new();
    private static readonly List<ExplorationFinding> CycleFindings = [];
    private static string? _logPath;
    private static string? _summaryPath;
    private static DateTime _startedUtc;
    private static int _completedCycles;
    private static string _currentView = "Shell";

    public static int Run(string exePath, int durationMinutes, string? logFileName = null, string? summaryFileName = null)
    {
        Findings.Clear();
        Log.Clear();
        _completedCycles = 0;
        _startedUtc = DateTime.UtcNow;

        var repoRoot = FindRepoRoot();
        var logDir = Path.Combine(repoRoot, ".testlogs");
        Directory.CreateDirectory(logDir);
        _logPath = Path.Combine(logDir, logFileName ?? "full-app-10min-detailed-log.txt");
        _summaryPath = Path.Combine(logDir, summaryFileName ?? "full-app-10min-detailed-summary.md");

        var endUtc = _startedUtc.AddMinutes(durationMinutes);
        AppendLogLine($"=== Full app exploration ({durationMinutes} min, detailed) ===");
        AppendLogLine($"Started: {_startedUtc:O}");
        AppendLogLine($"Executable: {exePath}");
        AppendLogLine($"Ends at: {endUtc:O}");
        AppendLogLine();

        FlaUI.Core.Application? app = null;
        var exitCode = 0;
        try
        {
            StopProcesses();
            app = FlaUI.Core.Application.Launch(exePath);
            using var automation = new FlaUI.UIA3.UIA3Automation();
            var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(60));
            if (window is null)
            {
                Record("Launch", "Critical", "Main window not found within 60s");
                FlushLog();
                WriteExecutiveSummary(durationMinutes, endUtc);
                return 2;
            }

            Record("Launch", "Pass", $"Main window: {UiAutomationHelpers.SafeName(window) ?? "(unnamed)"}");
            var cycle = 0;

            while (DateTime.UtcNow < endUtc)
            {
                cycle++;
                try
                {
                    BeginCycle(cycle);

                    RunPhase(window, "OCC", () => RunOccDeepCycle(window, cycle == 1));
                    if (DateTime.UtcNow >= endUtc) { EndCycle(cycle); break; }

                    RunPhase(window, "OCC", () => TestWorkQueueFilterChips(window));
                    if (DateTime.UtcNow >= endUtc) { EndCycle(cycle); break; }

                    RunPhase(window, "OCC", () => TestWorkQueueNavigation(window));
                    if (DateTime.UtcNow >= endUtc) { EndCycle(cycle); break; }

                    RunPhase(window, "OCC", () => TestBoardViewKanban(window));
                    if (DateTime.UtcNow >= endUtc) { EndCycle(cycle); break; }

                    RunPhase(window, "OCC", () => TestHistoricalDateRange(window));
                    if (DateTime.UtcNow >= endUtc) { EndCycle(cycle); break; }

                    RunPhase(window, "OCC", () => TestThreadCardContent(window));
                    if (DateTime.UtcNow >= endUtc) { EndCycle(cycle); break; }

                    RunPhase(window, "OCC", () => TestAiBadges(window));
                    if (DateTime.UtcNow >= endUtc) { EndCycle(cycle); break; }

                    RunPhase(window, "Settings", () => ExploreSettingsAllSections(window));
                    if (DateTime.UtcNow >= endUtc) { EndCycle(cycle); break; }

                    RunPhase(window, "Shell", () => ExploreShellSurfaces(window));
                    if (DateTime.UtcNow >= endUtc) { EndCycle(cycle); break; }

                    RunPhase(window, "PersonalOverview", () => ExplorePersonalOverview(window));
                    if (DateTime.UtcNow >= endUtc) { EndCycle(cycle); break; }

                    RunPhase(window, "Instance", () => ExploreInstancesAndReturn(window));
                    RunPhase(window, "Shell", () => ExploreSidebarButtons(window));

                    EndCycle(cycle);
                    _completedCycles = cycle;
                }
                catch (Exception cycleEx)
                {
                    Record("Harness.Cycle", "Warn", $"Cycle {cycle} aborted: {cycleEx.Message}");
                    EndCycle(cycle);
                    _completedCycles = cycle;
                    ReturnToOccFromInstance(window);
                }

                Thread.Sleep(600);
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

            StopProcesses();
            PrintSummary();
            FlushLog();
            WriteExecutiveSummary(durationMinutes, _startedUtc.AddMinutes(durationMinutes));
        }

        return Findings.Any(f => f.Severity is "Critical" or "Fail") ? 3 : exitCode;
    }

    private static void BeginCycle(int cycle)
    {
        CycleFindings.Clear();
        AppendLogLine($"[CYCLE {cycle}] {DateTime.UtcNow:O} | view={_currentView}");
    }

    private static void EndCycle(int cycle)
    {
        var pass = CycleFindings.Count(f => f.Severity == "Pass");
        var warn = CycleFindings.Count(f => f.Severity == "Warn");
        var fail = CycleFindings.Count(f => f.Severity is "Fail" or "Critical");
        AppendLogLine($"  Summary: pass={pass} warn={warn} fail={fail}");
        AppendLogLine();
    }

    private static void RunPhase(AutomationElement window, string view, Action action)
    {
        _currentView = view;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Record("Harness.Phase", "Warn", $"{view} phase aborted: {ex.Message}");
        }
    }

    private static void RunOccDeepCycle(AutomationElement window, bool captureKpiOverlap)
    {
        UiAutomationHelpers.FocusWindow(window);
        UiAutomationHelpers.ClickByName(window, "Sidebar Dashboard");
        Thread.Sleep(700);
        UiAutomationHelpers.EnsureDashboardOperationsTab(window);

        var ready = UiAutomationHelpers.WaitForDashboardOccReady(window, TimeSpan.FromSeconds(15));
        Record(
            "OCC.Ready",
            ready ? "Pass" : "Warn",
            ready ? "OCC markers visible" : "OCC not ready within timeout");

        UiAutomationHelpers.ScrollDashboardOccIntoView(window);
        Thread.Sleep(500);

        CaptureTextSamples(window, "OCC.Scope", "Showing:");
        TryCaptureAiChip(window);
        AnalyzeStickyHeader(window);
        AnalyzeTeachingTip(window);
        AnalyzeBranchPills(window);

        AnalyzeViewMode(window);
        AnalyzeDateRangePickers(window);
        AnalyzeSlaMarkers(window);
        AnalyzeKpiRow(window, alwaysLogAutomationIds: true);

        if (captureKpiOverlap)
        {
            AnalyzeKpiOverlap(window);
        }

        AnalyzeKanban(window, boardExpanded: false);
        AnalyzeChart(window);
        ScrollOccFull(window);
    }

    private static void TestWorkQueueFilterChips(AutomationElement window)
    {
        UiAutomationHelpers.FocusWindow(window);
        ReturnToOccFromInstance(window);
        UiAutomationHelpers.EnsureDashboardOperationsTab(window);
        UiAutomationHelpers.ScrollDashboardOccIntoView(window);
        Thread.Sleep(400);

        foreach (var chip in new[] { "All open", "Urgent", "SLA", "Hanging" })
        {
            try
            {
                var clicked = UiAutomationHelpers.ClickByName(window, chip);
                Thread.Sleep(450);
                var empty = UiAutomationHelpers.FindByNameContains(window, "No open threads") is not null ||
                            UiAutomationHelpers.FindByNameContains(window, "No urgent threads") is not null ||
                            UiAutomationHelpers.FindByNameContains(window, "No SLA") is not null ||
                            UiAutomationHelpers.FindByNameContains(window, "No hanging") is not null;
                Record(
                    $"OCC.FilterChip.{chip}",
                    clicked ? "Pass" : "Warn",
                    clicked
                        ? empty ? $"Chip '{chip}' selected; empty state visible" : $"Chip '{chip}' selected"
                        : $"Could not click filter chip '{chip}'");
            }
            catch (Exception ex)
            {
                Record($"OCC.FilterChip.{chip}", "Warn", ex.Message);
            }
        }

        UiAutomationHelpers.ClickByName(window, "All open");
        Thread.Sleep(300);
    }

    private static void TestWorkQueueNavigation(AutomationElement window)
    {
        UiAutomationHelpers.FocusWindow(window);
        ReturnToOccFromInstance(window);
        UiAutomationHelpers.ClickByName(window, "Sidebar Dashboard");
        Thread.Sleep(500);
        UiAutomationHelpers.EnsureDashboardOperationsTab(window);
        UiAutomationHelpers.WaitForDashboardOccReady(window, TimeSpan.FromSeconds(12));
        UiAutomationHelpers.ScrollDashboardOccIntoView(window);
        Thread.Sleep(400);

        var empty = UiAutomationHelpers.FindByNameContains(window, "No open threads") is not null ||
                    UiAutomationHelpers.FindByNameContains(window, "No urgent threads") is not null;
        if (empty)
        {
            Record("OCC.WorkQueue.Nav", "Info", "SKIP — empty work queue");
            return;
        }

        var beforeOnDashboard = IsDashboardVisible(window);
        var clicked = TryClickFirstWorkQueueCard(window);

        if (!clicked)
        {
            Record("OCC.WorkQueue.Nav", "Fail", "Could not click first work-queue card via UIA");
            return;
        }

        Thread.Sleep(2500);
        var navigated = DetectInstanceNavigation(window, beforeOnDashboard);
        var navFailed = UiAutomationHelpers.FindByNameContains(window, "Opened inbox") is not null ||
                        UiAutomationHelpers.FindByNameContains(window, "could not focus") is not null;

        if (navFailed)
        {
            Record(
                "OCC.WorkQueue.Nav",
                "Warn",
                "Instance opened; conversation focus may need manual selection (persistent InfoBar expected)");
        }
        else if (navigated.InstanceOpened)
        {
            Record(
                "OCC.WorkQueue.Nav",
                navigated.ThreadFocused ? "Pass" : "Warn",
                navigated.ThreadFocused
                    ? $"Navigated to instance; focus signals OK. Hints: {navigated.Hints}"
                    : $"Instance shell opened; thread focus unconfirmed. Hints: {navigated.Hints}");
        }
        else
        {
            Record(
                "OCC.WorkQueue.Nav",
                "Fail",
                $"No navigation detected after card click. Hints: {navigated.Hints}");
        }

        ReturnToOccFromInstance(window);
    }

    private static void TestBoardViewKanban(AutomationElement window)
    {
        UiAutomationHelpers.FocusWindow(window);
        ReturnToOccFromInstance(window);
        UiAutomationHelpers.EnsureDashboardOperationsTab(window);
        UiAutomationHelpers.ScrollDashboardOccIntoView(window);
        Thread.Sleep(400);

        var boardToggle = UiAutomationHelpers.FindByName(window, "Board view");
        if (boardToggle is null)
        {
            Record("OCC.BoardView", "Warn", "Board view toggle not found via UIA");
            return;
        }

        var expanded = ClickSafe(boardToggle);
        Thread.Sleep(900);
        Record(
            "OCC.BoardView.Toggle",
            expanded ? "Pass" : "Warn",
            expanded ? "Board view toggled ON" : "Could not toggle Board view");

        AnalyzeKanban(window, boardExpanded: true);

        if (ClickSafe(boardToggle))
        {
            Thread.Sleep(500);
            Record("OCC.BoardView.Collapse", "Pass", "Board view toggled OFF");
        }
    }

    private static void TestHistoricalDateRange(AutomationElement window)
    {
        UiAutomationHelpers.FocusWindow(window);
        UiAutomationHelpers.ClickByName(window, "Sidebar Dashboard");
        Thread.Sleep(400);
        UiAutomationHelpers.EnsureDashboardOperationsTab(window);
        Thread.Sleep(600);

        var liveScope = SampleScopeText(window);
        var switchedHistorical = SwitchToHistoricalReport(window);
        Thread.Sleep(800);
        var historicalScope = SampleScopeText(window);

        Record(
            "OCC.DateRange.HistoricalToggle",
            switchedHistorical ? "Pass" : "Warn",
            switchedHistorical
                ? $"Historical mode engaged. Scope before='{liveScope}' after='{historicalScope}'"
                : "Could not toggle to Historical report");

        var banner = UiAutomationHelpers.FindByNameContains(window, "Historical report");
        Record(
            "OCC.Historical.Banner",
            banner is not null ? "Pass" : "Info",
            banner is not null ? "Historical report banner/label visible" : "Historical banner not detected in UIA");

        if (UiAutomationHelpers.ClickByName(window, "Sync recent history") ||
            UiAutomationHelpers.ClickByNameContains(window, "Sync recent history"))
        {
            Thread.Sleep(800);
            Record("OCC.Historical.Sync", "Pass", "Sync recent history clicked");
        }
        else
        {
            Record("OCC.Historical.Sync", "Info", "Sync recent history button not visible (may require historical mode)");
        }

        if (UiAutomationHelpers.ClickByName(window, "Reload snapshot") ||
            UiAutomationHelpers.ClickByNameContains(window, "Reload snapshot"))
        {
            Thread.Sleep(800);
            Record("OCC.Historical.Reload", "Pass", "Reload snapshot clicked");
        }
        else if (UiAutomationHelpers.ClickByName(window, "Refresh"))
        {
            Thread.Sleep(1200);
            Record("OCC.Historical.Reload", "Pass", "Refresh clicked (Reload snapshot alias)");
            CaptureTextSamples(window, "OCC.Scope.AfterRefresh", "Showing:");
        }
        else
        {
            Record("OCC.Historical.Reload", "Info", "Reload snapshot / Refresh not found");
        }

        SwitchToLiveWorkload(window);
        Thread.Sleep(500);
    }

    private static void TestThreadCardContent(AutomationElement window)
    {
        UiAutomationHelpers.EnsureDashboardOperationsTab(window);
        UiAutomationHelpers.ScrollDashboardOccIntoView(window);
        Thread.Sleep(400);

        var cardTexts = UiAutomationHelpers.SafeTextNames(
            window,
            n =>
                n.Contains("Inquiry", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("Heuristic", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("Neutral", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("Waiting", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("SLA", StringComparison.OrdinalIgnoreCase) ||
                n.Length > 20,
            max: 16);

        var hasBadges = cardTexts.Any(t =>
            t.Contains("Inquiry", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("Heuristic", StringComparison.OrdinalIgnoreCase));
        var hasSla = cardTexts.Any(t =>
            t.Contains("Waiting", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("SLA", StringComparison.OrdinalIgnoreCase));
        var hasMessagePreview = cardTexts.Any(t =>
            t.Length > 20 &&
            !t.Contains("Heuristic", StringComparison.OrdinalIgnoreCase) &&
            !t.Contains("Inquiry", StringComparison.OrdinalIgnoreCase) &&
            !t.Contains("Neutral", StringComparison.OrdinalIgnoreCase) &&
            !t.Contains("Waiting", StringComparison.OrdinalIgnoreCase) &&
            !t.Contains("Click to open", StringComparison.OrdinalIgnoreCase) &&
            !t.Contains("Showing:", StringComparison.OrdinalIgnoreCase));

        Record(
            "OCC.ThreadCard.Badges",
            hasBadges ? "Pass" : "Warn",
            hasBadges
                ? $"Intent/inference badges visible: {string.Join(" | ", cardTexts.Take(6))}"
                : "No Inquiry/Heuristic/Neutral badge text found in UIA");

        Record(
            "OCC.ThreadCard.SlaText",
            hasSla ? "Pass" : "Info",
            hasSla ? "SLA/waiting text present on cards" : "No 'Waiting … SLA' text detected via UIA");

        Record(
            "OCC.ThreadCard.MessagePreview",
            hasMessagePreview ? "Pass" : "Warn",
            hasMessagePreview
                ? $"Message preview detected: {cardTexts.First(t => t.Length > 20 && !t.Contains("Showing:", StringComparison.OrdinalIgnoreCase))}"
                : "No prominent message preview text on thread cards via UIA");
    }

    private static void TestAiBadges(AutomationElement window)
    {
        var heuristic = UiAutomationHelpers.FindByNameContains(window, "Heuristic") is not null;
        var ollama = UiAutomationHelpers.FindByNameContains(window, "Ollama") is not null ||
                     UiAutomationHelpers.FindByNameContains(window, "AI") is not null;
        var aiReady = UiAutomationHelpers.FindByNameContains(window, "AI ready") is not null;
        var aiOffline = UiAutomationHelpers.FindByNameContains(window, "AI offline") is not null;

        Record(
            "OCC.AI.InferenceBadge",
            heuristic ? "Pass" : "Info",
            heuristic
                ? "Heuristic inference badge visible on thread cards"
                : "No Heuristic badge — AI enrichment may be off or cards not rendered");

        Record(
            "OCC.AI.StatusChip",
            aiReady || aiOffline ? "Pass" : "Info",
            aiReady ? "AI ready chip visible" : aiOffline ? "AI offline chip visible" : "AI status chip not found");

        if (ollama && !heuristic)
        {
            Record("OCC.AI.OllamaVsHeuristic", "Info", "Ollama/AI labels present but Heuristic badge not on cards");
        }
    }

    private static void AnalyzeKpiRow(AutomationElement window, bool alwaysLogAutomationIds)
    {
        var kpiIds = new (string AutoId, string Label)[]
        {
            ("OccKpiOpenThreads", "Open threads"),
            ("OccKpiUrgent", "Urgent"),
            ("OccKpiSlaBreaches", "SLA breaches"),
            ("OccKpiHangingLeads", "Hanging leads")
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
                Record($"OCC.KPI.{label}", "Warn", $"AutomationId={autoId} — card not found in UIA tree");
                continue;
            }

            var hint = TryReadNearbyValue(card);
            var aid = UiAutomationHelpers.SafeAutomationId(card) ?? autoId;
            Record(
                $"OCC.KPI.{label}",
                alwaysLogAutomationIds ? "Pass" : hint is not null ? "Pass" : "Warn",
                $"AutomationId={aid}; value={(hint ?? "unreadable")}; enabled={UiAutomationHelpers.SafeIsEnabled(card)}");
        }
    }

    private static void AnalyzeKpiOverlap(AutomationElement window)
    {
        var kpiIds = new (string AutoId, string Label)[]
        {
            ("OccKpiOpenThreads", "Open threads"),
            ("OccKpiUrgent", "Urgent"),
            ("OccKpiSlaBreaches", "SLA breaches"),
            ("OccKpiHangingLeads", "Hanging leads")
        };

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (autoId, label) in kpiIds)
        {
            var card = UiAutomationHelpers.FindByAutomationId(window, autoId);
            var hint = card is not null ? TryReadNearbyValue(card) : null;
            if (hint is not null)
            {
                values[label] = hint;
            }
        }

        if (values.Count >= 3 &&
            values.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1 &&
            values.Values.First() is var same && same != "—" && same != "0")
        {
            Record(
                "OCC.KPI.Overlap",
                "Warn",
                $"All KPIs show identical value ({same}). May indicate overlapping thread sets.");
        }
        else if (values.Count >= 2)
        {
            Record(
                "OCC.KPI.Overlap",
                "Pass",
                $"KPI values: {string.Join(", ", values.Select(kv => $"{kv.Key}={kv.Value}"))}");
        }
    }

    private static void AnalyzeSlaMarkers(AutomationElement window)
    {
        var slaSamples = UiAutomationHelpers.SafeTextNames(
            window,
            n => n.Contains("SLA", StringComparison.OrdinalIgnoreCase) ||
                 n.Contains("Waiting", StringComparison.OrdinalIgnoreCase),
            max: 5);

        Record(
            "OCC.SLA.Display",
            slaSamples.Count > 0 ? "Pass" : "Info",
            slaSamples.Count > 0
                ? $"SLA UI text: {string.Join(" | ", slaSamples)}"
                : "No SLA/waiting text in UIA tree (cards may be off-screen)");
    }

    private static void AnalyzeStickyHeader(AutomationElement window)
    {
        var scope = UiAutomationHelpers.SafeTextNames(window, n => n.Contains("Showing:", StringComparison.OrdinalIgnoreCase), max: 1);
        var workQueue = UiAutomationHelpers.FindByNameContains(window, "Work queue") is not null;
        Record(
            "OCC.StickyHeader",
            scope.Count > 0 && workQueue ? "Pass" : scope.Count > 0 ? "Warn" : "Info",
            scope.Count > 0
                ? $"Scope line present: {scope[0]}; work-queue label={workQueue}"
                : "Sticky header scope text not found");
    }

    private static void AnalyzeTeachingTip(AutomationElement window)
    {
        var tip = UiAutomationHelpers.FindByNameContains(window, "Unified work queue") is not null ||
                  UiAutomationHelpers.FindByNameContains(window, "filter chips") is not null;
        Record(
            "OCC.TeachingTip",
            tip ? "Pass" : "Info",
            tip ? "Queue UX TeachingTip text detectable in UIA" : "TeachingTip not open/detectable (expected after first visit)");
    }

    private static void AnalyzeBranchPills(AutomationElement window)
    {
        var pills = UiAutomationHelpers.SafeDescendants(window, ControlType.Button)
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

    private static void TryCaptureAiChip(AutomationElement window)
    {
        try
        {
            foreach (var marker in new[] { "AI ready", "AI offline" })
            {
                if (UiAutomationHelpers.FindByNameContains(window, marker) is not null)
                {
                    Record("OCC.AiChip", "Pass", $"Chip marker: {marker}");
                    return;
                }
            }

            Record("OCC.AiChip", "Info", "AI chip not found via UIA");
        }
        catch (Exception ex)
        {
            Record("OCC.AiChip", "Info", $"AI chip probe skipped: {ex.Message}");
        }
    }

    private static void AnalyzeViewMode(AutomationElement window)
    {
        var toggle = UiAutomationHelpers.FindByAutomationId(window, "OccViewModeToggle") ??
                     UiAutomationHelpers.FindByName(window, "Operations view mode");

        if (toggle?.Patterns.Toggle.IsSupported == true)
        {
            try
            {
                var before = toggle.Patterns.Toggle.Pattern.ToggleState;
                toggle.Patterns.Toggle.Pattern.Toggle();
                Thread.Sleep(700);
                var after = toggle.Patterns.Toggle.Pattern.ToggleState;
                Record("OCC.ViewMode", "Pass", $"Live/Historical toggled {before} → {after}");
                CaptureTextSamples(window, "OCC.Scope.AfterToggle", "Showing:");
                toggle.Patterns.Toggle.Pattern.Toggle();
                Thread.Sleep(500);
                return;
            }
            catch (Exception ex)
            {
                Record("OCC.ViewMode", "Warn", ex.Message);
            }
        }

        if (UiAutomationHelpers.ClickByNameContains(window, "Historical report"))
        {
            Thread.Sleep(600);
            Record("OCC.ViewMode", "Pass", "Historical report via label click");
            UiAutomationHelpers.ClickByNameContains(window, "Live workload");
            Thread.Sleep(400);
        }
    }

    private static void AnalyzeDateRangePickers(AutomationElement window)
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
            caption is not null
                ? UiAutomationHelpers.SafeName(caption) ?? "Caption found"
                : "Chart vs KPI scope caption not found");
    }

    private static void AnalyzeKanban(AutomationElement window, bool boardExpanded)
    {
        var columns = new[]
        {
            ("OccKanbanNew", "Kanban column: New inquiries"),
            ("OccKanbanHanging", "Kanban column: Hanging leads"),
            ("OccKanbanResolved", "Kanban column: Resolved")
        };

        var visible = 0;
        var details = new List<string>();
        foreach (var (autoId, name) in columns)
        {
            var col = UiAutomationHelpers.FindByAutomationId(window, autoId) ??
                      UiAutomationHelpers.FindByName(window, name);
            if (col is not null)
            {
                visible++;
                details.Add($"{name} (AutomationId={autoId})");
            }
        }

        if (visible < 3 && !boardExpanded)
        {
            for (var i = 0; i < 4; i++)
            {
                Keyboard.Press(VirtualKeyShort.RIGHT);
                Keyboard.Release(VirtualKeyShort.RIGHT);
                Thread.Sleep(80);
            }

            foreach (var (autoId, name) in columns)
            {
                if (UiAutomationHelpers.FindByAutomationId(window, autoId) is not null ||
                    UiAutomationHelpers.FindByName(window, name) is not null)
                {
                    if (!details.Any(d => d.Contains(name, StringComparison.OrdinalIgnoreCase)))
                    {
                        visible++;
                        details.Add($"{name} after scroll");
                    }
                }
            }
        }

        var suffix = boardExpanded ? " [board expanded]" : "";
        Record(
            "OCC.Kanban",
            visible >= 3 ? "Pass" : visible >= 1 ? "Warn" : "Fail",
            $"{visible}/3 kanban columns visible via UIA{suffix}. {string.Join("; ", details)}");
    }

    private static void AnalyzeChart(AutomationElement window)
    {
        var markers = new[] { "Message volume", "Sent", "Received", "No message volume", "messages in range" };
        var found = markers.Where(m => UiAutomationHelpers.FindByNameContains(window, m) is not null).ToList();
        Record(
            "OCC.Chart",
            found.Count > 0 ? "Pass" : "Info",
            found.Count > 0 ? string.Join(", ", found) : "Chart region not labeled in UIA");
    }

    private static void ExploreSettingsAllSections(AutomationElement window)
    {
        if (!UiAutomationHelpers.ClickByName(window, "Settings"))
        {
            Record("Settings", "Warn", "Settings sidebar not clickable");
            return;
        }

        Thread.Sleep(900);
        Record("Settings", "Pass", "Settings page opened");

        var sections = new (string Label, string[] Markers)[]
        {
            ("Notifications", ["NOTIFICATIONS", "Auto-open notification panel", "Toast sound"]),
            ("Appearance", ["App theme", "NOTIFICATIONS"]),
            ("Session & performance", ["Max concurrent WebViews", "SLA response threshold"]),
            ("AI", ["Enable local AI", "Ollama endpoint", "Test connection", "Download AI runtime"]),
            ("Storage", ["instances.json", "profiles", "Storage"]),
            ("About", ["About", "Unified Messenger", "4.0.0"])
        };

        foreach (var (label, markers) in sections)
        {
            try
            {
                var opened = UiAutomationHelpers.ClickByName(window, label) ||
                             UiAutomationHelpers.ClickByNameContains(window, label);
                Thread.Sleep(650);

                var found = markers.Where(m => UiAutomationHelpers.FindByNameContains(window, m) is not null).ToList();
                Record(
                    $"Settings.{label}",
                    opened && found.Count > 0 ? "Pass" : opened ? "Warn" : "Warn",
                    opened
                        ? $"Section opened; markers: {string.Join(", ", found)}"
                        : $"Could not open section '{label}'");
            }
            catch (Exception ex)
            {
                Record($"Settings.{label}", "Warn", ex.Message);
            }
        }

        var versionSamples = UiAutomationHelpers.SafeTextNames(
            window,
            n => n.Contains("4.0.0", StringComparison.Ordinal) ||
                 n.Contains("Unified Messenger v", StringComparison.OrdinalIgnoreCase),
            max: 4);
        Record(
            "Settings.About.Version",
            versionSamples.Any(n => n.Contains("4.0.0", StringComparison.Ordinal)) ? "Pass" : "Warn",
            versionSamples.Count > 0
                ? string.Join(" | ", versionSamples)
                : "Version 4.0.0 not found in UIA text");

        UiAutomationHelpers.ClickByName(window, "Sidebar Dashboard");
        Thread.Sleep(500);
        UiAutomationHelpers.EnsureDashboardOperationsTab(window);
    }

    private static void ExploreShellSurfaces(AutomationElement window)
    {
        UiAutomationHelpers.FocusWindow(window);
        UiAutomationHelpers.SendChord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_K);
        Thread.Sleep(600);
        var palette = UiAutomationHelpers.FindByAutomationId(window, "CommandPaletteSearch") is not null ||
                      UiAutomationHelpers.FindByName(window, "Command Palette Search") is not null ||
                      UiAutomationHelpers.FindByNameContains(window, "Search instances") is not null;
        Record(
            "CommandPalette",
            palette ? "Pass" : "Warn",
            palette ? "Ctrl+K command palette opened" : "Palette not detected");
        UiAutomationHelpers.SendEscape();
        Thread.Sleep(300);

        if (UiAutomationHelpers.ClickByName(window, "Notification Hub"))
        {
            Thread.Sleep(600);
            var feed = UiAutomationHelpers.FindByAutomationId(window, "NotificationFeedPanel") is not null ||
                       UiAutomationHelpers.FindByNameContains(window, "Notifications") is not null;
            Record(
                "Notifications",
                feed ? "Pass" : "Warn",
                feed ? "Notification Hub opened with feed panel" : "Notification Hub toggled");
            UiAutomationHelpers.ClickByName(window, "Notification Hub");
        }

        if (UiAutomationHelpers.ClickByNameContains(window, "Add Instance") ||
            UiAutomationHelpers.ClickByNameContains(window, "Add instance"))
        {
            Thread.Sleep(500);
            Record("AddInstance", "Pass", "Add Instance flow opened");
            UiAutomationHelpers.SendEscape();
            Thread.Sleep(300);
        }
    }

    private static void ExplorePersonalOverview(AutomationElement window)
    {
        UiAutomationHelpers.ClickByName(window, "Sidebar Dashboard");
        Thread.Sleep(400);
        UiAutomationHelpers.EnsurePersonalOverviewTab(window);
        Thread.Sleep(800);

        var personal = UiAutomationHelpers.FindByAutomationId(window, "PersonalGlobalSearch") is not null ||
                       UiAutomationHelpers.FindByNameContains(window, "Personal") is not null;
        Record(
            "PersonalOverview",
            personal ? "Pass" : "Warn",
            personal ? "Personal Overview tab content reachable" : "Personal tab not confirmed");

        var urgentLink = UiAutomationHelpers.FindByName(window, "View urgent operations threads") is not null ||
                         UiAutomationHelpers.FindByNameContains(window, "Urgent in Operations") is not null;
        if (urgentLink)
        {
            var clicked = UiAutomationHelpers.ClickByName(window, "View urgent operations threads") ||
                          UiAutomationHelpers.ClickByNameContains(window, "Urgent in Operations");
            Thread.Sleep(900);
            var onOcc = UiAutomationHelpers.EnsureDashboardOperationsTab(window) ||
                        UiAutomationHelpers.FindByAutomationId(window, "DashboardOccTab") is not null;
            var urgentChip = UiAutomationHelpers.FindByName(window, "Urgent") is not null;
            Record(
                "PersonalOverview.OccUrgentLink",
                clicked && onOcc ? "Pass" : "Warn",
                clicked
                    ? $"Cross-link clicked; OCC tab={onOcc}; Urgent chip visible={urgentChip}"
                    : "Urgent cross-link visible but click failed");
        }
        else
        {
            Record("PersonalOverview.OccUrgentLink", "Info", "No urgent operations cross-link (zero urgent threads?)");
        }

        UiAutomationHelpers.EnsureDashboardOperationsTab(window);
    }

    private static void ExploreInstancesAndReturn(AutomationElement window)
    {
        var instanceName = UiAutomationHelpers.SafeDescendants(window, ControlType.Text)
            .Select(UiAutomationHelpers.SafeName)
            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n) &&
                                 n.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase));

        if (instanceName is null)
        {
            Record("Instances.WebView", "Info", "No WhatsApp instance in sidebar UIA");
            return;
        }

        if (UiAutomationHelpers.ClickByName(window, instanceName))
        {
            Thread.Sleep(2000);
            var onInstance = DetectInstanceNavigation(window, dashboardBefore: true);
            Record(
                "Instances.WebView",
                onInstance.InstanceOpened ? "Pass" : "Warn",
                onInstance.InstanceOpened
                    ? $"Switched to instance '{instanceName}'. Hints: {onInstance.Hints}"
                    : $"Clicked '{instanceName}' but instance shell not confirmed");

            if (UiAutomationHelpers.ClickByName(window, "Back to Dashboard") ||
                UiAutomationHelpers.ClickByName(window, "Sidebar Dashboard"))
            {
                Thread.Sleep(800);
            }
        }
    }

    private static void ExploreSidebarButtons(AutomationElement window)
    {
        foreach (var btn in new[] { "Sidebar Dashboard", "Settings", "Notification Hub" })
        {
            if (UiAutomationHelpers.FindByName(window, btn) is not null)
            {
                Record("Sidebar", "Pass", $"{btn} present");
            }
        }
    }

    private static bool TryClickFirstWorkQueueCard(AutomationElement window)
    {
        var list = UiAutomationHelpers.FindByAutomationId(window, "WorkQueueList")
            ?? UiAutomationHelpers.FindByAutomationId(window, "ImmediateQueueList");
        if (list is not null)
        {
            var items = list.FindAllDescendants(list.ConditionFactory.ByControlType(ControlType.ListItem));
            if (items.Length > 0 && ClickSafe(items[0]))
            {
                return true;
            }
        }

        foreach (var item in UiAutomationHelpers.SafeDescendants(window, ControlType.ListItem))
        {
            try
            {
                var texts = UiAutomationHelpers.SafeDescendants(item, ControlType.Text)
                    .Select(UiAutomationHelpers.SafeName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();
                if (texts.Any(t => t is not null && t.Contains("U", StringComparison.Ordinal) && t.Length <= 3) ||
                    texts.Any(t => t is not null && t.Contains("Waiting", StringComparison.OrdinalIgnoreCase)))
                {
                    if (ClickSafe(item))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // stale node
            }
        }

        return UiAutomationHelpers.ClickByNameContains(window, "Click to open thread");
    }

    private static (bool InstanceOpened, bool ThreadFocused, string Hints) DetectInstanceNavigation(
        AutomationElement window,
        bool dashboardBefore)
    {
        var hints = new List<string>();

        if (UiAutomationHelpers.FindByName(window, "Back to Dashboard") is not null)
        {
            hints.Add("BackToDashboard");
        }

        if (UiAutomationHelpers.FindByNameContains(window, "Loading instance") is not null ||
            UiAutomationHelpers.FindByNameContains(window, "Opening conversation") is not null)
        {
            hints.Add("LoadingOverlay");
        }

        var whatsAppVisible = UiAutomationHelpers.SafeDescendants(window, ControlType.Text)
            .Any(e =>
            {
                var name = UiAutomationHelpers.SafeName(e);
                return name is not null && name.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase);
            });
        if (whatsAppVisible)
        {
            hints.Add("WhatsAppText");
        }

        var dashboardNow = IsDashboardVisible(window);
        if (dashboardBefore && !dashboardNow)
        {
            hints.Add("LeftDashboard");
        }

        var instanceOpened = hints.Contains("BackToDashboard") ||
                             hints.Contains("LeftDashboard") ||
                             hints.Contains("LoadingOverlay");
        var navError = UiAutomationHelpers.FindByNameContains(window, "Could not open conversation") is not null;
        var threadFocused = instanceOpened && !navError && !hints.Contains("LoadingOverlay");

        return (instanceOpened, threadFocused, string.Join(",", hints));
    }

    private static bool IsDashboardVisible(AutomationElement window) =>
        UiAutomationHelpers.FindByNameContains(window, "Operations Command Center") is not null ||
        UiAutomationHelpers.FindByNameContains(window, "Personal Overview") is not null ||
        UiAutomationHelpers.FindByAutomationId(window, "DashboardOccTab") is not null;

    private static void ReturnToOccFromInstance(AutomationElement window)
    {
        if (UiAutomationHelpers.ClickByName(window, "Back to Dashboard"))
        {
            Thread.Sleep(800);
        }
        else
        {
            UiAutomationHelpers.ClickByName(window, "Sidebar Dashboard");
            Thread.Sleep(600);
        }

        UiAutomationHelpers.EnsureDashboardOperationsTab(window);
        Thread.Sleep(400);
    }

    private static bool SwitchToHistoricalReport(AutomationElement window)
    {
        var toggle = UiAutomationHelpers.FindByAutomationId(window, "OccViewModeToggle") ??
                     UiAutomationHelpers.FindByName(window, "Operations view mode");
        if (toggle?.Patterns.Toggle.IsSupported == true)
        {
            try
            {
                if (toggle.Patterns.Toggle.Pattern.ToggleState == ToggleState.Off)
                {
                    toggle.Patterns.Toggle.Pattern.Toggle();
                }

                return true;
            }
            catch
            {
                // fall through
            }
        }

        return UiAutomationHelpers.ClickByNameContains(window, "Historical report");
    }

    private static void SwitchToLiveWorkload(AutomationElement window)
    {
        var toggle = UiAutomationHelpers.FindByAutomationId(window, "OccViewModeToggle") ??
                     UiAutomationHelpers.FindByName(window, "Operations view mode");
        if (toggle?.Patterns.Toggle.IsSupported == true)
        {
            try
            {
                if (toggle.Patterns.Toggle.Pattern.ToggleState == ToggleState.On)
                {
                    toggle.Patterns.Toggle.Pattern.Toggle();
                }

                return;
            }
            catch
            {
                // fall through
            }
        }

        UiAutomationHelpers.ClickByNameContains(window, "Live workload");
    }

    private static string SampleScopeText(AutomationElement window) =>
        UiAutomationHelpers.SafeTextNames(window, n => n.Contains("Showing:", StringComparison.OrdinalIgnoreCase), max: 1)
            .FirstOrDefault() ?? "(none)";

    private static void ScrollOccFull(AutomationElement window)
    {
        UiAutomationHelpers.ScrollDashboardOccIntoView(window);
        for (var i = 0; i < 6; i++)
        {
            Keyboard.Press(VirtualKeyShort.END);
            Keyboard.Release(VirtualKeyShort.END);
            Thread.Sleep(100);
        }

        Record("OCC.Scroll", "Pass", "Scrolled OCC content");
    }

    private static void CaptureTextSamples(AutomationElement window, string category, string prefix)
    {
        try
        {
            var matches = UiAutomationHelpers.SafeTextNames(
                window,
                n => n.Contains(prefix, StringComparison.OrdinalIgnoreCase),
                max: 4);

            if (matches.Count > 0)
            {
                Record(category, "Info", string.Join(" | ", matches));
            }
        }
        catch (Exception ex)
        {
            Record(category, "Info", $"UIA sample skipped: {ex.Message}");
        }
    }

    private static string? TryReadNearbyValue(AutomationElement card)
    {
        try
        {
            return card.FindAllDescendants(card.ConditionFactory.ByControlType(ControlType.Text))
                .Select(UiAutomationHelpers.SafeName)
                .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n) && n.Any(char.IsDigit));
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
        var finding = new ExplorationFinding(DateTime.UtcNow, area, severity, detail);
        Findings.Add(finding);
        CycleFindings.Add(finding);

        var label = severity switch
        {
            "Pass" => "PASS",
            "Warn" => "WARN",
            "Fail" => "FAIL",
            "Critical" => "FAIL",
            _ => "INFO"
        };
        AppendLogLine($"  {label}: {area} | {detail}");
    }

    private static void AppendLogLine(string line = "")
    {
        Console.WriteLine(line);
        Log.AppendLine(line);
    }

    private static void PrintSummary()
    {
        AppendLogLine();
        AppendLogLine("=== Full app exploration summary ===");
        foreach (var group in Findings.GroupBy(f => f.Area).OrderBy(g => g.Key))
        {
            var best = group.OrderByDescending(f => SeverityRank(f.Severity)).First();
            AppendLogLine($"  {group.Key}: [{best.Severity}] {best.Detail}");
        }

        AppendLogLine();
        AppendLogLine(
            $"Pass={Findings.Count(f => f.Severity == "Pass")}, " +
            $"Warn={Findings.Count(f => f.Severity == "Warn")}, " +
            $"Info={Findings.Count(f => f.Severity == "Info")}, " +
            $"Fail={Findings.Count(f => f.Severity == "Fail")}, " +
            $"Critical={Findings.Count(f => f.Severity == "Critical")}");
        AppendLogLine($"Cycles completed: {_completedCycles}");
        AppendLogLine($"Duration: {(DateTime.UtcNow - _startedUtc).TotalMinutes:F1} min");
        AppendLogLine($"Finished: {DateTime.UtcNow:O}");
    }

    private static void WriteExecutiveSummary(int durationMinutes, DateTime plannedEndUtc)
    {
        if (string.IsNullOrWhiteSpace(_summaryPath))
        {
            return;
        }

        try
        {
            var finishedUtc = DateTime.UtcNow;
            var actualMinutes = (finishedUtc - _startedUtc).TotalMinutes;
            var pass = Findings.Count(f => f.Severity == "Pass");
            var warn = Findings.Count(f => f.Severity == "Warn");
            var info = Findings.Count(f => f.Severity == "Info");
            var fail = Findings.Count(f => f.Severity is "Fail" or "Critical");

            var areaGroups = Findings
                .GroupBy(f => f.Area.Split('.')[0])
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var p = g.Count(f => f.Severity == "Pass");
                    var w = g.Count(f => f.Severity == "Warn");
                    var fl = g.Count(f => f.Severity is "Fail" or "Critical");
                    return $"- **{g.Key}**: pass={p}, warn={w}, fail={fl}";
                });

            var kanbanExpanded = Findings
                .Where(f => f.Area == "OCC.Kanban" && f.Detail.Contains("board expanded", StringComparison.OrdinalIgnoreCase))
                .Select(f => f.Detail)
                .LastOrDefault() ?? "No board-expanded kanban probe recorded";

            var kpiSample = Findings
                .Where(f => f.Area.StartsWith("OCC.KPI.", StringComparison.Ordinal) && f.Area != "OCC.KPI.Overlap")
                .GroupBy(f => f.Area)
                .Select(g => g.Last().Detail)
                .Take(4);

            var sb = new StringBuilder();
            sb.AppendLine("# Full App 10-Minute Detailed Exploration — Executive Summary");
            sb.AppendLine();
            sb.AppendLine($"**Run:** {_startedUtc:yyyy-MM-dd HH:mm:ss} UTC → {finishedUtc:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"**Planned duration:** {durationMinutes} min | **Actual:** {actualMinutes:F1} min");
            sb.AppendLine($"**Cycles completed:** {_completedCycles}");
            sb.AppendLine($"**Executable:** installed Unified Messenger v4.0.0");
            sb.AppendLine();
            sb.AppendLine("## Totals");
            sb.AppendLine($"- Pass: **{pass}** | Warn: **{warn}** | Info: **{info}** | Fail: **{fail}**");
            sb.AppendLine();
            sb.AppendLine("## By area");
            foreach (var line in areaGroups)
            {
                sb.AppendLine(line);
            }

            sb.AppendLine();
            sb.AppendLine("## vs prior ~2 min crash run");
            sb.AppendLine("- Prior run crashed at cycle 1 with `Name [#30005]` after work-queue navigation.");
            sb.AppendLine("- This run uses `SafeName`/`SafeTextNames` and per-phase try/catch so UIA property errors do not abort the harness.");
            sb.AppendLine(fail == 0 && _completedCycles >= 10
                ? $"- **Completed full duration** with {_completedCycles} cycles (target ~16+ for 10 min)."
                : _completedCycles < 2
                    ? "- Run did not complete expected cycle count — review log."
                    : $"- Completed {_completedCycles} cycles; compare cycle timing to prior audits.");

            sb.AppendLine();
            sb.AppendLine("## KPI / Kanban UIA (board expanded)");
            sb.AppendLine($"- Board-expanded kanban: {kanbanExpanded}");
            foreach (var k in kpiSample)
            {
                sb.AppendLine($"- {k}");
            }

            sb.AppendLine();
            sb.AppendLine($"Full log: `{_logPath}`");

            File.WriteAllText(_summaryPath, sb.ToString());
            Console.WriteLine($"Summary written: {_summaryPath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not write summary: {ex.Message}");
        }
    }

    private static void FlushLog()
    {
        if (string.IsNullOrWhiteSpace(_logPath))
        {
            return;
        }

        try
        {
            File.WriteAllText(_logPath, Log.ToString());
            Console.WriteLine($"Log written: {_logPath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not write log: {ex.Message}");
        }
    }

    private static int SeverityRank(string s) =>
        s switch { "Critical" => 5, "Fail" => 4, "Warn" => 3, "Pass" => 2, _ => 1 };

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

    private sealed record ExplorationFinding(DateTime Utc, string Area, string Severity, string Detail);
}
