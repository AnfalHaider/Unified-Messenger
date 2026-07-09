# UI/UX modernization plan — customer-facing surfaces

**As of:** 2026-07-07 · **Baseline:** v4.56.0 · **Scope:** every screen the business owner actually sees
**Method:** full read of the live XAML/design-system + imperative builders, cross-checked against 2025–2026
dashboard/Fluent best-practice research (sources at the bottom).

> **Bottom line.** The app has a genuine, well-built Fluent foundation — a token system, semantic status
> colors, Mica, the new WinUI `TitleBar`, skeleton loaders, WCAG glyph cues, and broad `AutomationProperties`
> coverage. It is **not** dated in its plumbing. Where it lags modern practice is **visual hierarchy and
> restraint**: everything is roughly the same visual weight, the type scale is small and flat, there is no
> depth/elevation, banners compete for attention, and two parallel color systems are used inconsistently. The
> fastest wins are in *hierarchy and consistency*, not a rewrite. Nothing below requires abandoning the current
> architecture.

---

## 1. What I evaluated

| Surface | File(s) |
|---|---|
| Design system | `Themes/Tokens.xaml`, `Themes/Typography.xaml`, `Themes/Controls.xaml`, `App.xaml` |
| Shell | `MainWindow.xaml` (TitleBar, Mica, 320px sidebar, right notification dock) |
| Sidebar | `Controls/WorkspaceSidebar.xaml` + imperative menu builder |
| Dashboard host | `Pages/DashboardPage.xaml` (single vertical scroll) |
| Command center | `Controls/CommandCenterPanel.xaml` + `.xaml.cs` (KPI band, banners, account accordions) |
| Activity patterns | `Controls/ActivityPatternsPanel.*` |
| Reviews | `Controls/ReviewHealthPanel.*` |
| Personal overview | `Controls/PersonalOverviewPanel.*` (flyout) |
| Settings | `Pages/SettingsPage.xaml` + partials |
| Dialogs | `Dialogs/*`, `AccountDetailDialog`, `WeeklyReportDialog` |
| Notifications | `Controls/NotificationFeedPanel.xaml` |
| Shared primitives | `Controls/Shared/SurfaceCard`, `MetricCardView`, `SectionHeaderView`, `EmptyStateView`, `LoadingOverlayView` |

---

## 2. Current-state assessment

### 2.1 Strengths (keep — do not churn)
- **Modern shell.** New `TitleBar` control, `MicaBackdrop`, pane-toggle, scope selector and AI toggle in the
  right header. This is current WinUI 3 practice.
- **A real token layer** (`Tokens.xaml`): spacing 4/8/12/16/24, radii 6/8, semantic status colors
  (`UmStatusSuccess/Warning/Danger/Neutral/Muted`), opacity tokens. Most apps this size have none.
- **Accessibility baseline is strong**: status is never color-alone (glyph cues), `AutomationProperties`
  set widely, tab-order helper, contrast remediation already done (`#1B75BB` ≈ 4.86:1 on white).
- **Perceived-performance touches**: skeleton loaders, `EntranceThemeTransition` + `RepositionThemeTransition`
  on the card host, "Updated Xm ago" freshness stamps.

### 2.2 Issues, by theme

**A. Visual hierarchy is too flat (the biggest issue).**
- The KPI band is a row of near-equal `MetricCardView` tiles with a single accent tile. There is **no single
  hero number** answering the one question the owner opens the app for — *"Am I caught up, yes/no?"*. Research
  is blunt here: a user should grasp the dashboard's primary message in **~5 seconds**, and critical metrics
  belong **top-left, larger, higher-contrast**. Right now the eye has nowhere to land first.
- The type scale is small and compressed: body 12, caption 10, section label 11, metric value 20. The jump
  12 → 20 with almost nothing between makes everything read as "small dense text," which reads as *dated*
  regardless of how clean the layout is. Modern dashboards use fewer, larger, more separated steps.

