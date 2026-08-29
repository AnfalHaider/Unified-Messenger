# Phase B — UI/UX audit

**Run:** 2026-08-29 · **Against:** `feat/audit-2026-08` @ v4.99.69 · **Suite:** 1899 pass

Labels: **CONFIRMED** (observed — measured or seen on screen) · **LIKELY** (code says so) · **UNKNOWN**.

---

## B0 · Coverage — read this first

This phase ran **after** twelve increments that had already fixed most of what a first pass would have
found, so it is a re-audit of a moving target rather than a survey of the shipped release. What it covers
and what it does not:

| Covered | How |
|---|---|
| Dashboard, Analytics, Reviews, Reports, Settings, Notification Hub | Rendered and screenshotted, **both themes** |
| Dark-theme elevation | Measured in HSL against Material's published model |
| Colour discipline | Counted and contrast-measured |
| Typography, weight, corner radius, spacing | Counted across XAML **and** C# |
| Alert calibration | Compared against monitoring-design practice |

| **NOT covered** | Why |
|---|---|
| **Screen reader** | Never run. Unchanged and still the single largest gap in the product. |
| **150% / 200% display scaling** | Changing the owner's display scale on their working machine was not something to do unasked. |
| **Narrow window / responsive reflow** | Only observed incidentally when the notification panel narrowed the content area — which is where the AI briefing clipping was caught. |
| **High contrast** | `UISettings.ColorValuesChanged` carries the re-evaluation and **has still never been seen to fire on an actual toggle**. Unchanged from §0.4. |
| **Urdu / Arabic RTL** | Real customer names in the live data are Latin-script; no RTL string was rendered. For a Pakistani business this remains a genuine hole. |
| **15+ accounts, very long names** | Six accounts on this machine; no synthetic load was injected because the store is the owner's live data. |
| **WebView2 content** | Masked by the screenshot filter — see B6. |

---

## B1 · Dark elevation — now correct, and it was measurably wrong before · **CONFIRMED**

The owner's report was *"dark theme has no proper visibility"*, and the answer to "reading the text, or
telling the panels apart?" was **telling the panels apart**. That was diagnosed and fixed in v4.99.61; this
phase re-measured it against published guidance rather than intuition.

Material's dark-theme model: shadows do not work on dark grounds (a dark shadow on a dark surface has almost
no contrast), so **elevation is carried by lightness** — each layer adds roughly **4–8 percentage points**
over a near-black base.

Measured in HSL against the app's own tokens:

| Surface | Hex | Lightness |
|---|---|---|
| canvas | `#0E0F12` | 6.3% |
| sunken | `#121418` | 8.2% |
| surface **before** v4.99.61 | `#17191D` | 10.2% |
| surface **after** | `#20242B` | **14.7%** |

| canvas → surface step | |
|---|---|
| Before | **3.9 pts — below Material's minimum** |
| After | **8.4 pts — inside the 4–8 band** |

So the pre-fix elevation was *below the threshold at which a layer reads as raised at all*. That is an
independent confirmation of a fix that was made on contrast reasoning, and it explains the owner's wording
precisely: not "I can't read this" but "I can't tell these apart".

**Light theme needs less and gets it free.** A white card on a grey canvas is a familiar figure-ground cue
and the eye discriminates lightness far better in bright ranges, so light survived the same numbers.

**Remaining:** the app still has no drop-shadow or `ThemeShadow` anywhere. That is *correct* for dark and a
missed affordance for light, where a shadow would do work no colour change can. Not scheduled — it is a
design choice, not a defect.

---

## B2 · The corner-radius scale is enforced in XAML only · **CONFIRMED** · S3

`DesignScaleTests.EveryCornerRadiusComesFromTheScale` allows `{6, 8, 12}` and scans **`.xaml` only**. Every
XAML radius conforms. In C#, `new CornerRadius(N)` is unchecked, and it has drifted:

| Radius | Uses | On scale? |
|---|---|---|
| 6 | 9 | ✅ |
| 8 | 10 | ✅ |
| 12 | 2 | ✅ |
| **2** | 5 | ⚠️ `UmCornerRadiusXsValue` **is** a defined token — the test's allowed list just omits it |
| **0, 4, 5, 10, 14, 15, 999** | 9 | ❌ off scale |

