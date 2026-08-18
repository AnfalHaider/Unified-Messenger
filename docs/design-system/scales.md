# Type, icon and spacing scales

**Decided:** 2026-08-18 · **Baseline:** v4.99.34 · Guarded by `DesignScaleTests`.

This is the mapping every surface must draw from. It was decided from measured usage, not from taste —
the counts below are from the tree at this baseline, excluding `obj/` and `bin/`.

---

## A correction to the numbers this work started from

`docs/remaining-work.md` §0.1 originally reported **"16 distinct font sizes"**. That figure was wrong in
two ways, and both mattered:

1. **It counted build output.** The measuring grep walked `obj/`, which holds three more copies of every
   XAML file (x64, ARM64, and the default publish). Use counts were inflated roughly fourfold.
2. **It merged two different scales.** `FontSize` on a `FontIcon` sets a **glyph size**, not a type size.
   Icon sizing and text sizing are separate systems that happen to share an attribute name. Collapsing
   them into one ramp would have forced icons and text onto the same steps, which is not a consistency
   improvement — it is a different inconsistency.

The corrected measurement:

| | XAML | C# builders | Union |
|---|---|---|---|
| **Text sizes** | 7 distinct, 93 uses | **11 distinct, 87 uses** | **12** |
| **Icon glyph sizes** | 8 distinct, 34 uses | 10 distinct, 28 uses | **12** |

The conclusion the roadmap drew still holds, and is if anything sharper: **the C# builders carry more
distinct text sizes (11) than the entire XAML surface (7), in fewer uses.** They are the drift source,
they are invisible to the existing guards, and that is why U7 has to land with U1 and U2 rather than after.

Spacing needed no correction — distinct values are unaffected by duplicate files:

| | Distinct | Token | Literal | Total uses |
|---|---|---|---|---|
| `Padding` | 33 | 4 | **29** | 62 |
| `Margin` | 19 | 2 | **17** | 24 |

**29 distinct literal paddings across 62 uses.** Close to every second padding in the app is a value that
appears nowhere else.

---

## The type ramp — 7 steps

Seven steps, each a clear jump from the last, named for what it is *for* rather than how big it is.

| Token | Size | Role | Absorbs |
|---|---|---|---|
| `UmFontSizeCaption` | **11** | Timestamps, badges, axis labels, section eyebrows | 9, 10 |
| `UmFontSizeBody` | **12** | Default body text — the dominant size, 97 uses | — |
| `UmFontSizeBodyStrong` | **14** | Emphasised body, row titles, panel subtitles | 13, 15 |
| `UmFontSizeSubtitle` | **16** | Section headings inside a page | — |
| `UmFontSizeTitle` | **20** | Page and card titles | 18 |
| `UmFontSizeMetric` | **24** | KPI values | 26, 28 |
| `UmFontSizeHero` | **32** | The one number that answers "am I caught up?" | 34, 40, 42 |

**9 and 10 round up, never down.** Both are below comfortable reading size on a dashboard someone scans
at arm's length; merging them upward is the accessible direction and costs nothing.

Retired tokens and their replacements — `UmFontSizeSectionLabel` (11) → `Caption`, `UmBadgeFontSize` (10)
→ `Caption`, `UmFontSizeScope` (14) → `BodyStrong`, `UmFontSizeMetricSm` (18) and
`UmFontSizeMetricValue` (20) → `Title`.

## The icon scale — 4 steps

Separate from the type ramp, for the reason given above.

| Token | Size | Role | Absorbs |
|---|---|---|---|
| `UmIconSizeSm` | **12** | Inline within a chip or badge | 11, 13 |
| `UmIconSizeMd` | **16** | The default — row icons, buttons, status glyphs | 14, 15, 18 |
| `UmIconSizeLg` | **24** | Section and feature icons | 26, 28, 30 |
| `UmIconSizeXl` | **40** | Empty-state hero icons | — |

`14 → 16` is the one change worth watching: 14 is currently the commonest icon size (16 uses) and sits
beside 12px body text. If 16 reads heavy next to body copy in the live check, the fix is to move the
*text* it accompanies up a step, not to reintroduce 14 — two inline icon sizes two pixels apart is the
incoherence this scale exists to remove.

## The spacing scale — a 4px grid

| Token | Value |
|---|---|
| `UmSpacingXs` / `UmPaddingXs` | **4** |
| `UmSpacingSm` / `UmPaddingSm` | **8** |
| `UmSpacingMd` / `UmPaddingMd` | **12** |
| `UmSpacingLg` / `UmPaddingLg` | **16** |
| `UmSpacingXl` / `UmPaddingXl` | **24** |
| `UmSpacingXxl` / `UmPaddingXxl` | **32** |

Every literal rounds to the nearest grid step, rounding **up** where a control needs breathing room and
**down** only where rounding up would clip. Asymmetric paddings survive only where the asymmetry is
deliberate and load-bearing (a badge is wider than it is tall; a card's bottom edge carries a footer),
and each keeps a purpose-named token rather than becoming a literal again.

Corner radius is already on a 6/8/12 scale and guarded — see `DesignScaleTests`.

---

## The rule that keeps this true

`DesignScaleTests` reads **both `.xaml` and `.cs`**. A new off-scale font size, icon size or padding fails
the build. The previous guards read XAML only, which is precisely how the C# builders accumulated more
distinct text sizes than the whole XAML surface without anyone noticing.