**B. No depth.** Everything is a flat 1px-border card. *Depth* is one of Fluent's five pillars, yet there is
zero elevation anywhere — the hero/attention surfaces don't lift off the page. Flatness + small type is what
makes an otherwise-clean UI feel like an internal tool rather than a 2026 product.

**C. Two color systems, used inconsistently.** Custom `UmStatus*`/`UmBrand*` tokens **and** raw Fluent
`SystemFillColorAttention/Caution/Critical*` are both in use (banners use the Fluent set; cards use the custom
set). They don't match each other, so "attention blue" in a banner ≠ the brand blue in a button. Pick one
semantic palette and route everything through it.

**D. Custom colors are not theme-aware — a real dark-mode bug.** `Tokens.xaml` declares flat `<Color>` keys
with **no `ThemeDictionaries`**. So `DashboardSectionHeaderStyle` paints section headers in
`UmBrandSlate #1E293B` (near-black) in **both** themes — nearly invisible on dark Mica. `EmptyStateView` and
several accents share the same problem. The Fluent `ThemeResource` brushes adapt; the custom ones silently
don't.

**E. Card/radius inconsistency.** Three near-identical card styles differ only by padding (12/12/16), and the
imperative builders use corner radii of 2, 4, 6, 8, 10, and 15 with no rule. Radius should encode meaning
(container vs chip vs pill), not vary ad hoc.

**F. Banner overload.** The command center can now stack **four** banners above the cards
(digest → attention → location-CTA → weekly-report reminder). Together they push the actual content below the
fold and dilute urgency — the opposite of "reserve bright/prominent treatment for what's truly urgent."

**G. Wide-screen space is wasted.** The dashboard is one long vertical `StackPanel` (greeting → command center
→ activity → reviews). On a 1440px+ monitor the content column is narrow and the scroll is long. This is
exactly the problem the **bento-grid** trend solves: modular tiles of *varying size by priority* that use
horizontal space and let the owner see status, activity, and reviews without scrolling.

**H. Empty/loading/error states are uneven.** A good `EmptyStateView` primitive exists but isn't used
everywhere (you flagged this yourself); some panels show a bare "Still syncing…" `TextBlock` instead.

**I. Micro-interaction coverage is thin.** `MetricCardView` has pointer states; most other interactive
surfaces (account accordions, chips, review rows) don't visibly respond to hover/press, and there's no use of
`ConnectedAnimation` for the account → detail-dialog → WebView drill path.

---

## 3. Gap analysis vs. 2025–2026 best practice

| Principle (from research) | App today | Gap |
|---|---|---|
| Primary message in ~5 s; hero metric top-left | Equal-weight KPI row | **High** |
| Clear modular type scale, larger KPIs | 10–12 px dense, flat jump to 20 | **High** |
| Depth/elevation for priority (Fluent pillar) | Entirely flat | **Med-High** |
| One semantic color system, never hue-alone | Two systems; glyph cues ✅ | **Med** |
| Theme-aware tokens (dark-mode AA) | Custom colors not theme-split | **Med (bug)** |
| Progressive disclosure | Accordions + detail dialog ✅ | **Low** (good) |
| Bento/priority-sized tiles, use wide screens | Single vertical scroll | **Med** |
| Reserve prominence for the urgent | 4 stacking banners | **Med** |
| Consistent empty/loading states | Uneven | **Med** |
| Micro-interactions/tooltips | Partial | **Low-Med** |

---

## 4. Step-by-step improvement plan

Phased so each release is coherent, low-risk, and independently shippable. Effort is rough dev-hours; risk is
regression risk. **Phase A is the foundation — do it first**; later phases consume its tokens.

### Phase A — Design-system hardening (foundation) · ~½ day · low risk
The prerequisite for everything visual. No layout changes yet — just make the system correct and expressive.

1. **Add `ThemeDictionaries` to `Tokens.xaml`.** Split every `UmBrand*` and `UmStatus*` color into
   `Default` (dark) + `Light` variants so custom colors adapt like the Fluent ones. *Fixes issue D
   (dark-mode section headers/accents).* Verify each status color hits **4.5:1** on both surfaces.