Eight distinct radii in code against three allowed. The class docstring says radii *"had reached six distinct
values across the app"* before a cleanup — the cleanup and its test both covered XAML, so the code side kept
drifting and is now worse than the number that triggered the original work.

**This is the same gap, in the same test file, that `NoLiteralFontSizeInCode` was written to close for font
sizes** — its comment says the XAML-only scan is *"the reason the C# builders accumulated ELEVEN distinct
text sizes against the whole XAML surface's seven"*. The lesson was applied to one property and not the
other.

**Correction:** extend the scan to `.cs`, add `2` to the allowed set (it is already a token), give `999` a
named "pill" token since fully-rounded is a real idiom, and migrate the remaining seven.

---

## B3 · Typography and weight are disciplined — no finding

Stated because the brief asks for it plainly rather than inventing problems. Measured across XAML and C#:

- **Font sizes** resolve to one ramp. The 22 distinct *references* are the same tokens spelled two ways —
  `UmFontSizeBody` in XAML, `UmScale.Text.Body` in C# — which is the documented duplication that
  `TheCodeScaleMatchesTheXamlTokens` exists to keep honest. Semantic steps in use: Caption, Body,
  BodyStrong, Title, Hero, plus three icon sizes.
- **Font weights**: `SemiBold` and the inherited default. That is it.

Both already match what a dense operational dashboard should do.

---

## B4 · Colour still carries two palettes · **CONFIRMED** · S4

69 `SystemFillColor*` references remain beside the audited `UmStatus*` tokens — two greens, two ambers, two
reds. `StatusContrastTests.TheSystemPaletteDoesNotSpreadFurther` pins it at exactly 69, so it cannot grow;
shrinking it is deliberate work with its own contrast pass. Unchanged from §0.1a, and correctly characterised
there as a *consistency* defect rather than a contrast one.

What did change this session: every `UmStatus*` colour is now measured against **all three** real surfaces of
its own theme, which is what caught the two that were failing.

---

## B5 · Alert calibration — the dashboard trains the owner to ignore it · **CONFIRMED**

Monitoring-design practice is unambiguous here: *"Set thresholds too sensitive and every minor fluctuation
triggers amber or red. The team learns to ignore alerts because most of them are noise."*

The dashboard currently reads **`SLA met 0%`**, permanently, because the target is 15 minutes and the median
reply is hours. The threshold is a settled owner decision and is not in question. **The tile is.** A number
that is red every day is not a signal; it is wallpaper, and it devalues the tiles either side of it.

This is [owner-decision §1](../owner-decisions.md) and it is still open: option B — *"median first reply 3h
20m · target 15m"* — says the same true thing, keeps the 15-minute standard, and removes the permanent zero.
**One instruction settles it.**

Related, and observed this session: the dashboard is otherwise well-behaved against that guidance — one hero
answer ("N customers are waiting"), a severity-ranked insight list, and green/amber/red never used as the
*only* carrier (`SuccessAndDangerAreNearlyIdenticalInGreyscale…` pins that).

---

## B6 · What the tooling could not see

**WebView2 content is masked.** The account view — WhatsApp Web itself — renders in separate processes that
the screenshot filter blanks out, so an opened account comes back as an empty dark frame. This is why
increment 97's stranding bug could not be reproduced live and had to be proven by extracted-function tests
instead. Any future visual audit of the embedded channels is blocked the same way unless those processes are
granted.

---

## B7 · Findings summary

| # | Finding | Severity | State |
|---|---|---|---|
| B1 | Dark elevation was 3.9 pts, below Material's 4–8 minimum | S2 | **Fixed** v4.99.61; re-measured here |
| B2 | Corner-radius scale enforced in XAML only; 9 off-scale C# literals | S3 | **Open** — correction specified |
| B3 | Typography and weight | — | No finding |
| B4 | Two status palettes (69 refs, ratcheted) | S4 | Open, deliberate |
| B5 | `SLA met 0%` is permanent-red wallpaper | S3 | **Owner decision**, one instruction |
| B6 | WebView2 content unauditable through this tooling | — | Constraint |
| B8 | Screen reader, scaling, RTL, high contrast, 15+ accounts | — | **Not covered** — see B0 |
