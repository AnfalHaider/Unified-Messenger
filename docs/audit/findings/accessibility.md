# Findings — Accessibility and contrast

Measured against the running app via UI Automation (the same API a screen reader consumes), and against
the shipping theme files by computing WCAG 2.1 ratios directly.

---

### F-A11Y-01 — Nine interactive controls announced nothing to a screen reader, including the primary recovery action

- **Severity:** S2
- **Confidence:** confirmed (enumerated live before and after)
- **Where:** `Controls/CommandCenterPanel.xaml` (`WindowSelector`, `ReportButton`, `ResyncButton`, `DensityToggle`, `DigestDismiss`), `MainWindow.xaml` (`PanePinButton`)
- **Where:** `Controls/CommandCenterPanel.xaml.cs` — three imperatively built buttons: the needs-reply row, the "Showing: …" filter chip, and the per-account awaiting pill
- **Status:** **FIXED** in `v4.99.15`.
- **User-visible symptom:** A screen-reader user hears **"button"** with no indication of what it does.
  9 of 39 interactive controls (23%) were affected. The worst is `ResyncButton` — **Re-sync is the recovery
  action the product's own copy instructs people to take** ("out of date — click Re-sync", "can't read this
  account — click Re-sync", both written earlier in this same audit). A blind owner was told to press a
  button they could not identify.
  The per-account awaiting pills were as bad in a different way: every card renders one reading "N
  awaiting", so a screen reader announced the same phrase repeatedly with **no way to tell which branch**
  each belonged to.
- **Repro:** enumerate `TreeScope.Descendants` on the main window, filter to interactive control types,
  report those with an empty `Name`.
- **Root cause:** one idiom, applied inconsistently. Every affected control puts a `StackPanel`
  (icon + label) or a bare `FontIcon` in `Content`, so WinUI derives no accessible name from it — a name is
  only inferred when `Content` is a string. The codebase already knows this: `NotificationToggleButton`,
  declared **four lines below** `PanePinButton` in the same file, sets `AutomationProperties.Name`
  explicitly. A `ToolTipService.ToolTip` is not a substitute — screen readers do not announce it in place
  of a name, and several of these controls had a good tooltip and no name.
- **Fix applied:** explicit `AutomationProperties.Name` on all six XAML controls and all three imperative
  ones. The imperative names carry their context rather than repeating the visible label:
  `"Depilex DHA-2 WhatsApp: 163 customers waiting. Activate to work through this account's replies."` and
  `"{customer name}, open conversation"` for needs-reply rows.
- **Evidence (live, same dashboard state, before → after):**
  ```
  before: interactive elements 39, UNNAMED 9
          PanePinButton, WindowSelector, ReportButton, ResyncButton,
          DensityToggle, DigestDismiss, + 3 imperative buttons
  after : interactive elements 42, UNNAMED 3
          then, in a later state: 165 buttons, UNNAMED 0
  announced live: "Re-sync now" · "Open weekly business report" ·
                  "Date range for caught-up percentage" · "Compact view" ·
                  "Dismiss this summary" · "Pin sidebar expanded" ·
                  "Depilex F-11 WhatsApp: 137 customers waiting. Activate to…"
  ```
- **Not fully verified:** the needs-reply **row** name could not be exercised live — I could not get that
  list to render through UI Automation invocation, and the 3 buttons still unnamed in the 42-element
  snapshot were transient and gone before I could identify them. The row fix follows the identical pattern
  to the pills that *are* verified, but I did not see it announced. Stated rather than assumed.

---

### F-A11Y-02 — The brand colour failed contrast in dark theme, on the chart line and the accent button

- **Severity:** S2
- **Confidence:** confirmed (computed from the shipping theme files; regression proven by reverting)
- **Where:** `UnifiedMessenger/Themes/Tokens.xaml` (`UmBrandTealColor`)
- **Where:** consumers — `Controls/MessageVolumeLineChart.xaml:27,32`, `Controls/Shared/EmptyStateView.xaml:17`, `Themes/Controls.xaml:41`
- **Status:** **FIXED** in `v4.99.15`.
- **User-visible symptom:** In dark theme — which is what a Windows machine set to dark gets, since the
  default preference is "System" — the message-volume chart line measured **2.82:1** against the card it is
  drawn on. That fails even the 3:1 WCAG 1.4.11 bar for graphical objects essential to understanding a
  view, and the chart *is* the view. The empty-state icon shared the colour. Separately, the accent button's
  dark-theme text sat at **4.31:1**, a marginal fail on the Re-sync button.
