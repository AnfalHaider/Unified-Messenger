using System.Diagnostics;
using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace UnifiedMessenger.UiSmokeTests;

/// <summary>
/// Full-application timed exploration: OCC deep tests, shell surfaces, settings, instances.
/// Writes a transcript to <c>.cursor/full-app-10min-log.txt</c>.
/// </summary>
internal static class FullAppExploration
{
    private static readonly List<ExplorationFinding> Findings = [];
    private static readonly StringBuilder Log = new();
    private static string? _logPath;

    public static int Run(string exePath, int durationMinutes, string? logFileName = null)
    {
        Findings.Clear();
        Log.Clear();

        var repoRoot = FindRepoRoot();
        var logDir = Path.Combine(repoRoot, ".cursor");
        Directory.CreateDirectory(logDir);
        _logPath = Path.Combine(logDir, logFileName ?? "full-app-10min-log.txt");

        var endUtc = DateTime.UtcNow.AddMinutes(durationMinutes);
        AppendLogLine($"=== Full app exploration ({durationMinutes} min) ===");
        AppendLogLine($"Started: {DateTime.UtcNow:O}");
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
                return 2;
            }

            Record("Launch", "Pass", $"Main window: {window.Name}");
            var cycle = 0;

            while (DateTime.UtcNow < endUtc)
            {
                cycle++;
                AppendLogLine($"--- Full-app cycle {cycle} ---");

                RunOccDeepCycle(window, cycle == 1);
                if (DateTime.UtcNow >= endUtc)
                {
                    break;
                }

                TestWorkQueueNavigation(window);
                if (DateTime.UtcNow >= endUtc)
                {
                    break;
                }

                TestHistoricalDateRange(window);
                if (DateTime.UtcNow >= endUtc)
                {
                    break;
                }

                TestThreadCardContent(window);
                if (DateTime.UtcNow >= endUtc)
                {
                    break;
                }

                TestAiBadges(window);
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

                ExploreInstancesAndReturn(window);
                ExploreSidebarButtons(window);

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
        }

        return Findings.Any(f => f.Severity is "Critical" or "Fail") ? 3 : exitCode;
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

        AnalyzeViewMode(window);
        AnalyzeDateRangePickers(window);
        AnalyzeSlaMarkers(window);

        if (captureKpiOverlap)
        {
            AnalyzeKpiOverlap(window);
        }

        AnalyzeKanban(window);
        AnalyzeChart(window);
        ScrollOccFull(window);
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

    private static void TestImmediateQueueNavigation(AutomationElement window) =>
        TestWorkQueueNavigation(window);

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

        if (UiAutomationHelpers.ClickByName(window, "Refresh"))
        {
            Thread.Sleep(1200);
            Record("OCC.DateRange.Refresh", "Pass", "Refresh clicked in historical mode");
            CaptureTextSamples(window, "OCC.Scope.AfterRefresh", "Showing:");
        }

