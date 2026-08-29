# Phase D — bug hunt

**Run:** 2026-08-29 · **Against:** `feat/audit-2026-08` @ v4.99.70 · **Suite:** 1899 pass

Labels: **CONFIRMED** (observed) · **LIKELY** (code says so) · **UNKNOWN** (artifact named).

---

## D0 · Method

The brief asks for active hunting rather than waiting for bugs to present themselves, and names the shapes
to look for. This phase took the shapes **already found in this audit** and swept for siblings — which is
the only hunting method that has actually worked here. Every defect found across increments 91–102 fell into
one of five shapes, and four of the five turned out to have more than one instance.

| Shape | First instance | Siblings found by sweeping |
|---|---|---|
| A brush resolving the wrong theme | AI insight strip | **8 files**, each with its own private resolver |
| A control present but invisible | Account-details button | **8** empty `FontIcon` glyphs |
| Text that cannot wrap where it is put | AI shift briefing | **6** horizontal `StackPanel`s |
| A number that means two things | `SLA met` on Analytics | badge-vs-panel, survivorship, live-vs-total |
| A scale enforced on one surface only | Font size (fixed once) | **Corner radius — same gap, still open until D2** |

That last row is the finding of this phase: **the codebase has a recurring failure mode where a rule is
enforced in XAML and not in C#**, and it has now produced the same defect twice in the same test file.

---

## D1 · Bugs found and fixed during this audit

Recorded so the class is visible, not to re-list the changelog. All **CONFIRMED**.

| # | Bug | Severity | Fixed |
|---|---|---|---|
| 1 | A followed link replaced the scraped session with no way back — the allowlist spans whole registrable domains, so `google.com` stranded it too | **S1** | v4.99.66 |
| 2 | Eight `FontIcon`s with an empty `Glyph` — present, laid out, focusable, clickable, invisible. Made the L1 drill-down unreachable for the life of the feature | **S1** | v4.99.65 |
| 3 | Every themed token unsafe through `Application.Current.Resources` — the Reviews page was blank white tiles | **S1** | v4.99.62 |
| 4 | Eight files each rolled their own brush resolver, bypassing the one that exists | **S2** | v4.99.63 |
| 5 | Every `ContentDialog` rendered light in dark | **S2** | v4.99.64 |
| 6 | Contrast tests measured surfaces the app does not ship; two tokens failed AA at full opacity | **S2** | v4.99.60 |
| 7 | "Reply speed is healthy" computed over survivors while 100 waited | **S2** | v4.99.67 |
| 8 | A day with no data drawn identically to a day with a zero | **S2** | v4.99.67 |
| 9 | Badge said 21, panel said "No notifications yet" | **S2** | v4.99.67 |
| 10 | Wrapping text inside horizontal `StackPanel`s — clipped mid-word, six sites | **S3** | v4.99.67 |
| 11 | Captions at 0.55 were 3.84:1 in light | **S3** | v4.99.69 |
| 12 | Two `BackfillDedupeStore` tests: one red for two hours every night, one passing without exercising its subject | **S3** | v4.99.64 |

---

## D2 · The corner-radius scale was enforced in XAML only · **CONFIRMED** · S3 · **fixed v4.99.70**

`DesignScaleTests.EveryCornerRadiusComesFromTheScale` scanned `.xaml` and nothing else. Every XAML radius
conformed. In C#, `new CornerRadius(N)` was unchecked and had drifted to **eleven distinct values**:
`0, 2, 4, 5, 6, 8, 10, 12, 14, 15, 999`.

The class docstring says radii *"had reached **six** distinct values across the app — cards rendered at 8,
10, 12 and 14 — which is not a system, it is an accumulation"*. The cleanup that prompted that sentence, and
the test written to hold it, both covered XAML — so the code side kept accumulating and ended up **worse
than the number that triggered the original work**.

**This is the second time this exact gap has produced a defect in this file.** `NoLiteralFontSizeInCode`
exists because of the first: its comment reads *"`DesignScaleTests` read .xaml only, so every literal in
`CommandCenterPanel.xaml.cs` and friends was invisible to it"* — eleven text sizes against seven. The lesson
was applied to font size and not to radius.

