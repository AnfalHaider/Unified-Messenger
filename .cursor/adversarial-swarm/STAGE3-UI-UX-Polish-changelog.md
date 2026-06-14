# STAGE 3 — UI/UX Polish Changelog

**Agent:** Agent-UI-UX-Polish  
**Date:** 2026-06-14  
**Version:** 4.1.0

## P0 — Layout virtualization

- **Work queue:** `ListView` → `ItemsRepeater` + `StackLayout` inside `MainContentScrollViewer` (viewport-driven virtualization).
- **Immediate queue:** Same `ItemsRepeater` migration (collapsed legacy lane).
- **Kanban board:** Three column `ListView` controls → `ItemsRepeater`; board wrapped in horizontal `ScrollViewer` with `UmKanbanColumnMinWidth` / `UmKanbanBoardMinWidth` to fix 150%+ DPI column clipping.
- **Scroll bubbling:** `OccItemsRepeaterHelper` wires per-element wheel bubbling for work/immediate queues; kanban cards wired in `ElementPrepared`.

## P0 — UIA / AutomationId

- `OccBoardViewToggle`, `OccFilterAllOpen`, `OccFilterUrgent`, `OccFilterSla`, `OccFilterHanging`
- `OccWorkQueueSectionLabel` on work queue section header
- Kanban columns retain `OccKanbanNew` / `OccKanbanHanging` / `OccKanbanResolved` when board expanded
- Constants added to `ViewAutomationIds.cs`

## P1 — Design tokens (Phase A–D)

- Semantic status colors + layout tokens in `Themes/Tokens.xaml`; High Contrast overrides in `HighContrast.xaml`
- `UmSemanticBrushes` helper for frozen resource brushes
- `OperationsThreadCardViewModel` and `UnifiedMessengerDashboardPresentationHelper` use semantic tokens (no `Color.FromArgb` / hardcoded hex in ViewModels)
- OCC XAML: metric cards, date pickers, filter chips, AI chip, empty state use token resources
- `AccentButtonStyle` foreground → `TextOnAccentFillColorPrimaryBrush`
- Thread cards: `MinWidth="0"` + `TextTrimming="CharacterEllipsis"` on tag/sentiment/revenue text; SLA stripe uses `UmCornerRadiusXsValue`

## P1 — MessageVolumeLineChart

- Debounced `SizeChanged` (24 ms) with ε-guard on width/height to avoid UI-thread `PathGeometry` rebuild storms

## Misc

- **About page version:** `app.manifest` assemblyIdentity aligned to 4.1.0.0 (was stale 3.7.0.0; About reads assembly version)
- **Settings:** Toggle for `OccCompactCardDensity` under Session & Performance
- **Version bump:** 4.1.0 across csproj, manifest, installer

## Files touched (primary)

| Area | Files |
|------|-------|
| OCC layout | `OperationsCommandCenter.xaml`, `.xaml.cs`, `KanbanColumnBoard.xaml`, `.xaml.cs` |
| Helpers | `OccItemsRepeaterHelper.cs`, `UmSemanticBrushes.cs`, `UmSemanticColors.cs` |
| Cards/chart | `OperationsThreadCardViewModel.cs`, `OperationsThreadCardView.xaml`, `MessageVolumeLineChart.xaml.cs` |
| Themes | `Tokens.xaml`, `HighContrast.xaml`, `Controls.xaml` |
| Settings | `SettingsPage.xaml`, `.xaml.cs`, `SettingsPage.Metrics.partial.cs` |
| Version | `UnifiedMessenger.csproj`, `app.manifest`, `installer-shared.iss` |
| A11y | `ViewAutomationIds.cs` |

## Known limitations

- Kanban within-column drag reorder (ListView `DragItemsCompleted`) replaced by keyboard Alt+↑/↓ reorder + cross-column drop on column zones; pointer drag initiates cross-column transfer only.

## Validation

- Release x64 build (Stage 5)
- Unit tests