        SwitchToLiveWorkload(window);
        Thread.Sleep(500);
    }

    private static void TestThreadCardContent(AutomationElement window)
    {
        try
        {
        UiAutomationHelpers.EnsureDashboardOperationsTab(window);
        UiAutomationHelpers.ScrollDashboardOccIntoView(window);
        Thread.Sleep(400);

        var cardTexts = window.FindAllDescendants(window.ConditionFactory.ByControlType(ControlType.Text))
            .Select(e => e.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Where(n =>
                n.Contains("Inquiry", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("Heuristic", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("Neutral", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("Waiting", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("SLA", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();

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
            !t.Contains("Click to open", StringComparison.OrdinalIgnoreCase));

        Record(
            "OCC.ThreadCard.Badges",
            hasBadges ? "Pass" : "Warn",
            hasBadges
                ? $"Intent/inference badges visible: {string.Join(" | ", cardTexts.Take(6))}"
                : "No Inquiry/Heuristic/Neutral badge text found in UIA");

        Record(
            "OCC.ThreadCard.SlaText",
            hasSla ? "Pass" : "Info",
            hasSla
                ? "SLA/waiting text present on cards"
                : "No 'Waiting … SLA' text detected via UIA");

        Record(
            "OCC.ThreadCard.MessagePreview",
            hasMessagePreview ? "Pass" : "Fail",
            hasMessagePreview
                ? "Message preview or summary text detected on thread cards"
                : "No prominent message preview — cards show badges only (matches user screenshot; BuildFallbackSummary may return '—')");
        }
        catch (Exception ex)
        {
            Record("OCC.ThreadCard", "Warn", $"UIA card probe failed: {ex.Message}");
        }
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
                Record($"OCC.KPI.{label}", "Pass", $"Value={hint}");
            }
            else
            {
                Record($"OCC.KPI.{label}", "Warn", "KPI value not readable via UIA");
            }
        }

        if (values.TryGetValue("Open threads", out var open) &&
            values.TryGetValue("Needs action", out var needs) &&
            values.TryGetValue("SLA breaches", out var sla) &&
            open == needs && needs == sla && open != "—" && open != "0")
        {
            Record(
                "OCC.KPI.Overlap",
                "Warn",
                $"All three KPIs show identical value ({open}). Likely all unreplied threads are SLA-breached (IsImmediateAction includes IsSlaBreached). See ThreadData.cs:72-77.");
        }
        else if (values.Count >= 3)
        {
            Record(
                "OCC.KPI.Overlap",
                "Pass",
                $"KPI values differ or zero: {string.Join(", ", values.Select(kv => $"{kv.Key}={kv.Value}"))}");
        }
    }

    private static void AnalyzeSlaMarkers(AutomationElement window)
    {
        var slaSamples = window.FindAllDescendants(window.ConditionFactory.ByControlType(ControlType.Text))
            .Select(e => e.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n) &&
                        (n.Contains("SLA", StringComparison.OrdinalIgnoreCase) ||
                         n.Contains("Waiting", StringComparison.OrdinalIgnoreCase)))
            .Distinct()
            .Take(5)
            .ToList();

        Record(
            "OCC.SLA.Display",
            slaSamples.Count > 0 ? "Pass" : "Info",
            slaSamples.Count > 0
                ? $"SLA UI text: {string.Join(" | ", slaSamples)}"
                : "No SLA/waiting text in UIA tree (cards may be off-screen)");
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
                Record("OCC.ViewMode", "Pass", $"Toggled {before} → {after}");
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
            caption?.Name ?? "Chart vs KPI scope caption not found");
    }

    private static void AnalyzeKanban(AutomationElement window)
    {
        var columns = new[]
        {
            "Kanban column: New inquiries",
            "Kanban column: Hanging leads",
            "Kanban column: Resolved"
        };

        var visible = columns.Count(col => UiAutomationHelpers.FindByName(window, col) is not null);
        Record(
            "OCC.Kanban",
            visible >= 3 ? "Pass" : visible >= 2 ? "Warn" : "Fail",
            $"{visible}/3 kanban columns visible via UIA");

        for (var i = 0; i < 4; i++)
        {
            Keyboard.Press(VirtualKeyShort.RIGHT);
            Keyboard.Release(VirtualKeyShort.RIGHT);
            Thread.Sleep(80);
        }

        if (UiAutomationHelpers.FindByName(window, "Kanban column: Resolved") is not null)
        {
            Record("OCC.Kanban.Resolved", "Pass", "Resolved column visible after horizontal scroll");
        }
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

    private static void ExploreSettingsAi(AutomationElement window)
    {
        if (!UiAutomationHelpers.ClickByName(window, "Settings"))
        {
            Record("Settings", "Warn", "Settings sidebar not clickable");
            return;
        }

        Thread.Sleep(900);
        Record("Settings", "Pass", "Settings page opened");

        if (UiAutomationHelpers.ClickByName(window, "AI") ||
            UiAutomationHelpers.ClickByNameContains(window, "Local AI"))
        {
            Thread.Sleep(700);
            Record("Settings.AI", "Pass", "AI settings section opened");
        }

        foreach (var marker in new[] { "Enable local AI", "Download AI runtime", "Test connection", "Pull selected model" })
        {
            if (UiAutomationHelpers.FindByNameContains(window, marker) is not null)
            {
                Record("Settings.AI.Controls", "Pass", marker);
            }
        }

        UiAutomationHelpers.ClickByName(window, "About");
        Thread.Sleep(500);
        if (UiAutomationHelpers.WaitForMarker(window, "About", TimeSpan.FromSeconds(3)))
        {
            Record("About", "Pass", "About page reachable");
        }

        UiAutomationHelpers.ClickByName(window, "Back to Settings");
        Thread.Sleep(300);
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
            palette ? "Ctrl+K command palette opened" : "Palette not detected");
        UiAutomationHelpers.SendEscape();
        Thread.Sleep(300);

        if (UiAutomationHelpers.ClickByName(window, "Notification Hub"))
        {
            Thread.Sleep(600);
            Record("Notifications", "Pass", "Notification Hub toggled");
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
    }

    private static void ExploreInstancesAndReturn(AutomationElement window)
    {
        var instanceName = window.FindAllDescendants(window.ConditionFactory.ByControlType(ControlType.Text))
            .Select(e => e.Name)
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

        var listItems = window.FindAllDescendants(window.ConditionFactory.ByControlType(ControlType.ListItem));
        foreach (var item in listItems)
        {
            try
            {
                var texts = item.FindAllDescendants(item.ConditionFactory.ByControlType(ControlType.Text))
                    .Select(t => t.Name)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();
                if (texts.Any(t => t.Contains("U", StringComparison.Ordinal) && t.Length <= 3) ||
                    texts.Any(t => t.Contains("Waiting", StringComparison.OrdinalIgnoreCase)))
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

        var whatsAppVisible = window.FindAllDescendants(window.ConditionFactory.ByControlType(ControlType.Text))
            .Any(e => (e.Name ?? string.Empty).Contains("WhatsApp", StringComparison.OrdinalIgnoreCase));
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
        window.FindAllDescendants(window.ConditionFactory.ByControlType(ControlType.Text))
            .Select(e => e.Name)
            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n) &&
                                 n.Contains("Showing:", StringComparison.OrdinalIgnoreCase)) ?? "(none)";

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
            var matches = window.FindAllDescendants(window.ConditionFactory.ByControlType(ControlType.Text))
                .Select(e => UiAutomationHelpers.SafeName(e))
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
                .Select(t => t.Name)
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
        Findings.Add(new ExplorationFinding(DateTime.UtcNow, area, severity, detail));
        AppendLogLine($"[{severity}] {area}: {detail}");
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
        AppendLogLine($"Finished: {DateTime.UtcNow:O}");
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
