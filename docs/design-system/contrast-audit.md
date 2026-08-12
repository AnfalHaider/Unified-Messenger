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

- Only the brand token was re-measured. The semantic status colours (`UmSemanticColors` — success / warning
  / danger) and the neutral text ramp were **not** audited, in either theme.
- High-contrast mode was checked for the brand token only (`#FFFF00` on black), not end-to-end.
