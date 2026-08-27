# Brand contrast audit (WCAG 2.1 AA)

> **Re-measured 2026-08-11 during the product-hardening audit.** The previous revision of this file audited
> `#14B8A6` (teal). That is **no longer the brand colour** — the token had been changed to `#1B75BB` (blue)
> without this document being updated, so it described a failure in the wrong theme for a colour that was
> not shipping. The numbers below are computed from `Themes/Tokens.xaml` as it stands and are pinned by
> `BrandContrastTests`, which reads the XAML directly so this cannot silently drift again.

Brand token: `UmBrandTealColor` — used for the message-volume chart line and fill, the empty-state icon,
and `AccentButtonStyle`'s background (which is what the **Re-sync** button uses).

## Method

WCAG 2.1 relative luminance on sRGB. AA requires **4.5:1** for normal text, and **3:1** for large text and
for graphical objects essential to understanding a view (1.4.11 non-text contrast). The chart line and the
empty-state icon are exactly such graphical objects; they are held to the stricter 4.5:1 bar here because
nothing was gained by being lenient.

## Current values

| Theme | Token | Rationale |
|---|---|---|
| Light | `#1B75BB` | unchanged |
| Dark | `#58A6FF` | lightened — see below |

| Foreground | Background | Ratio | Normal text (4.5:1) | Large / UI (3:1) |
|---|---|---:|---|---|
| `#1B75BB` | `#FFFFFF` (light card) | **4.87:1** | Pass | Pass |
| `#1B75BB` | `#F3F3F3` (light layer fill) | 4.39:1 | Marginal fail | Pass |
| `#58A6FF` | `#2D2D30` (dark card) | **5.43:1** | Pass | Pass |
| `#58A6FF` | `#1E1E1E` (dark chrome) | **6.60:1** | Pass | Pass |
| `#FFFF00` | `#000000` (high contrast) | 19.56:1 | Pass | Pass |

### Accent button — the brand as a *background*

`AccentButtonStyle` paints the brand behind `TextOnAccentFillColorPrimaryBrush`, which flips per theme, so
lightening the dark value had to be checked from this direction too.

| Theme | Text | Background | Ratio | Verdict |
|---|---|---|---:|---|
| Light | white | `#1B75BB` | 4.87:1 | Pass |
| Dark (before) | near-black | `#1B75BB` | 4.31:1 | **Fail** |
| Dark (after) | near-black | `#58A6FF` | **8.31:1** | Pass |

## What was wrong, and what changed

`#1B75BB` measured **2.82:1 on a dark card** — failing even the 3:1 non-text bar — while being the colour
of the message-volume chart line and the empty-state icon. It also put the accent button's dark-theme text
at 4.31:1, a marginal fail on the product's primary recovery action.

Two changes:

1. `UmBrandTealColor` / `UmBrandTealBrush` moved into `ResourceDictionary.ThemeDictionaries`, with a
   lighter `#58A6FF` for dark. This mirrors what `UmBrandSlate` in the same file already did, for the same
   reason — that token carries a comment explaining it "must lighten on dark or section headers/badges
   vanish".
2. All four consumers switched from `{StaticResource UmBrandTealBrush}` to `{ThemeResource ...}`. Without
   this the theme dictionary has no effect — the same file's own comment warns of it, and every consumer
   was using `StaticResource`.

## Known remaining gap

`#1B75BB` on `#F3F3F3` (light layer fill) is **4.39:1** — a marginal fail for normal text, passing the
3:1 graphical bar. Left as-is deliberately: the brand is not used as body text on that surface, and
darkening it enough to clear 4.5:1 there would cost contrast in the accent-button role where it currently
passes. Recorded rather than silently accepted.

## Not covered

- ~~Only the brand token was re-measured.~~ The semantic status colours were measured in session 2 and
  every one of them failed in one theme — see "Semantic status colours, measured" below. The neutral text
  ramp is still **not** audited.
- High-contrast mode was checked for the brand token only (`#FFFF00` on black), not end-to-end.

---

# Semantic status colours, measured (session 2, `v4.99.26`)

Closes the first "Not covered" item above. Success / warning / danger were never measured in either theme,
and every one of them failed in one of them.

## What was wrong

They were declared **outside** the theme dictionaries in `Tokens.xaml` — a single value serving light and
dark. `UmBrandTealColor` had already been split per theme for exactly this reason, and
`BrandContrastTests.BrandIsDefinedSeparatelyForLightAndDark` exists to keep it split. The fix was never
applied to the colours that actually carry the status.

Measured as **text** (they are drawn as text — the on-time percentage on the Analytics leaderboard picks
one of the three by value, and the KPI delta badges use two of them), so the bar is 4.5:1:

