# Unified Messenger design system

## Theme resources

| File | Purpose |
|------|---------|
| `Themes/Tokens.xaml` | Brand colors, spacing, corner radius |
| `Themes/Typography.xaml` | Section headers, metric values, body/caption styles |
| `Themes/Controls.xaml` | Dashboard and surface card border styles |

Merged in `App.xaml`.

## Shared components

| Control | Use |
|---------|-----|
| `EmptyStateView` | Centered empty states with icon, title, hint, optional action |
| `LoadingOverlayView` | Full-surface loading with message |
| `MetricCardView` | KPI label/value/subtext — OCC live-thread KPI row (`IsAccent` for revenue-at-risk) |
| `SectionHeaderView` | Section title with optional badge — Settings sections, OCC KPI header |
| `SurfaceCard` | Standard padded card container — OCC immediate lane, kanban, highlights |
| `AccessibleChartHost` | Chart wrapper with automation summary — sentiment and weekly activity charts |
| `OperationsThreadCardView` | OCC/kanban thread cards |

## OCC layout

Operations Command Center uses a 12-column grid (`OccLayoutGridEngine`) with presets:

- `operations-focus` (default)
- `analytics-focus`
- `compact`

Persisted in `settings.json` as `occPanelPlacements`.

## Adoption (Workstream B)

Shared controls are wired into production surfaces:

- **Operations Command Center** — `MetricCardView` KPI row, `SectionHeaderView` for the live-thread header, `SurfaceCard` for immediate lane / kanban / highlights panels; charts use `AccessibleChartHost` internally.
- **Settings** — all section labels use `SectionHeaderView`.
- **Notifications / Personal Overview** — already use `EmptyStateView` and `LoadingOverlayView`.
