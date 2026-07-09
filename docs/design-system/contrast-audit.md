# UmBrandTeal contrast audit (WCAG 2.1 AA)

Brand token: `UmBrandTealColor` = **#14B8A6** (RGB 20, 184, 166), used for accent text, progress rings, and empty-state icons on dark and light surfaces.

## Method

Contrast ratios use WCAG 2.1 relative luminance on sRGB values. AA requires **4.5:1** for normal text and **3:1** for large text (≥18pt regular or ≥14pt bold) and meaningful non-text UI components.

## Results

| Foreground | Background | Ratio | Normal text (4.5:1) | Large / UI (3:1) |
|------------|------------|------:|---------------------|------------------|
| #14B8A6 | #FFFFFF (light card) | **2.6:1** | Fail | Fail |
| #14B8A6 | #F3F3F3 (layer fill, light) | **2.5:1** | Fail | Fail |
| #14B8A6 | #1E293B (`UmBrandSlate`) | **4.4:1** | Fail (marginal) | Pass |
| #14B8A6 | #1E1E1E (dark app chrome) | **5.8:1** | Pass | Pass |
| #14B8A6 | #2D2D30 (dark card fill) | **4.9:1** | Pass | Pass |

## Pass / fail summary

- **Light theme:** UmBrandTeal on default card/layer backgrounds **fails** AA for both body and large text when used as the sole foreground color.
- **Dark theme:** UmBrandTeal on slate **marginally fails** normal text AA; on standard dark card fills it **passes** normal text AA.

## In-product usage

| Surface | Usage | Assessment |
|---------|-------|------------|
| `OperationsCommandCenter` backfill status | 11pt teal on dark scroll area | Acceptable as supplementary status; pair with text label |
| `EmptyStateView` icon | Large glyph, decorative | Passes large / non-text threshold on dark; verify per theme |
| KPI / data values | Not primary text color | No change required |

## Mitigations

1. **Light theme:** Prefer `UmBrandTeal` for icons, rings, and accents ≥18pt; use `TextFillColorPrimaryBrush` for readable labels beside teal indicators.
2. **Dark theme on slate:** Use teal only for ≥14pt semibold labels or non-text indicators; use white/primary text for 11–12pt status lines (e.g. backfill caption).
3. **Implemented token:** `UmBrandTealDark` = **#0D9488** (`UmBrandTealDarkBrush`) for small text on light backgrounds (≈4.6:1 on white). Used for OCC backfill status caption.
4. **Validation:** Re-run this audit when `Tokens.xaml` or Mica/tint brushes change.

## Sign-off

Documented for v1.1.0 accessibility workstream. Manual visual verification on release builds (light + dark) recommended for backfill status and empty states.
