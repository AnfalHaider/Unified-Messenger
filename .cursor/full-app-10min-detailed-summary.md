# Full App 10-Minute Detailed Exploration — Executive Summary

**Run:** 2026-06-14 15:35:39 UTC → 2026-06-14 15:45:48 UTC
**Planned duration:** 10 min | **Actual:** 10.1 min
**Cycles completed:** 6
**Executable:** installed Unified Messenger v4.0.0

## Totals
- Pass: **142** | Warn: **86** | Info: **85** | Fail: **14**

## By area
- **AddInstance**: pass=6, warn=0, fail=0
- **CommandPalette**: pass=6, warn=0, fail=0
- **Instances**: pass=5, warn=1, fail=0
- **Launch**: pass=1, warn=0, fail=0
- **Notifications**: pass=6, warn=0, fail=0
- **OCC**: pass=51, warn=72, fail=14
- **PersonalOverview**: pass=0, warn=6, fail=0
- **Settings**: pass=49, warn=7, fail=0
- **Sidebar**: pass=18, warn=0, fail=0

## vs prior ~2 min crash run
- Prior run crashed at cycle 1 with `Name [#30005]` after work-queue navigation.
- This run uses `SafeName`/`SafeTextNames` and per-phase try/catch so UIA property errors do not abort the harness.
- Completed 6 cycles; compare cycle timing to prior audits.

## KPI / Kanban UIA (board expanded)
- Board-expanded kanban: No board-expanded kanban probe recorded
- AutomationId=OccKpiOpenThreads — card not found in UIA tree
- AutomationId=OccKpiUrgent; value=unreadable; enabled=True
- AutomationId=OccKpiSlaBreaches; value=unreadable; enabled=True
- AutomationId=OccKpiHangingLeads — card not found in UIA tree

Full log: `D:\Projects\Unified Messenger\.cursor\full-app-10min-detailed-log.txt`
