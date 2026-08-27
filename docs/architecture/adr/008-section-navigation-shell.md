# ADR-008: Section navigation shell

## Status

Accepted (v4.95.0) — WP0 of the UI vision work-package plan (that planning document was internal scaffolding and is no longer tracked; the shipped result is this ADR plus `docs/design-system/`).

## Context

The app had **three pages** (Dashboard, Settings, About) and a sidebar that listed only accounts. The UI
vision calls for a section-based left navigation, and every later work package routes through it.

Two things made this the risky change rather than a cosmetic one:

1. **"Which destination is active" was three booleans plus a nullable string**, duplicated across
   `ShellNavigationCoordinator`, `MainWindowViewModel`, `ShellSelectionState` and
   `WorkspaceSidebarHelper`. Eight sections × four places is combinatorial. The evidence that this was
   already failing: `IsWorkQueueSelected` threaded through six files, always `false`, for a feature that
   had been removed.
2. **WebView2 renders above XAML.** Navigation surfaces must be real grid columns, never overlays, or
   they vanish the moment an account is open.

## Decision

**A `ShellSection` enum replaces the boolean encoding.** The sidebar's selection output was already
string-keyed (`ResolveSelectionKey` → `"dashboard"`, an instance id, …), so only the *input* needed
formalising. `Models/ShellViewState.cs` existed for exactly this purpose and was wired to nothing; it was
replaced rather than left as a second parallel concept. `IsWorkQueueSelected` was retired in the same
change — it was the precedent for exactly the debt this refactor would otherwise multiply.

An account is deliberately **not** a section. Selecting one collapses the `ContentFrame` and shows the
per-account WebView instead; section and instance are different axes, and the section is preserved so
leaving an account returns you where you were.

**The existing `WorkspaceSidebar` was extended in place, not replaced.** A second rail would have cost
~376px of chrome, forced a decision about which rail the pin / hover-expand / pane-toggle machinery
owns, and broken roughly sixteen consumers (7 events, 9 public methods, `IShellUiHost` membership, UI
smoke automation names). Section rows are emitted by the existing declarative
`WorkspaceSidebarMenuPlanner` and built through the existing `CreateSelectableRow`, so selection visuals,
compact density, focus and tab order need no second code path.

**One `ShowSectionAsync` replaces per-destination copies.** It generalises the eleven-step
`ShowDashboardAsync` shape; `ShowDashboardAsync` and `ShowSettingsAsync` now delegate to it.

## What was deliberately left out

- **Inbox.** No local store holds message history — every store caps at one preview string per chat, and
  `TranscriptBuilder` builds a single-message prompt fragment, not a transcript. A placeholder would
  imply data the app does not have. WP4 must settle the data story first.
- **Notifications and Settings.** Both already exist as sidebar *footer* buttons with working badges and
  automation ids. Notifications currently toggles the dock, and the dock's fate is explicitly WP7's
  decision. Moving them now would duplicate destinations and change behaviour prematurely; a footer is
  part of the rail, and bottom-anchored Settings is conventional.
- **New keyboard chords.** `Ctrl+1..9` is already instance selection. The sections are reachable from the
  command palette via a single `OpenSection` action carrying its target — one action rather than one per
  destination, so the dispatch switch does not grow per section.

## Consequences

- Adding a destination is now an enum member, a planner row, and a page — not four boolean edits.
- Analytics, Reviews and Reports become browsable surfaces backed by panels that already worked
  (`ActivityPatternsPanel`, `ReviewHealthPanel`, and the report body shared with `WeeklyReportDialog`
  via `WeeklyReportDialog.Populate`, so the page and dialog cannot drift).
- `ReviewsPage` needs its own empty state: `ReviewHealthPanel` self-collapses when no Google Business
  account exists, which is right for a dashboard card but renders a blank page.
- The last-visited section persists (`AppSettings.LastVisitedSection`, schema v19) and is parsed
  defensively, so a stale or hand-edited value cannot stop the shell from opening.
- The sidebar footer was left untouched, which is also what keeps the UI smoke harness passing — it
  clicks `Sidebar Dashboard` and `Add Instance` by automation name.