- **Repro:** compute WCAG relative-luminance contrast for `#1B75BB` against `#2D2D30` and `#1E1E1E`.
- **Root cause:** a single brand value used in both themes. `Tokens.xaml` already solves this for
  `UmBrandSlate` via `ThemeDictionaries`, with a comment explaining that token "must lighten on dark or
  section headers/badges vanish" and that **"Consumers MUST use `{ThemeResource ...}`, not
  `{StaticResource ...}`, for these to switch."** `UmBrandTeal` never got that treatment, and all four of
  its consumers used `StaticResource` — so even adding a theme dictionary alone would have changed nothing.
  Compounding it, `docs/design-system/contrast-audit.md` still described the *previous* brand colour
  `#14B8A6`, reporting a light-theme failure for a colour that no longer shipped, while the real failure
  had moved to dark theme.
- **Fix applied:** `UmBrandTealColor`/`Brush` moved into `ThemeDictionaries` — `#1B75BB` light,
  **`#58A6FF`** dark — and all four consumers switched to `ThemeResource`. The dark value was chosen by
  measuring candidates against both dark surfaces *and* against the accent button's near-black foreground,
  because lightening a colour used as a background could have made that role worse rather than better.
  It improves both roles:
  | | before | after |
  |---|---:|---:|
  | brand on dark card (chart line, icon) | 2.82:1 | **5.43:1** |
  | brand on dark chrome | 3.42:1 | **6.60:1** |
  | accent button text, dark theme | 4.31:1 | **8.31:1** |
- **Regression proof:** reverting the dark token to `#1B75BB` fails 5 of the 7 `BrandContrastTests` with
  the exact measured ratios — `brand on dark card is 2.82:1, needs 4.5:1`. The tests parse
  `Tokens.xaml` rather than hard-coding hex, so changing the brand without checking contrast now fails the
  build.
- **Residual, recorded not hidden:** `#1B75BB` on `#F3F3F3` (light layer fill) is **4.39:1** — marginal
  fail for normal text, passes the 3:1 graphical bar. Left alone deliberately: the brand is not body text
  on that surface, and darkening it to clear 4.5:1 would cost contrast in the accent-button role where it
  currently passes.

---

## Checks that came back CLEAN

- **Keyboard focusability.** The 11 controls reporting `IsKeyboardFocusable = false` are **all
  system-provided**: the title-bar Minimize/Maximize/Close buttons (reachable via Alt+Space, and never
  focusable in the tab order by Windows convention) and scrollbar repeat buttons (never focusable by
  design). **No application control was unreachable by keyboard.** Recorded because this looked like an
  11-item finding until each was identified.
- **High-contrast theme.** `Themes/HighContrast.xaml` overrides the brand to `#FFFF00`, measured at
  **19.56:1** on black. The runtime-merge mechanism (`ThemeService`) is in place.
- **Existing accessibility helpers exist and are used** — `AccessibilityTabOrderHelper`,
  `FocusTrapHelper` (applied in `AddInstanceDialog`), and `WorkspaceSidebarAccessibility`.

## What was NOT covered

- **Only the dashboard/command-centre surface was enumerated.** Analytics, Reviews, Reports, Settings
  (8 partial sections), About, and all ~15 dialogs were **not** checked for accessible names. Given 23% of
  controls on the one screen examined were unnamed, the prior for the rest should be treated as high.
- **Tab order was not verified.** `AccessibilityTabOrderHelper` exists and is referenced, but I did not
  walk the focus order to confirm it is logical, nor check that focus is visible on every control.
- **No screen reader was actually run.** Findings are from the UI Automation tree, which is what Narrator
  and NVDA consume, but I did not listen to the output. Announcement order and verbosity are unverified.
- **Only the brand token's contrast was measured.** The semantic status colours (success / warning /
  danger, used for the awaiting pill and on-time percentages) and the neutral text ramp were not audited
  in either theme — and status colour is exactly where colour-only encoding tends to hide.
- **Colour-only status encoding was not systematically checked.** `StatusGlyph` pairs a shape-distinct
  glyph with the on-time percentage colour, which is the right pattern, but I did not audit every status
  surface for it.
- **OS text scaling and reduced-motion** were not tested.
