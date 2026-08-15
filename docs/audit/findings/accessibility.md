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

---

### F-A11Y-03 — Second sweep: the remaining pages, and what the static analyser got wrong

- **Severity:** S2
- **Confidence:** confirmed for the controls fixed; **partial** for live verification (see below)
- **Status:** **FIXED** in `v4.99.16`.
- **Method.** Rather than navigate every page by hand, all 144 XAML files were swept for interactive
  declarations carrying no name source (no `AutomationProperties.Name`, no string `Content`, no string
  `Header`). That flagged **32** declarations. Live enumeration then showed the sweep **massively
  over-reports**, and both reasons are worth recording because they would mislead anyone repeating this:
  1. **`<ToggleSwitch.Header>` as a child element.** Settings declares its toggles with a `StackPanel`
     inside a `Header` *property element*, which a regex over the opening tag cannot see — but WinUI **does**
     derive an accessible name from it. Static sweep flagged 23 controls in `SettingsPage.xaml`; live
     enumeration found **5**, and none of those 5 were the toggles.
  2. **`<Button.Flyout>` matches `<Button\b`.** A property element was counted as a control declaration —
     `DashboardPage.xaml`'s flagged entry was this, and `PersonalButton` above it was already correctly
     named.
  **Static analysis alone would have produced a wrong finding here.** The live tree is the authority.
- **Genuinely unnamed and now fixed** (9 declarations across 6 files):
  | Control | File | Name given |
  |---|---|---|
  | `ExportButton` | `AnalyticsPage.xaml` | "Export analytics data" |
  | `RangeBox` | `AnalyticsPage.xaml` | "Date range for analytics" |
  | `AccountSelector` | `ActivityPatternsPanel.xaml` | "Filter activity by account" |
  | `RangeSelector` | `ActivityPatternsPanel.xaml` | "Date range for activity patterns" |
  | `RangeBox` | `ReportsPage.xaml` | "Reporting period" |
  | `SearchBox` | `CommandCenterPanel.xaml` | "Filter accounts or locations" |
  | report-reminder dismiss | `CommandCenterPanel.xaml` | "Dismiss report reminder until next week" |
  | per-notification dismiss | `NotificationFeedPanel.xaml` | "Dismiss notification" |
  | (5 live instances of the above notification button) | — | — |
- **Two sub-patterns worth naming**, both of which recurred:
  - **`PlaceholderText` is not an accessible name.** `SearchBox` had a perfectly good placeholder and
    announced nothing. Screen readers do not substitute placeholder text for a name.
  - **A control inside a `DataTemplate` repeats.** The notification dismiss button appeared 5 times in the
    live tree, all unnamed — one declaration, five silent controls.
- **Live verification after the fix:**
  ```
  Dashboard   interactive=87  unnamed=0
  Settings    interactive=87  unnamed=0
  ```
- **NOT live-verified:** Analytics, Reviews, Reports and About. Their flagged controls were fixed by
  inspection, and the fixes are the same one-attribute pattern verified elsewhere, but I could not drive
  UI Automation onto those pages to confirm — the section-link buttons did not respond to programmatic
  invocation in the state the app was in. Stated rather than assumed.

---

## Observed in passing — confirms F-ORCH-06 is worse than recorded

Enumerating Settings surfaced the accessible names, which are also the visible labels. The developer
vocabulary recorded in F-ORCH-06 is therefore **also what a screen reader reads aloud**:

> "Refresh all WebViews" · "Enable lazy WebView loading" · "Enable per-instance sleep unload" ·
> "Enable edit instance metadata" · "Enable import export instances" · "Enable instance notes and tags" ·
> "Compact Operations card density" · "Export instances" · "Import instances"

A sighted owner can at least infer meaning from surrounding layout; a screen-reader user gets only the
string. That raises the practical severity of F-ORCH-06 without changing its S3 classification, and is
recorded here so the copy pass is not treated as purely cosmetic.

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

---

### F-A11Y-04 — The account card itself, and the review chips, announced nothing

- **Severity:** S2
- **Confidence:** confirmed (identified and verified live)
- **Where:** `Controls/CommandCenterPanel.xaml.cs` `BuildRow` (the per-account `Expander`)
- **Where:** `Controls/ReviewHealthPanel.xaml.cs:150` (per-account "N to reply" chip), `:240` (per-review row)
- **Status:** **FIXED** in `v4.99.20`.
- **How it was found.** Three unnamed buttons persisted on the dashboard through two previous accessibility
  increments. Earlier passes could not identify them because `TreeWalker.ControlViewWalker.GetParent`
  returned only the top-level window. Enumerating each unnamed button's **descendants** instead was what
  cracked it — the first one contained "Depilex F-11 WhatsApp", "140 awaiting", "Longest wait: 66d…", i.e.
  the entire account card. The card is an `Expander`, and WinUI renders its header as a `Button`; with no
  name on the `Expander`, that button announced only "button".
