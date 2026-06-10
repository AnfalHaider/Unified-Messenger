# ADR: OCC 12-column grid layout

## Status

Accepted (v1.1)

## Context

OCC previously used two fixed `StackPanel` columns with list-based panel order. Users need cross-column moves, resize spans, hide/show, and presets.

## Decision

- Use a **12-column CSS-like grid** with `OccPanelPlacement` records (`column`, `row`, `columnSpan`, `rowSpan`, `isVisible`).
- Keep **thread display order** separate in `RichTriageStore` (visual kanban/immediate order only).
- Enforce **minimum column spans** per panel type to preserve usability.
- Migrate legacy `occActionPanelOrder` / `occContextPanelOrder` on first load when `occPanelPlacements` is empty.
- Ship three **layout presets**; custom layouts clear `occLayoutPresetId`.

## Consequences

- `AppSettings` version bumped to 5.
- `OccLayoutGridApplier` positions WinUI `Grid` children at runtime.
- Keyboard: arrow keys move, Shift+arrow resize, Delete hides panel.