**Fixed:** the scan now covers `.cs`; `4 → 6`, `5 → 6`, `10 → 12` (×3), `14 → 12`, `15 → 12`; and a
`WorkspaceSidebar` fallback of `4` was corrected to `6` to match the token it falls back from.

**The scale itself grew by one tier, deliberately.** `TheScaleStaysSmallEnoughToBeAScale` asserted ≤3 tiers
and failed — correctly, that is what it is for. The resolution was not to raise the number quietly:
`UmCornerRadiusXsValue = 2` has been declared in `Tokens.xaml` the whole time for chips and small markers,
so the fourth tier already existed and the list had simply never seen it. `0` (square) and `999` (pill) are
allowed but **excluded from the tier count**, so the number cannot creep while looking like a system.

---

## D3 · "1 replies" · **CONFIRMED on screen** · S4 · **fixed v4.99.70**

`CommandCenterPanel.xaml.cs:2156` — `$"median · {response.SampleCount} replies"`, with no singular branch.
Seen on the dashboard on 2026-08-29 reading **"median · 1 replies"**, because reply history restarted on
2026-08-28 and there was exactly one sample.

Swept for siblings: 32 interpolated strings pair a count with a plural noun. All but this one either have a
singular branch (`accountsBehind` has an explicit `1 =>` arm) or are in genuinely-plural contexts (a
day-count from a setting, a message total). **One real instance, fixed.**

---

## D4 · Hunted and NOT found — stated so the absence is on the record

| Shape hunted | Result |
|---|---|
| A store read that bypasses `CorruptFileRecovery` | **Zero.** The v4.99.48 hardening holds. |
| A disposable leaked | **Zero.** All six undisposed fields are process-lifetime singletons — correct. |
| An unsafe scheme reachable from a page | **Zero.** Four schemes: `https`, `http`, `mailto`, `tel`. |
| Page-controlled input reaching C# unbounded | **Zero.** Bounded, coalescing queue; parse failures logged by *length*, not content. |
| Hardcoded colour in XAML bypassing the theme dictionaries | **Zero.** Pinned by `NoColourIsHardcodedInXaml`. |

---

## D5 · Open, not fixed

| # | Item | Severity | Why not fixed |
|---|---|---|---|
| **D5-a** | The report calls the **total** waiting count "right now" while the dashboard headlines the **live** queue — 100 vs 41, same phrasing | S3 | Correction specified in [02-code.md §C6](02-code.md); it is one string, but which number should headline the report is a product call, not a mechanical one. |
| **D5-b** | `GoogleReviewSnapshotService` has **8 silent catches** — the one service whose failures are already known to be invisible | S4 | A logging pass, and the next one should be driven by reading `app.log` on a real Re-sync rather than by a static count. |
| **D5-c** | `themePreference` flipped `Dark` → `Light` mid-session (file written 22:43) | **UNKNOWN** | Cannot be attributed — a stray click during dialog hunting is as likely as the app rewriting it. Reproducing means setting Dark, relaunching repeatedly, and watching the file. Recorded rather than claimed. |
| **D5-d** | 44 `async void` handlers with no `try` | S3 | Each is defended one level down; the cost of 44 guards exceeds the risk while that holds. See [02-code.md §C3](02-code.md). |

---

## D6 · What this phase could not hunt

- **Anything behind WebView2.** Its content renders in separate processes the screenshot filter masks, so
  the embedded channels — the actual product surface — were never visually inspected.
- **Anything a screen reader would find.** Still never run.
- **Anything that needs load**: 15+ accounts, very long names, RTL text, account churn, a network drop with
  pages already loaded.
- **Anything only reachable through a dialog nobody opened.** Three of 23 were opened this session.

Four of the twelve bugs in D1 were found by *rendering the app and looking at it*, and two of those only
because the owner sent a screenshot. The list above is where the next ones are.
