# Remaining issues log (Wave 12 re-audit)

Issues that do **not** block continued delivery but prevent claiming full completion criterion #6 and release tagging until resolved or explicitly accepted.

## Resolved — deferred items (Wave 13)

| ID | Resolution |
|----|------------|
| D1 | OCC split into partial classes; main `OperationsCommandCenter.xaml.cs` under 400 lines; layout/interaction helpers extracted |
| D2 | Dashboard/OCC/Personal/Settings use `ApplicationServices` via `RegistryNavigationArgs.Services` |
| D3 | Shared `ChartBarRenderHelper` for weekly/sentiment chart bar rendering |
| D4 | `System.Drawing.Common` 8.0.0 pinned in `UnifiedMessenger.UiSmokeTests` |
| D5 | OCC keyboard reorder helper (`Alt+Up/Down`) unit-tested; tab-order constants unchanged |
| D6 | Custom VM bar charts retained; no LiveCharts dependency |

## Resolved in program (reference)

| Original | Resolution |
|----------|------------|
| A1 Dual refresh subscribers | `DashboardRefreshCoordinator` (Wave 2) |
| A4 Read-path `Changed` storms | `RefreshOperationalFlags(raiseChanged: false)` (Wave 2) |
| S1–S10 security items | Wave 1 + re-audit Wave 11 |
| Legacy branch filter / control center | Removed Wave 4 |
| Dashboard client-side branch scoping | Removed (locked decision) |

## Release readiness

**Tag release when:** automatable criteria pass (see `completion-criteria.md`) and UiSmoke green on CI.
