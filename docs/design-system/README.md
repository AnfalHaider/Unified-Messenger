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
| `EmptyStateView` | Icon + title + hint + optional action — every no-data surface |
| `LoadingOverlayView` | Full-surface loading with message |
| `MetricCardView` | KPI label / value / subtext |
| `MiniSparkline` | Micro-trend under a KPI tile |
| `SectionHeaderView` | Section title with optional badge — Settings sections, page headers |
| `SurfaceCard` | Standard padded card container |
| `AwaitingChatActions` | Mark-handled / snooze on an awaiting row — use on EVERY awaiting surface |

## Charts (`Controls/Charts`)

`BarChartView`, `AreaLineChartView`, `DonutChartView`, `KpiStatCard`, `DeltaBadge`. Each carries its own
automation summary; `AccessibleChartHost` was deleted in v4.99.47 as unreachable.

Type, icon, spacing and radius scales live in [scales.md](scales.md).

## Semantic colors

Delivery and status accents are centralized in `UmSemanticBrushes` (kept in lockstep with `Themes/Tokens.xaml` by `StatusContrastTests.TheCodePaletteMatchesTokensXamlExactly`). A third copy, `UmSemanticColors`, was deleted at v4.99.68 — it was `const string`, so it could not be theme-aware, nothing checked it, and its values were a mix of light-theme, dark-theme and neither.

## Adoption

Shared controls are wired into production surfaces:

- **Operations Command Center** — `MetricCardView` KPI row, kanban columns, message volume chart.
- **Settings** — `SectionHeaderView`; section bodies use `SurfaceCard` + `UmSurfaceCardStyle`.
- **Notifications / Personal Overview** — `EmptyStateView`, `LoadingOverlayView`; Personal Overview activity/status panels use `SurfaceCard`.

## Accessibility

- **High-contrast theme** — system high-contrast mode merges `Themes/HighContrast.xaml` overrides at runtime via `ThemeService` (v3.3.0).