2. **Introduce a modular type scale** in `Typography.xaml`: e.g. `Caption 11 / Body 13 / Subtitle 15 /
   MetricSm 18 / Metric 24 / Hero 34`, each with a defined weight. Replace the ad-hoc 10/11/12/20 sizes.
   *Fixes the "small dense text" read.*
3. **Add an elevation token set** (2 levels: resting card, raised hero) using `ThemeShadow` or a soft
   border+background-layer combo. One reusable `UmRaisedSurfaceStyle`. *Fixes issue B.*
4. **Collapse to one semantic palette.** Map banners/cards/chips to `UmStatus*` (or to the Fluent set) —
   pick one, delete the other's usages. Add a `radius rule`: pill = height/2, chip = 6, container = 8/10,
   nothing else. *Fixes issues C and E.*

*Deliverable:* no visible redesign yet, but dark mode is correct and the toolbox for Phases B–D exists.

### Phase B — Command-center hierarchy (the headline win) · ~1 day · med risk
Make the 5-second scan work.

5. **Add a hero status block** at the top of the command center: one large tile (or a wide banner-height
   strip) that states the single answer — e.g. **"You're caught up"** / **"3 customers waiting"** — in Hero
   type with the semantic color + glyph, plus the one supporting number (oldest wait). Everything else in the
   KPI band demotes to secondary size.
6. **Rebalance the KPI band into a bento row**: hero tile spans 2 columns; caught-up %, awaiting, reply-time,
   messages/day become smaller equal tiles beside/below it. Tile size = priority.
7. **Consolidate the four banners into one priority slot.** Show only the single highest-priority banner
   (order: attention > weekly-report > digest > location-CTA); the rest collapse into a small "N more"
   affordance or move into the notification panel. *Fixes issue F.*

### Phase C — Bento dashboard layout · ~1 day · med risk
8. **Two-column responsive dashboard on wide screens.** Keep the command center full-width at the top, then
   place Activity patterns and Reviews **side by side** (bento) above ~1200px, stacking below it. Uses
   `VisualStateManager` adaptive triggers — no new data, just layout. *Fixes issue G / long-scroll.*

### Phase D — Cards, density & motion polish · ~1 day · low-med risk
9. **Unify card chrome** to the new elevation + radius rules (account accordions, KPI tiles, activity/review
   cards all share resting elevation; hero uses raised).
10. **Add hover/press affordances** to account accordions, chips, and review rows (subtle
    `SubtleFillColorSecondaryBrush` on pointer-over), matching `MetricCardView`.
11. **`ConnectedAnimation`** for account card → `AccountDetailDialog` (and the "open WebView" hand-off) so the
    drill-down feels continuous.
12. **Compact/comfortable density toggle** already exists for cards — extend it consistently to the KPI band
    and activity panel spacing.

### Phase E — Empty / loading / error sweep · ~½ day · low risk
13. **Route every panel through `EmptyStateView`** (command center syncing, activity no-data, reviews
    not-connected, needs-reply all-clear, notifications empty). Kill the bare "Still syncing…" `TextBlock`s.
14. **Standardize loading** on skeletons (not spinners) for content areas; keep the spinner only for the
    WebView host. Add a friendly first-run/zero-accounts state.

### Phase F — Settings & dialogs coherence · ~½ day · low risk
15. **Settings**: the sectioned `SurfaceCard` layout is solid; apply the new type scale + section-header token
    so it matches the dashboard, and verify every toggle has a one-line helper (most do).
16. **Dialogs**: apply the raised-surface + type scale to `WeeklyReportDialog` / `AccountDetailDialog`
    headers so they read as first-class surfaces, not default `ContentDialog`s.

### Phase G — Sidebar density at scale · ~½ day · low risk
17. **Auto-density for large account counts** (the deferred backlog item): when an owner has many accounts,
    tighten row height + hide secondary metadata behind hover, **without** overriding the user's explicit
    pin/compact choice. Add a lightweight in-rail filter/search at high counts.

