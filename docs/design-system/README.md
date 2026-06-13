# Unified Messenger design system

## Theme resources

| File | Purpose |
|------|---------|
| `Themes/Tokens.xaml` | Brand colors, spacing, corner radius, opacity scale (`UmOpacityMuted`, `UmOpacityHint`) |
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

Operations Command Center uses a **fixed responsive layout** (KPI grid, branch pills, immediate queue, three-column kanban). User-controlled **display order** within kanban columns is persisted in `triage_v2.json` via `ThreadDisplayOrderService`.

## Semantic colors

Delivery and status accents are centralized in `UmSemanticColors` (aligned with brand tokens).

## Adoption

Shared controls are wired into production surfaces:

- **Operations Command Center** — `MetricCardView` KPI row, kanban columns, message volume chart.
- **Settings** — `SectionHeaderView`; section bodies use `SurfaceCard` + `UmSurfaceCardStyle`.
- **Notifications / Personal Overview** — `EmptyStateView`, `LoadingOverlayView`; Personal Overview activity/status panels use `SurfaceCard`.

## Accessibility

- **High-contrast theme** — system high-contrast mode merges `Themes/HighContrast.xaml` overrides at runtime via `ThemeService` (v3.3.0).