- **User-visible symptom:** the primary control on the product's main screen — one per account, the thing a
  keyboard user tabs to and activates to see who is waiting — told a screen-reader user nothing. Its
  children were individually named, so the *contents* could be read, but the control being focused
  identified neither the account nor what activating it would do.
- **Fix applied:** the `Expander` is named
  `"{account}: {N} customers waiting. Expand to see who is waiting."`. The two `ReviewHealthPanel` buttons
  were named in the same pass — the per-account chip carries its location
  (`"{account}: N reviews need a reply."`) for the same reason the awaiting pills do, and each pending
  review row names its reviewer so the rows are distinguishable, which is the point of the list.
- **Evidence (live, dashboard):**
  ```
  before: interactive=42  UNNAMED=3
  after : interactive=42  UNNAMED=0
    "Depilex F-11 WhatsApp: 140 customers waiting. Expand to see who is waiting."
    "Depilex DHA-2 WhatsApp: 178 customers waiting. Expand to see who is waiting."
    "Depilex Men DHA-2 WhatsApp: 96 customers waiting. Expand to see who is waiting."
  ```
- **Not verified live:** the two `ReviewHealthPanel` names. They only render when a Google account has
  unanswered reviews, and this machine's review data reads "Not scanned yet", so the chip and row branches
  never ran. Fixed by inspection, same one-attribute pattern as the verified ones.

---

# Tab order walked, and the screen-reader experience (session 2, `v4.99.27`)

Session 1 read the UIA tree but never walked focus. This drove `Tab` through the running app with UIA
reporting the focused element at each stop.

**Method note worth keeping.** The first walk leaked: after eight stops the app lost foreground and the
synthetic keystrokes went to a different application, which happily reported *its* controls as the "tab
order". Eighteen of the first twenty-six stops were another program. Any focus walk driven by injected
input has to re-assert the foreground window **and** discard stops whose `ProcessId` is not the app's,
or it produces a confident, entirely fictional result.

## Result: 51 stops, zero unnamed, cycle closes

| Check | Result |
|---|---|
| Distinct focus stops in one cycle | **51** |
| Unnamed stops | **0** |
| Focus traps / dead ends | **none** — the order repeats after 51, so every stop is reachable and escapable |
| Order follows reading order | **yes** — title-bar chrome → sidebar nav → locations and their accounts → command-centre controls → account cards → footer nav, then wraps |
| Names carry state, not just identity | **yes** — e.g. *"Depilex F-11 WhatsApp: 97 customers waiting. Activate to work through this account's replies."* |

Locations group their accounts in visual order, and each account card contributes exactly three stops
(expand, details, activate) in a consistent pattern.

## F-A11Y-05 — an account row never said it could be opened

- **Severity:** S3 · **Confidence:** confirmed (observed in the walk, fixed, re-tested) · **Status:** **FIXED**

The walk put the defect and its own solution next to each other:

```
[Group] 'DHA-2 location, 2 accounts, press to collapse or expand'
[Group] 'Depilex DHA-2 WhatsApp, WhatsApp'
```

A sidebar row is a `Border` with a `KeyDown` handler, not a `Button`. It exposes **no Invoke pattern**, so
a screen reader announces a plain `Group` with no indication that it does anything. Enter and Space *do*
open the account (`InstanceRow_KeyDown`), so the capability was there and only the affordance was missing
— and the location header immediately above already solved it, by baking the action into the name.

Fixed the same way: unselected account rows now end with "press to open". Placed **last** in the name, so
a user who interrupts the announcement still hears the account, its status and its unread count first; and
omitted when the row is the selected one, where it would be noise.

Not fixed by giving the row a real `Button` control type and an Invoke pattern, which is the more correct
answer. That needs a custom `AutomationPeer` on a control used in the app's busiest surface, and the
affordance — the thing a user actually loses — is fully addressed by the name. Recorded as the better fix
if anyone revisits.

## What this does NOT establish

- **No screen reader was run.** This reads the UIA tree Narrator and NVDA consume, in focus order, which
  is much closer to the real experience than a static dump — but nobody listened to it. Announcement
  verbosity, punctuation handling and interruption behaviour are unverified.
- **Only the dashboard shell was walked.** Settings, Analytics, Reviews, Reports and every dialog have
  their own focus orders, none of them walked.
- **Shift+Tab was not tested**, so reverse order is unverified.
- **No modal was walked**, so focus containment inside a `ContentDialog` — and whether focus returns
  sensibly on close — is untested.