### Phase H — Accessibility & dark-mode audit (verification gate) · ~½ day · low risk
18. After A–G, re-run a contrast pass in **both** themes (the custom tokens are the risk), confirm focus
    visuals on every new interactive element, and check the bento layout keeps a sane tab order.

---

## 5. Suggested release mapping

| Release | Phases | Theme | Status |
|---|---|---|---|
| **v4.57.0** | A + B | "Design-system + command-center hierarchy" — the visible modernization headline | ✅ shipped |
| **v4.58.0** | C + D | "Bento layout + motion polish" | ✅ shipped |
| **v4.58.1–.3** | (fix) | Light-mode neutral-text theming — `ThemeBrushResolver` + window-root theme fallback | ✅ shipped |
| **v4.59.0** | (feature) | New-vs-returning customer insight (`ContactHistoryStore`) | ✅ shipped |
| **v4.59.1** | E | Empty-state sweep (command center + Activity → `EmptyStateView`) | ✅ shipped |
| **v4.6x** | F + G | Settings/dialog coherence · sidebar density | ☐ remaining (marginal) |
| **(gate)** | H | Accessibility/dark-mode verification before each of the above ships | ◑ ongoing |

**Shipped so far:** A — `Tokens.xaml` `ThemeDictionaries` (dark-mode slate fix) + type-scale lift.
B — command-center hero + one-banner consolidation. C — responsive bento row (Activity + Reviews side by
side ≥1360px, animated reflow). D — bento entrance/reposition motion + card-radius consistency (ConnectedAnimation
was deliberately skipped — the account cards are in-place accordions, not navigations, so it doesn't fit).

Phases A → H are ordered by dependency and by visible impact per hour. If you want a single highest-ROI slice:
**A + B together** deliver ~70% of the "feels modern" jump for ~1.5 days of work and near-zero data risk.

---

## 6. Explicitly out of scope / do not do
- **No framework change, no rewrite.** WinUI 3 + the current token system are the right base.
- **Don't touch the scraping/data layer** — this is purely presentation.
- **Don't over-animate.** Motion should be functional (entrance, connected, hover); avoid decorative loops
  that fight an at-a-glance monitoring tool.
- **Keep the WCAG glyph cues** — they're ahead of most apps; extend, don't remove.

---

## Sources
- [Dashboard Design Principles: The Definitive Guide (UXPin)](https://www.uxpin.com/studio/blog/dashboard-design-principles/)
- [BI Dashboard Design: 2025 UX Best Practices (UK Data Services)](https://ukdataservices.co.uk/blog/articles/business-intelligence-dashboard-design)
- [20 Dashboard UI/UX Principles for 2025 (Medium)](https://medium.com/@allclonescript/20-best-dashboard-ui-ux-design-principles-you-need-in-2025-30b661f2f795)
- [Web Design Trends 2026: Minimalism → Bento Grids (Medium)](https://medium.com/@aksamark/web-design-trends-2026-why-minimalism-is-evolving-into-bento-grids-16839fd31fb7)
- [Bento Grid Dashboard Design: Complete Guide 2026 (Orbix)](https://www.orbix.studio/blogs/bento-grid-dashboard-design-aesthetics)
- [Color palettes & accessibility for data visualization (Carbon/Medium)](https://medium.com/carbondesign/color-palettes-and-accessibility-features-for-data-visualization-7869f4874fca)
- [Color Contrast for Accessibility: WCAG Guide 2026 (WebAbility)](https://www.webability.io/blog/color-contrast-for-accessibility)
- [Streamlining UX with WinUI 3 + Fluent (UxD Critical Software)](https://medium.com/uxd-critical-software/streamlining-ux-design-with-windows-ui-3-and-microsoft-fluent-system-d91bdc05225e)
- [Windows controls and patterns (Microsoft Learn)](https://learn.microsoft.com/en-us/windows/apps/design/controls/)
