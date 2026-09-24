---
id: v6-a-hidden-grid-child-and-a-view-placed-by-stylesheet-numbers
date: 2026-09-24
agent: claude
title: display none takes a child out of the grid, so a column kept for it strands the one that is left; and a native view must be laid on a measured rect, never on numbers copied from the stylesheet
triggers: [dock, dock-bar, page-slot, split wide, full width, grid-template-columns, display none, setBounds, WebContentsView, zoom, zoomFactor, bar too short, view not resizing, 4.23]
files: [v6/ui/tokens.css, v6/app/main.ts, v6/ui/screens/work.tsx, v6/app/preload.cjs, v6/tests/smoke.spec.ts]
cost: Shipped. The owner saw a dock bar that stopped two-thirds of the way across, with bare window behind it.
evidence: 2026-09-24 the probe read .dock as 1px wide while .dock-bar, .dock3 and .page-slot all sat at 1131px, their own content width, and main had placed the page at the full 1356
status: live
---

Two faults, one habit: the same layout was written down in two places and the copies drifted.

**1. A hidden grid child is not a narrow grid child.** Full width was written as

```css
.split.wide { grid-template-columns: 0 minmax(0, 1fr); }
.split.wide > .main { display: none; }
```

`display: none` takes `.main` out of the grid altogether, so `.dock` became the **first** item and landed in the
`0` column. Its children then overflowed and sized themselves to their own content — which is why the bar
stopped where its last button did and bare `--ground` showed for the rest of the row. Hide a child and the
template must lose its column too: `grid-template-columns: minmax(0, 1fr)`, one column for the one child left.

A second, opposite symptom came from the same place. `.dock`'s column was implicit, so it was `auto`, which
cannot shrink below its content: once the window was narrow (or zoomed), the bar's buttons pushed the whole
dock — and the page slot inside it — **wider than the window**. A container that something is laid over needs
`grid-template-columns: minmax(0, 1fr)` and `min-width: 0`, and the bar needs to be allowed to give way
(`min-width: 0`, the name truncating, the buttons `flex: 0 0 auto`).

**2. A native view must be laid on a rect somebody measured.** `app/main.ts` placed the account's page from
`BAR`, `RAIL`, `LINE`, `DOCK_BAR` and `CUSTOMER` — constants copied from `tokens.css`, with a comment telling
the next person to change both together. They still drifted: the page sat 1px left of its slot even when
nothing was wrong, because nobody had counted the dock's border. And the constants are **CSS pixels**, while
`setBounds` wants the window's own; the two stop being the same the moment the window is zoomed, so on a
zoomed PC the page landed over the panels or short of the edge.

The dock now measures `.page-slot` with a `ResizeObserver` and sends the rect; main multiplies by
`webContents.getZoomFactor()` and clamps it inside the window, keeping the constants only as the opening guess
before the dock has been on screen once. One place knows the layout, and it is the place that draws it.

**Testing it.** Assert the page covers its slot exactly — `view == slot x zoom` — in all four states: panels
open, panels collapsed, zoomed, and a resized window. Make the helper return the two rectangles when they
disagree, not a bare `false`, or the failure tells you nothing. Both halves were broken on purpose to watch it
go red: the old template gave a bar 32px wide, and ignoring the measured slot showed the 1px drift at zoom 1.