| Colour | Light card `#FFFFFF` | Dark card `#2D2D30` | Dark chrome `#1E1E1E` |
|---|---|---|---|
| Success `#22C55E` | **2.28 FAIL** | 6.03 | 7.32 |
| Warning `#F59E0B` | **2.15 FAIL** | 6.39 | 7.76 |
| Danger `#DC2626` | 4.83 | **2.84 FAIL** | 3.45 (fails text) |

Success and warning at 2.15–2.28:1 in light theme are worse than the brand regression that prompted the
original audit (2.82:1). Danger in dark theme lands at almost exactly that same value.

## Why per-theme was mandatory, not preferred

Retuning a single shared value could not have worked. To clear AA text contrast a colour needs relative
luminance **≤ 0.183** against a white card and **≥ 0.294** against the dark card. Those ranges do not
overlap, so whatever value was chosen, one theme was always going to fail. This is asserted as a test
(`NoSingleColourCouldHaveSatisfiedBothThemes`) so the reasoning survives.

## The palette now

| | Light | ratio | Dark | ratio (card / chrome) |
|---|---|---|---|---|
| Success | `#15803D` | 5.02 | `#22C55E` | 6.03 / 7.32 |
| Warning | `#B45309` | 5.02 | `#F59E0B` | 6.39 / 7.76 |
| Danger | `#DC2626` | 4.83 | `#F87171` | 4.96 / 6.03 |

Both themes declare all three explicitly, even where the value did not change, so neither can silently
inherit the other's palette again.

## The knock-on that nearly shipped a worse bug

Moving the colours into theme dictionaries broke the way code-built controls fetched them.
`UmSemanticBrushes.Get` used `Application.Current.Resources.TryGetValue`, and `ThemeBrushResolver`'s own
comment already documents why that is unsafe — *"that resolves the app-default theme, not the element's
actual theme"* — while explicitly excusing the semantic brushes on the grounds that **theme-invariance
made it safe**. Making them per-theme removed that excuse. The fallback on a failed lookup was a silent
`Colors.Gray`, so the failure mode was every status colour quietly turning grey.

Resolved by giving `UmSemanticBrushes` an explicit light/dark table and taking an optional element, the
same shape `ThemeBrushResolver` already uses for the neutral text ramp. The palette now exists in two
places — `Tokens.xaml` for XAML consumers, `UmSemanticBrushes` for code — and a test asserts they match
exactly, so the duplication cannot drift. The silent grey fallback now logs a warning.

**Verified live:** published and launched; no brush-resolution warnings in `app.log`, so every semantic
brush resolved.

## WCAG 1.4.1 — colour is never the only signal. CLEAN

The measurement forces this question rather than merely inviting it: **success and danger differ by
1.04:1 in light and 1.21:1 in dark.** In greyscale they are the same colour, and red/green is the
commonest colour-vision deficiency. That cannot be tuned away either — both must clear 4.5:1 against the
same background, which forces them into the same narrow luminance band. So the non-colour cue is doing
real work, not decorative work.

Checked, and each carries words or a glyph independent of colour:

| Surface | Non-colour cue |
|---|---|
| Sidebar status dot (8px `Ellipse`, fill only) | Row subtitle and accessible name state it — "Signed out — tap to reconnect", "No internet — reconnecting…", "Connection error" |
| On-time percentage (Analytics leaderboard) | The number itself, plus a tooltip naming the account and measured count |
| KPI delta badges | A chevron/▲▼ glyph for direction, plus `AutomationProperties.SetName` = "N percent up/down vs …" |
| Awaiting pill | Text — "N awaiting" vs "caught up" |
| Hero rail (green / caution / red) | Three distinct headlines, pinned by test |

`StatusCueTests` (10, green) pins the parts that are reachable without a UI thread, including the general
rule: **any two connection states painted different colours must also read differently.**

**One judgement recorded rather than changed.** The delta badge's *direction* is carried by the glyph, but
its *sentiment* — whether that direction is good or bad — is carried by colour alone. The underlying
information is fully available (direction + which metric), so 1.4.1 is satisfied; the colour is a
redundant affective cue on top. Noted so the next reader knows it was examined, not missed.

## Still not covered

- **The `SystemFillColor*` brushes were not measured.** They are used more often than the app's own
  tokens (36 / 33 / 29 references vs 12 / 12 / 11) and come from WinUI, which supplies per-theme values —
  so they are *probably* fine, but that is inference, not measurement.
- **High-contrast mode** is still only checked for the brand token. `HighContrast.xaml` re-declares the
  status brushes and a test asserts it still does, but the values were not measured.
- **No colour was verified on screen.** UI Automation exposes no fill colours, so "the palette renders as
  intended" rests on the resource wiring resolving without warnings, not on a pixel.
- The **neutral text ramp** remains unaudited, as before.
