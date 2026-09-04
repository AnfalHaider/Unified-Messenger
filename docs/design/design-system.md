# The design system — where everything lives

**Written:** 2026-09-04 · **Baseline:** v4.99.87 (Increment 121).

One page answering "if I want to change how the app looks, what do I edit?" — written because a change
that should be one edit was taking a search each time, and because two of the guards below only exist
now that someone went looking.

---

## The short answer

| To change… | Edit | Guarded by |
|---|---|---|
| A colour | `UnifiedMessenger/Themes/Tokens.xaml` — **both** theme dictionaries | `DesignTokenConsolidationTests` · `StatusContrastTests` |
| A font size, icon size or spacing step | `Tokens.xaml`, then the matching constant in `Services/UmScale.cs` | `DesignScaleTests.TheCodeScaleMatchesTheXamlTokens` |
| A control's look | `Themes/Controls.xaml` | — |
| Type ramp definitions | `Themes/Typography.xaml` | `DesignScaleTests` |
| High-contrast overrides | `Themes/HighContrast.xaml` | — |

**There is no build-time code generation, and deliberately so.** `UmScale` duplicates the numbers from
`Tokens.xaml` by hand, because code-built controls must never make a UI-thread WinRT call to read a
resource — that mistake terminated the process once already. Duplication is safe only while something
proves the copies agree, and `DesignScaleTests` does exactly that: it parses `Tokens.xaml` and asserts
every value in `UmScale` matches. Change one, the build tells you to change the other.

---

## The four vocabularies, and why there are four

This looked like sprawl and mostly is not. Each exists for a reason that survives inspection:

1. **`Themes/Tokens.xaml`** — the definition. Colours, sizes, spacing, radii, opacities. Colours live
   inside `<ResourceDictionary.ThemeDictionaries>` with a `Light` and a `Default` (dark) block.
2. **`Services/UmScale.cs`** — the numbers, for code-built controls, mirrored and test-locked (above).
3. **`Services/UmSemanticBrushes.cs`** — brush *keys* as constants, so a call site names
   `UmSemanticBrushes.StatusDangerBrushKey` rather than retyping a string.
4. **`Services/ThemeBrushResolver.cs`** — the only correct way to turn a key into a brush in code.

**Rule 4 is the one that keeps being violated.** `Application.Current.Resources` resolves the
**app-default** theme, not the element's — and this app themes the *window root*, not the application, so
it reads Light even in dark mode. That covers every `Um*` token, because `Tokens.xaml` declares them
inside theme dictionaries. Eight files had each rolled their own private lookup, which is why
white-in-dark surfaces kept reappearing after every fix across v4.99.61–63.
`DesignTokenConsolidationTests.NoCodeBuiltBrushIsResolvedFromApplicationResources` now fails the build
on a new one.

Also: resolve brushes on `ActualThemeChanged` / `Loaded`, **never once in a constructor**. A control
being constructed is not in the visual tree, so its `ActualTheme` is `Default` — that baked light tiles
into `KpiStatCard` permanently.

---

## Colour: the status palette and its washes

Six semantic colours (`Success`, `Warning`, `Danger`, `Info`, `Neutral`, `Muted`) and five background
washes (`Success`, `Warning`, `Danger`, `Info`, `Neutral`). Every one is defined in both themes.

**The Windows system fill brushes are now banned outright, not merely rationed.** They took their colour
from the OS and were never re-measured. The count went 69 → 10 at v4.99.36, and stuck at 10 for three
releases for a stated reason: there was no audited token to move an *attention* or *neutral* background
onto. Increment 121 added `UmStatusInfoWashBrush` and `UmStatusNeutralWashBrush`, migrated the last ten
references, and changed the ceiling from `<= 10` to `== 0`.

> ⚠️ `StatusContrastTests` counts **raw text occurrences** across every `.cs` and `.xaml` file under the
> app directory. Writing the prefix in a *comment* fails the build exactly as a real usage would. That
> cost a build during Increment 120; it is recorded here and in the test so it costs no one else.

Contrast is measured, not assumed. Each status colour is checked against the light card, the dark card
and the dark chrome at 4.5:1 — and, new in Increment 121, **against its own wash in both themes**, which
is the pairing a chip actually renders. The washes shipped for three releases with no contrast test at
all, on the reasoning that a background is measured by whatever is drawn on it. True, and nothing was
measuring that either.

---

## Component inventory

**Charts** (`Controls/Charts/`) — `KpiStatCard`, `BarChartView`, `AreaLineChartView`, `DonutChartView`,
`DeltaBadge`. All used by `AnalyticsPage`.

**Shared** (`Controls/Shared/`) — `SurfaceCard`, `MetricCardView`, `SectionHeaderView`, `EmptyStateView`,
`LoadingOverlayView`, `MiniSparkline`, `AwaitingChatActions`. `AwaitingChatActions` is the single
mark-handled/snooze control; use it on **every** awaiting row, so a surface cannot ship without one.

**Panels** (`Controls/`) — `CommandCenterPanel`, `WorkspaceSidebar`, `ReviewDesk`, `NotificationFeedPanel`,
`PersonalOverviewPanel`, `ActivityPatternsPanel`, `CommandPalette`, `DashboardSectionLinks`.

### One genuine orphan

**`Controls/MessageVolumeLineChart.xaml`** is declared and instantiated nowhere. Only
`Services/MessageVolumeLineChartHelper.cs` carries the name, and that is a separate pure-logic type that
does not use the control. `AreaLineChartView` appears to have superseded it.

Left in place rather than deleted, because removing a control is a decision with its own reasoning and
this increment was about tokens. It is recorded here so the next person does not spend the search
finding out it is dead.

*(An earlier note in this stream called `KpiStatCard` an orphan. That was wrong — `AnalyticsPage.xaml`
instantiates three of them.)*

---

## The guards, and what each one catches

| Test | Catches |
|---|---|
| `DesignScaleTests.TheCodeScaleMatchesTheXamlTokens` | `UmScale` drifting from `Tokens.xaml` |
| `DesignScaleTests` (literals) | A raw font/icon size in `.xaml` or `.cs` instead of a token |
| `DesignScaleTests.NoFontIconShipsWithAnEmptyGlyph` | An invisible but still-clickable icon button |
| `DesignTokenConsolidationTests.EveryBrushKeyNamedInCodeExists` | A typo'd brush key — which does **not** throw; the lookup silently falls back |
| `DesignTokenConsolidationTests.EveryBrushIsDefinedInBothThemesOrNeither` | A brush themed on one side only |
| `DesignTokenConsolidationTests.EachStatusColourIsReadableOnItsOwnWash` | Unreadable chip text |
| `DesignTokenConsolidationTests.NoCodeBuiltBrushIsResolvedFromApplicationResources` | The private-lookup regression |
| `StatusContrastTests` | Status colours below AA · any return of the system fill palette |
| `AccountVocabularyTests` | The word "instance" in user-visible text |

---

## Adding a token

1. Add it to **both** theme dictionaries in `Tokens.xaml`. One side only is the classic
   unreadable-artifact bug, and a test now fails on it.
2. If code needs it, add the key to `UmSemanticBrushes` and read it through `ThemeBrushResolver`.
3. If it is a number code needs, mirror it in `UmScale` — the parity test will tell you if you forget.
4. If it is a colour text will sit on, add it to the contrast theory data.
