> ⚠️ **Superseded by [MASTER-PLAN.md](../MASTER-PLAN.md)** (2026-06-16). Historical; the master plan is the living source of truth.

# v2.0 Re-audit checklist

Assessment after v2.0 roadmap implementation (June 2026).

## Architecture

| Item | Status |
|------|--------|
| `ShellController` extracted from `MainWindow` | Pass — `MainWindow.xaml.cs` ≤450 lines |
| Shell layer DI gate (no forbidden singletons) | Pass — CI + `V2MigrationGateTests` |
| `OccLayoutInteractionService` for keyboard nudge/resize | Pass |
| Grid-only persist on custom layout save | Pass — legacy lists derived on load only |

## Security

| Item | Status |
|------|--------|
| WebMessage requires `instanceId` | Pass |
| `WebViewNavigationGuard` http/https only | Pass |
| `WebViewScriptGateway.ExecutePreparedScriptAsync` | Pass |

## UX / Professional ops

| Item | Status |
|------|--------|
| Branch filter chip in KPI strip | Pass |
| OCC palette actions (refresh, branch filter, immediate queue) | Pass |
| SLA countdown on thread cards | Pass |
| Conversation focus loading indicator | Pass |
| Command palette category icons | Pass |
| Settings keyboard shortcut reference | Pass |
| OCC layout cheat-sheet teaching tip | Pass |

## Quality

| Item | Status |
|------|--------|
| Coverlet on unit test project | Pass |
| BenchmarkDotNet project (`UnifiedMessenger.Benchmarks`) | Pass |
| UiSmoke `Warn` treated as failure | Pass |
| CI vulnerable package scan | Pass |

## Manual release verification

- OCC layout edit full keyboard path — verified via UiSmoke + local install (v2.0.0)
- Branch filter chip clear action — verified via unit tests + local install
- Notification → conversation focus under load — verified via shell navigation tests + local install
- Light/dark contrast on teal status text — verified (`UmBrandTealDarkBrush` + contrast audit)

## Script gateway consolidation

| Call site | Status |
|-----------|--------|
| `WebViewDraftInjector` | Pass — `ExecutePreparedScriptAsync` |
| `ConversationContextScraper` | Pass — `ExecutePreparedScriptAsync` |
| `DashboardScrapeOrchestrator` | Pass — `ExecutePreparedScriptAsync` |
