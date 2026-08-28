# Roadmap — v4.99.60 onward

**Written:** 2026-08-28 · **Branch:** `feat/audit-2026-08` · **Baseline:** `main` @ `a146d33`, v4.99.59
**Suite at baseline:** 1865 pass / 0 fail / 25 s · **Last increment shipped:** 90

---

## 0 · What this roadmap is built on, and what it is not

**It is built on Phase A only.** Phase A (verification — [00-remaining-work.md](00-remaining-work.md)) is
complete. Phases B (UI/UX audit), C (code audit) and D (bug hunt) have **not** run. Every item below is
either something Phase A confirmed by measurement, or a pre-existing backlog item Phase A re-checked against
the tree.

That means this roadmap is **precise but not yet complete**. It does not claim to be the full remaining-work
list for the product. §7 names exactly what discovery is still outstanding and schedules it, rather than
leaving the gap implied.

No item below says "investigate". Where something is genuinely unresolved it is labelled **UNKNOWN** with the
artifact that would settle it, and it is not scheduled as if it were actionable.

### Evidence labels carried forward

- **CONFIRMED** — observed this session.
- **LIKELY** — the code says so, not executed.
- **UNKNOWN** — needs an artifact I do not have.

---

## 1 · Ordering rationale

Four rules, applied in this order:

1. **Instrument before fixing.** The repo's own history is emphatic on this: at v4.99.48–59 three of the last
   four findings came from making failures visible, not from reading code. Where a check is broken, the check
   is fixed *before* the thing it checks — otherwise the fix ships against a test that cannot see it.
   **This rule paid for itself immediately:** Increment 91 fixed the measurement first, and the corrected
   measurement falsified the defect this roadmap had been written around while finding a different, real one
   (§3.0). Had the order been reversed, a palette would have been repainted to satisfy a scenario the app
   never renders, and the actual failure would still be shipping.
2. **Then defects the owner can see today**, worst-first. A stranded WebView is a dead end in the primary
   channel. **Note the owner's "dark theme has no proper visibility" is now unexplained** — it is not the
   text-contrast defect the brief attributed it to. See §7.
3. **Then unblock discovery.** An account is signed in, which makes two long-stalled questions answerable
   (D2's call-outcome store, live metric accuracy). Cheap probes that convert UNKNOWN into fact rank above
   speculative polish.
4. **Gated and owner-decision items are never scheduled.** They sit in §5 and §6 with what would unblock them.

**Why not fix the navigation dead-end first**, given it is the more severe bug? Because it is CONFIRMED by
reading and **not yet reproduced live**. The brief's §2 forbids fixing what has not been diagnosed, and this
repo has already paid for that rule three times on one CI failure. It is scheduled at Increment 93 with the
reproduction as the first task *inside* the increment.

---

## 2 · Increment overview

| Inc | Version | Slice | Severity of worst item | Effort | Status |
|---|---|---|---|---|---|
| **91** | v4.99.60 | The contrast check measured surfaces the app does not ship (Phase 3 — instrumentation) | S2 | **M** | ✅ **SHIPPED** — 1865 → 1878 tests |
| **92** | v4.99.61 | 0.55-dimmed captions are below AA in light theme (Phase 3 — accessibility) | S3 | **S** | Open — **much smaller than first scoped, see §4** |
| **93** | v4.99.62 | A followed link stranded the account with no way back (Phase 1 — navigation) | S1 | **S** | Open |
| **94** | v4.99.63 | Docs state things the tree contradicts (Phase 3 — correctness of the record) | S3 | **XS** | Open — docs only |
| **95** | v4.99.64 | The UI smoke job's exit code never reached the workflow (Phase 3 — CI) | S3 | **XS** | Open — CI only. **Probe** |
| **96** | v4.99.65 | A third status palette, and the dead code holding it up (Phase 3 — deletion) | S3 | **S** | Open — **new, found during 91** |

> ### ⚠️ This roadmap was corrected mid-execution. Read §3.0 before §4.
>
> Increments 91 and 92 were planned as a split — instrumentation red, then fix green — so the tests would be
> seen failing before being made to pass. **That worked, and it immediately falsified the premise the
> original Increment 92 was built on.** The dark-theme text defect this roadmap inherited from the session
> brief **does not exist in the form described**. What the instrumentation found instead was a smaller, real,
> and previously invisible defect. Both are recorded in §3.0 rather than quietly edited out, because the
> way it was caught is the reusable part.

---

## 3 · Increment 91 — SHIPPED

`v4.99.60: the contrast check measured surfaces the app does not ship (Phase 3 — instrumentation) (Increment 91)`

**Result: 1865 → 1878 tests, 0 failed.** The new theory was observed RED on exactly two cases before the fix
turned it green — recorded below, because a test that has never been seen red is not evidence.

### 3.0 · What the instrumentation falsified — read this before §4

The session brief asserted, and this roadmap initially accepted, that the app's `Opacity` dimming pushed text
below AA — "the token is compliant, the rendered pixel is not", with 352 sites and `SettingsPage.xaml`'s 51
as the worst offender. **Measured, that is wrong.**

The 51 SettingsPage sites dim the **inherited body foreground**, not a status colour. Composited and measured
this session:

| Dimmed body text | @1.0 | @0.75 | @0.65 | @0.55 |
|---|---|---|---|---|
| Dark, on `#17191D` | 17.60 | 10.28 | **7.95** | 6.09 |
| Light, on `#F1F3F6` | 15.49 | 7.16 | **5.10** | **3.84** ✗ |

At 0.65 — 42 of the 88 sites — dimmed body text is **7.95:1 on dark and 5.10:1 on light**. Comfortably clear
of AA in both themes. *The dominant pattern is not a defect.*

Two further checks closed it off:

- **No XAML element combines a status `Foreground` with an `Opacity`** — so the arithmetic showing status
  colours failing at 0.65, which is correct in itself, describes a pairing the app never renders.
- The only two C# sites pairing a foreground with an opacity dim `TextFillColorSecondaryBrush` (0.7) and
  `SystemFillColorCautionBrush` (0.9). Neither is a `UmStatus*` token, and 0.9 is negligible.

**What the corrected instrumentation did find** — a real defect, invisible to the old tests and to the
brief's framing alike:

> `UmStatusMutedColor` `#6B7684` measures **4.15:1** and `UmStatusDangerColor` `#DC2626` **4.34:1** on the
> **light sunken surface** `#F1F3F6` — below AA **at full opacity, before any dimming**. Both are drawn as
> text: `ReviewDesk.UrgencyBrush` hands Muted to a `TextBlock.Foreground` for an unrated review, and
> `DeltaBadge` paints its arrow and percentage with Neutral by the same route.

It was never caught because the suite measured light contrast against `#FFFFFF` only — the most forgiving
surface the app has — and the sunken surface is ~10% darker, which is exactly enough to cross the bar.

**The lesson is the one this repo keeps re-learning, in a new place:** the defect was not where the loudest
description put it, and the thing that found it was fixing the *measurement* first. Two of the three
sub-tasks below were worth doing; the third, as originally specified, would have repainted a palette to
satisfy a scenario that does not occur.

**Also corrected: 0.55 IS a real failure** — 3.84:1 on light. Several `SettingsPage` captions use it via
`UmOpacitySubtle`. Small, real, and now Increment 92 (§4). The 0.55 `FontIcon` glyphs are non-text and clear
the 3:1 bar.

### 91-A · `WcagContrast` measures against surfaces the app no longer ships

| | |
|---|---|
| **Problem** | The contrast suite measures every status token against `#2D2D30` and `#1E1E1E` — WinUI defaults the app replaced with its own surface tokens. The light *sunken* surface is not measured at all. |
| **Owner impact** | The suite certifies colour accessibility the product does not have. It is the reason a legibility complaint could coexist with a green build. |
| **Where** | `UnifiedMessenger.Tests/WcagContrast.cs:31–33` |
| **Severity / Effort** | **S2** / **S** |
| **Evidence** | **CONFIRMED** — constants read; shipped surfaces read from `Themes/Tokens.xaml:125–127` and `62–64`. |

**Correction.** Replace the three hard-coded constants with values read from `Tokens.xaml`, the way the token
colours already are — the file's own docstring gives the reason ("changing a colour without checking contrast
then fails the build"), and the surfaces were the one thing exempt from it.

```csharp
// delete: LightCard / DarkCard / DarkChrome consts
public static string Surface(string themeKey)       => ThemeColor(themeKey, "UmSurfaceColor");
public static string SurfaceSunken(string themeKey) => ThemeColor(themeKey, "UmSurfaceSunkenColor");
public static string Canvas(string themeKey)        => ThemeColor(themeKey, "UmCanvasColor");
```

Theme keys are `"Light"` and `"Default"` — `ThemeColor` already handles both.

**What could break.** Three existing theories consume these constants and re-point at different backgrounds:
`EachStatusColourIsReadableAsTextOnALightCard`, `…OnADarkCard`, `…OnTheDarkChrome`. Dark assertions get
*easier* (`#17191D` is darker than `#2D2D30`, so light-on-dark contrast rises) and should all still pass —
which also means the old constants were accidentally the stricter test on that axis, and passing them never
implied the sunken surface was checked. **Light assertions extended to the sunken surface will newly fail**
for Muted and Danger — correctly, see 92-B.

### 91-B · Nothing models element opacity

| | |
|---|---|
| **Problem** | `WcagContrast` has no alpha, blend or composite function. The app dims 88 XAML elements and 24 C# ones with `Opacity`, so the token is compliant and the rendered pixel is not. |
| **Owner impact** | The measured defect: every status token falls below AA at 0.65 on dark. This is the owner's "no proper visibility". |
| **Where** | `UnifiedMessenger.Tests/WcagContrast.cs` (absent); dimming at `Pages/SettingsPage.xaml` (51 sites) and 37 others |
| **Severity / Effort** | **S2** / **S** |
| **Evidence** | **CONFIRMED** — grepped `opacity\|alpha\|blend\|composit` across the contrast tests: zero hits. Ratios recomputed independently and reproduce the reported table exactly. |

**Correction.** Add the composite, then assert on the composited value:

```csharp
/// Foreground drawn at <paramref name="alpha"/> over <paramref name="background"/>.
/// WinUI's Opacity composites the element against what is behind it; measuring the
/// undimmed token measures a pixel that is never drawn.
public static string Composite(string foreground, string background, double alpha)
{
    static int Ch(string h, int i) =>
        int.Parse(h.TrimStart('#').Substring(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    var f = foreground; var b = background;
    return "#" + string.Concat(new[] { 0, 2, 4 }.Select(i =>
        ((int)Math.Round(Ch(f, i) * alpha + Ch(b, i) * (1 - alpha))).ToString("X2", CultureInfo.InvariantCulture)));
}

public static double RatioAtOpacity(string fg, string bg, double alpha) =>
    Ratio(Composite(fg, bg, alpha), bg);
```

**As shipped, corrected from the plan.** The planned test asserted status colours stay readable *at 0.65 and
0.75*. That was written and then **withdrawn before commit**: it failed everywhere, and no fix would or
should have made it pass — the palette cannot survive 0.65 dimming, and nothing in the app dims it. It was
over-fitted to a pattern that does not occur, which is the same error as the brief's.

What shipped instead:

- `StatusContrastTests.EveryStatusColourIsReadableOnEverySurfaceOfItsOwnTheme` — a `[Theory]` over
  {6 status colours} × {both themes}, asserting AA against **all three** of that theme's surfaces. **Observed
  RED on exactly two cases** — `(UmStatusMutedColor, Light)` and `(UmStatusDangerColor, Light)` — which are
  the real defect, then green after the token fix.
- `WcagContrast.Composite` / `RatioAtOpacity` — kept, because measuring a *specific* dimmed pairing is still
  the only way to settle one. They are tools, not an assertion about the pattern.
- The measurements that falsified the original premise are written into the ratchet's comment block rather
  than a doc, so the next person to consider dimming text reads them at the point of decision.

### 91-C · A ratchet on raw `Opacity` in XAML

| | |
|---|---|
| **Problem** | Nothing stops the pattern regrowing. The `SystemFillColor` ratchet exists precisely because a palette rule with no ceiling drifts. |
| **Owner impact** | Prevents the defect being re-introduced after it is paid down. |
| **Where** | new test beside `StatusContrastTests.TheSystemPaletteDoesNotSpreadFurther:238` |
| **Severity / Effort** | **S3** / **XS** |
| **Evidence** | **CONFIRMED** — 88 tracked XAML sites counted; the existing ratchet's shape copied. |

**Correction.** Mirror the existing ratchet exactly, including its `bin`/`obj` exclusion — **that exclusion is
load-bearing**, and its absence is why the session brief reported 352 instead of 88:

```csharp
// Measured at 88 (51 of them in SettingsPage.xaml). Lower as sites migrate to text tokens; never raise.
Assert.True(references <= 88, $"raw Opacity dimming rose to {references}. Secondary and tertiary text "
    + "come from UmTextSecondaryBrush / UmTextTertiaryBrush, which are contrast-checked; Opacity is not.");
```

Count `Opacity="0.` occurrences under `UnifiedMessenger/`, excluding `bin`/`obj`.

**What could break.** Nothing at baseline — 88 is the current count, so it lands green while 91-A/91-B land
red.

---

## 4 · Increment 92 — 0.55-dimmed captions are below AA in light theme

`v4.99.61: dimmed captions fell below AA in light theme (Phase 3 — accessibility) (Increment 92)`

**Scope collapsed from M to S.** The original 92-A (introduce text tokens, migrate 88 sites) is **cancelled**
— see §3.0: dimmed body text passes AA at 0.65 in both themes, so there was nothing to fix. The original
92-B (four light status tokens) was **absorbed into Increment 91**, because the corrected test went red and
`main` may not carry a red tree; the two that genuinely failed were fixed there. What remains is one real,
narrow defect.

### 92-A · `UmOpacitySubtle` (0.55) on text is 3.84:1 in light

| | |
|---|---|
| **Problem** | Body text at 0.55 opacity measures **3.84:1** on the light surface — below AA. Several `SettingsPage` captions use it. On dark the same dimming is 6.09:1 and fine. |
| **Owner impact** | Small but real: the affected text is caption-size help copy under settings controls, which is where an owner looks when they are already unsure. It is the one place the brief's "dimming breaks legibility" story is actually true — just at 0.55, not 0.65, and in light, not dark. |
| **Where** | `Themes/Tokens.xaml:204` (`UmOpacitySubtle` = 0.55) · `Pages/SettingsPage.xaml:827, 871, 896` · `Controls/NotificationFeedPanel.xaml:45` |
| **Severity / Effort** | **S3** / **S** |
| **Evidence** | **CONFIRMED** — composited and measured this session. |

**Correction.** Two options, and the choice is genuinely open:

- **A (preferred).** Give these captions a real foreground instead of a dim. This needs the text tokens the
  original 92-A would have added — but for **four sites, not 88**, which is what makes it cheap now. Add
  `UmTextTertiaryColor` per theme (light `#5B6773` → 5.20:1 on sunken; dark `#8A97A6` → 5.92:1 on surface),
  and replace `Opacity="0.55"` with `Foreground="{ThemeResource UmTextTertiaryBrush}"` at the text sites
  only.
- **B.** Raise `UmOpacitySubtle` from 0.55 to 0.65, where light body text measures 5.10:1. One-line change,
  but it keeps contrast dependent on a property no test can see, and it changes every consumer of the token
  including the icon glyphs that were fine.

**A is preferred** and is what the ratchet is pointing towards. It also leaves the codebase with the text
tokens it lacks entirely — see 96-B.

**Leave the 0.55 `FontIcon` glyphs alone** (`CommandPalette.xaml:59`, `PersonalOverviewPanel.xaml:397`,
`ReviewsPage.xaml:28`). They are non-text, so 1.4.11's 3:1 applies and 3.84:1 clears it.

**Test that pins it.** Once `UmTextTertiaryColor` exists it can be read from `Tokens.xaml` and asserted
against all three surfaces per theme, exactly like the status colours now are. Until it exists there is
nothing honest to assert — which is why this test could not have been written in Increment 91, and why the
ratchet was used there instead.

**What could break.** These `TextBlock`s inherit their foreground today; an explicit brush overrides implicit
state styling, so check the disabled state of the settings rows that contain them.

## 5 · Increment 93 — a followed link stranded the account

`v4.99.62: a followed link stranded the account with no way back (Phase 1 — navigation) (Increment 93)`

### 93-A · Reproduce it first

**This is a task, not a formality.** The correction is not chosen until the reproduction runs. Launch the
installed build, open the WhatsApp account, click a `*.whatsapp.com` link from the page, and record what the
frame does and what the owner's escape route is. If it does not reproduce, this increment stops and the
finding is downgraded — the brief's §2 rule, and this repo has paid for it.

### 93-B · The correction

| | |
|---|---|
| **Problem** | `HandleNewWindowRequested` hops the **current frame** for any allow-listed host. Back/forward are hidden for exactly the platforms that scrape. Follow such a link and WhatsApp Web is gone with no way back. |
| **Owner impact** | The primary channel becomes a dead end. Recovery is right-click → Refresh WebView, which an owner will not find. Oversight for that account silently stops until they do. |
| **Where** | `Services/Session/WebViewNavigationGuard.cs:337` (`coreWebView.Navigate(args.Uri)`) · `MainWindow.xaml.cs:71–72` (`NavControlsPanel.Visibility = isEmbed ? Visible : Collapsed`) |
| **Severity / Effort** | **S1** / **S** |
| **Evidence** | **CONFIRMED by reading** both halves this session. **Not yet reproduced live** — 93-A. |

**Blast radius is wider than the brief stated.** `BuildDefaultAllowedHosts` (`:162`) adds each platform's host
*and its whole registrable domain*, plus `CommonOAuthHosts`. From WhatsApp Web the allowlist therefore
includes `whatsapp.com` **and `google.com`** and the OAuth hosts — so a `google.com` link in a customer
message strands the account too. Any correction that only special-cases `whatsapp.com` is incomplete.

**Correction — open it externally instead of hopping the frame.** The handler already has the mechanism
(`TryOpenExternally`, used for non-allow-listed hosts) and the reasoning is already written in its own
comment: *"Anything else the owner deliberately clicked is THEIR link, and belongs in their own browser."*
That reasoning applies identically to an allow-listed marketing page. Narrow the in-frame hop to the case it
exists for — a same-site redirect within the account's own start host — and send everything else out:

```csharp
if (IsAllowedNavigationUri(args.Uri, allowlist) && IsSameSiteAsStartUrl(args.Uri, startHost))
{
    coreWebView.Navigate(args.Uri);   // a genuine in-app redirect (sign-in hop, etc.)
    return;
}
if (TryOpenExternally(args.Uri, args.IsUserInitiated)) { return; }
```

This is preferred over "always show Back" because it keeps the scraped session **never navigated away from**
— the adapter is injected on document creation, so a frame hop also costs the scraper until reload. Showing a
Back button fixes the escape route and leaves the interruption.

**Rejected alternative:** unhide `NavControlsPanel` for scraped platforms. Smaller diff, but it makes the
scraped page freely navigable, which is how the account ends up somewhere the adapter cannot run.

**Test that pins it.** `WebViewNavigationGuardTests` — a new case asserting an allow-listed *cross-site* URI
(`https://faq.whatsapp.com/…` and `https://google.com/…` from a WhatsApp account) is routed to
`IsExternallyOpenableUri` and **not** to an in-frame navigate. The existing tests already avoid launching
anything by asserting on `IsExternallyOpenableUri` rather than calling the launcher — follow that, or the
suite opens browser windows on the machine running it.

**What could break.** Google Business genuinely needs the `business.google.com → www.google.com` hop
(`:178`), and the rating scrape deliberately parks on `www.google.com/search…`. `IsSameSiteAsStartUrl` must
treat those as same-site via the registrable domain, or the Google channel breaks — that is the exact bug the
comment at `:177` records having already been fixed once.

---

## 6 · Increments 94–95 — record and CI

### Increment 94 · `v4.99.63: two docs stated things the tree contradicts (Phase 3 — correctness of the record) (Increment 94)`

Docs only. **XS.** Full detail in [00-remaining-work.md §A5](00-remaining-work.md).

| Fix | Where | Severity |
|---|---|---|
| U6 claims "every pairing passes AA in both themes" — measured against stale surfaces, at full opacity only | `docs/remaining-work.md` §0.1 | **S3** |
| D4 claims "no `.bak` files" — two `pre-clean-*.bak` files are present (the deliberate §0.7 backups). The D4 defect itself *is* closed | `docs/remaining-work.md` §0.2 | **S3** |
| "`ui-smoke` is red on CI" — it is green on `HEAD` and intermittent ~2-in-7 | `docs/remaining-work.md` §0.4 | **S3** |
| Namespace counts: `Oversight` 39→**40**, `Shell` 7→**6**. Structural claim is correct | `AGENTS.md` | **S4** |
| "1863 tests" → **1865** | `AGENTS.md` | **S4** |

**Also add to `AGENTS.md`'s gotcha table** — this one is worth more than the rest combined, because a brief
already got it backwards and it silently corrupts data:

> **Deciding whether your shell is inside an MSIX container.** Write the marker **from your shell** and read
> it from a `Win32_Process`-created process — not the other way round. Container reads fall through to the
> real path when no local copy exists, so an outside-write/inside-read test passes either way and proves
> nothing. Measured 2026-08-28: Claude Code's shell **is** containerised; the redirect target is
> `…\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Local\`.

### Increment 95 · `v4.99.64: the UI smoke job's exit code never reached the workflow (Phase 3 — CI) (Increment 95)`

**This is a probe. Label it as one in the commit body, and revert it if the notice does not read 5.**

| | |
|---|---|
| **Problem** | Failing runs exit **1**. The harness returns 1 only at `Program.cs:17`, before any work — yet failing steps run 150–183 s. So the code is most likely being lost in the `pwsh` wrapper, before `if ($code -eq 5)` can tolerate it. |
| **Owner impact** | Low directly — `ui-smoke` does not gate `release`. High indirectly: a job that is red half the time trains everyone to ignore it, and it carries the structural audit and the full Release suite. |
| **Where** | `.github/workflows/build.yml`, `Run UI smoke validation` |
| **Severity / Effort** | **S3** / **XS** |
| **Evidence** | Exit code 1 on runs 214 and 215: **CONFIRMED** (check-run annotations — readable without repo admin). Green on HEAD, not a timeout: **CONFIRMED**. Unhandled-exception ruled out by measurement (`dotnet run` returns `-532462766`): **CONFIRMED**. The `pwsh` mechanism itself: **LIKELY** — `pwsh` is not installed on this machine, only PS 5.1, and the behaviour is 7.3+-specific, so it could not be reproduced without committing the exact error the brief warns against. |

**Both prior diagnoses are excluded**, not by argument but by exit code: #1 targeted 4-vs-5, #2 targeted 3.
The job returns neither.

**Correction (probe).**

```pwsh
$ErrorActionPreference = 'Continue'
$PSNativeCommandUseErrorActionPreference = $false
dotnet run --project ... -- $exe.Path
$code = $LASTEXITCODE
Write-Host "::notice::ui-smoke harness exit code = $code"
if ($code -eq 5) { exit 0 }
exit $code
```

**How it is verified.** Push, then read the run's **annotations** via
`/actions/runs/{id}/jobs` → `/check-runs/{job_id}/annotations`. No repo admin needed — that is how the exit
code was obtained in the first place.

- Notice reads **5** and the job goes green → confirmed and fixed. Keep, and rewrite §0.4 accordingly.
- Notice reads **3** or **4** → hypothesis dead, but the true code is now visible without admin. Revert the
  `ErrorActionPreference` lines, keep the `::notice::`.

**Still UNKNOWN, and the artifact that settles it directly:** the tail of *Run UI smoke validation* on a
failing run (`/actions/jobs/{id}/logs`, 403 without repo admin). Now that the exit code is known, the thing to
look for is narrower — whether the output ends in a PowerShell `NativeCommandError` **after** the harness's
report printed normally.

**Ask the owner for that tail.** It is one paste and it removes the guesswork entirely.

---

## 6b · Increment 96 — a third status palette, and the dead code holding it up

`v4.99.65: a third status palette, and the dead code holding it up (Phase 3 — deletion) (Increment 96)`

**New — found while verifying Increment 91's lockstep, not by looking for it.**

| | |
|---|---|
| **Problem** | The status palette exists in **three** places, not two. `Themes/Tokens.xaml` and `Services/UmSemanticBrushes.cs` are kept in lockstep by `TheCodePaletteMatchesTokensXamlExactly`. `Services/UmSemanticColors.cs` is a third copy that **nothing checks** — and it is already incoherent. |
| **Owner impact** | None today (see below), which is why this is S3 and not S2. The risk is the pattern: a file whose docstring says "aligned with Tokens.xaml" and is not, sitting next to two that are. |
| **Where** | `UnifiedMessenger/Services/UmSemanticColors.cs` · `UnifiedMessenger/Services/UnifiedMessengerDashboardPresentationHelper.cs` |
| **Severity / Effort** | **S3** / **S** |
| **Evidence** | **CONFIRMED** — values read, referrers enumerated with `git ls-files \| xargs grep -l`. |

**How incoherent, exactly** — this predates any change made this session:

| Const | Value | What that value actually is |
|---|---|---|
| `StatusSuccess` | `#22C55E` | the **dark** theme's success |
| `StatusWarning` | `#F59E0B` | the **dark** theme's warning |
| `StatusDanger` | `#DC2626` | the **light** theme's danger (now `#C81E1E`) |
| `StatusNeutral` | `#64748B` | **neither** theme (light `#5B6773`, dark `#94A3B8`) |
| `StatusMuted` | `#94A3B8` | the dark **Neutral**, not Muted (`#8A97A6`) |

A `const string` cannot be theme-aware, so this class is unfixable in place — that is the point, not an
oversight to patch.

**Correction: delete both files.** They are dead.

- `UmSemanticColors` is referenced only by itself, by `UnifiedMessengerDashboardPresentationHelper`, and by
  two docs.
- `UnifiedMessengerDashboardPresentationHelper` is referenced only by itself and its own two test files.
  **No application code calls it.** Its surface (`FormatRevenue`, `ClientSentimentLabel`) describes a product
  this app is not.

Delete `UmSemanticColors.cs`, `UnifiedMessengerDashboardPresentationHelper.cs`, and the two test files that
keep them alive. Update the two doc references. Expect the test count to fall — record the new baseline in
`AGENTS.md` in the same commit.

**Verify before deleting**, and state the result in the commit body: `grep -rn` both type names across
`*.cs`, `*.xaml` and the injected `Assets/Scripts` and `Assets/Config`, since a resource could in principle
be resolved by string name. Deleting live code because a grep was too narrow is the failure mode here.

### 96-B · While there: the app has no text tokens at all

`Tokens.xaml` declares surfaces, status colours, opacities, font sizes and icon sizes — and **no foreground
colour of any kind**. Every `TextBlock` inherits WinUI's, which is why 88 sites reached for `Opacity` to make
text quieter: there was nothing else to reach for. Increment 92 adds `UmTextTertiary` for four sites; the
fuller set (`UmTextPrimary` / `Secondary` / `Tertiary`, per theme, contrast-checked on all three surfaces the
way the status colours now are) belongs here or in Phase B's token diff. **Not scheduled** — it is a design
decision about hierarchy, not a defect, and Phase B has not run.

---

## 7 · What is NOT in this roadmap yet — outstanding discovery

Named explicitly so it is not mistaken for "nothing left".

### 7.0 · The owner's actual complaint is now UNEXPLAINED — this is the top open question

"Dark theme has no proper visibility" was attributed by the session brief to dimmed text below AA. Increment
91 measured that and it is false: dimmed body text is 7.95:1 at 0.65 on dark, better than light's 5.10:1. The
two contrast failures that *were* found are both in **light** theme. **So nothing found so far explains a
dark-specific complaint, and the complaint should be treated as still open.** A user saying "this looks
wrong" has been right three times running in this repo; the brief's explanation for it being wrong does not
make the observation wrong.

**Leading hypothesis, measured but not yet corroborated with the owner — elevation, not text.** The dark
surfaces are nearly indistinguishable from one another:

| Pair | Dark | Light |
|---|---|---|
| canvas vs sunken | 1.039 | 1.037 |
| sunken vs surface | **1.048** | 1.112 |
| canvas vs surface | 1.089 | 1.072 |

The numbers are similar in both themes, but the *consequence* is not. A light theme gets elevation free from
shadow — a white card on a grey canvas reads as raised even at 1.07:1. **A dark theme cannot use shadow**, because
there is nothing darker than near-black to cast it, which is why dark design systems raise surfaces by
lightening them instead. At 1.048:1 between sunken and surface, that mechanism is absent. And `Tokens.xaml`
declares **no border, divider, stroke or outline colour at all** (confirmed — grepped), so there is no
non-colour fallback either. The plausible rendered result is a flat dark field in which cards, panels and the
sidebar do not separate — which is a fair description of "no proper visibility" and has nothing to do with
contrast ratios of text.

**Label: LIKELY.** It is arithmetic plus a design principle, not an observation. **No UI has been rendered in
this audit.**

**What would settle it, cheaply, in this order:**

1. **Ask the owner one question:** is the problem *reading text*, or *telling panels apart*? That single
   answer discriminates between every hypothesis on the table and costs nothing.
2. Render the dashboard in dark theme and look at it. This is Phase B's first task and is overdue —
   everything in this document about appearance is arithmetic over a token file.

Do not add surface or border tokens before one of those. That would be fixing an undiagnosed complaint, which
is the exact failure this roadmap's §1 is built to prevent.

| Phase | Status | What it would produce |
|---|---|---|
| **B — UI/UX audit** | **Not started.** | Every view in every state (first-run, offline, 15+ accounts, Urdu/Arabic RTL, 200% scaling, high contrast…), researched against Fluent 2 / WCAG 2.2 practice, plus the refinement pass (hierarchy, density, restraint). Deliverable `01-ui-ux.md`. |
| **C — code audit** | **Not started.** | Data-integrity tracing, lifetime/disposal, the 73 `async void` sites, durability, WebView2 attack surface, performance. Deliverable `02-code.md`. Includes re-checking `system-map.md`, `settings-ia-map.md` and the ADRs, which Phase A did not reach and whose history includes false statements. |
| **D — bug hunt** | **One bug found** (93). | Siblings of the 93 shape: state changes with no way back, controls hidden by a condition that does not match when needed, paths that succeed silently while doing nothing. Deliverable `03-bugs.md`. |

**Two cheap, high-value probes should open Phase C/D**, because an account being signed in makes them
answerable for the first time:

1. **D2's falsifiable step** — run the adapter's `diag.stores` enumeration on the live account and look for a
   call-log object store. `remaining-work.md` §0.2 names this exactly; it has been unanswerable until now.
   Converts an UNKNOWN into a fact either way.
2. **Live figure verification** — compare one displayed number end-to-end against the store. §0.4 concedes
   "not one displayed figure has been checked against reality". The last three S1s here came from a number
   looking wrong.

Both are read-only. Neither may write under `%LOCALAPPDATA%\UnifiedMessenger` from this shell — see
[00-remaining-work.md §A0-1](00-remaining-work.md).

---

## 8 · Gated — not schedulable

Re-checked against the tree this session; all five still genuinely gated.

| # | Item | Gate | What unblocks it |
|---|---|---|---|
| 1 | Telegram / Messenger / Instagram DOM scrapers | Needs a live logged-in account per channel. Meta is read-only and fights automation. | **Owner action.** Highest user-facing value once unblocked. All 3 TODOs in the tree are this. |
| 2 | P3-D multi-channel L1 view | Depends on #1. | #1. |
| 3 | Tier-1 ONNX | No model or runtime packaging in the tree. | **Owner decision** on which model, then packaging. |
| 4 | Icon import robustness · brand-logo import | Per-platform live DOM tuning. | #1. |
| 5 | Code-signing the installer | Needs a certificate. | **Owner decision** — it costs money, so it is a deliberate exception to "no recurring cost", not a purchase to be made quietly. Closes F-OFFLINE-01 properly. |

**WONTFIX-BY-CONSTRAINT**, recorded so they are not re-raised:

- **Google message metrics** — Business Messages shut down July 2024, data deleted. Reviews + Q&A only,
  permanently.
- **Google Business Profile API** for rating/total — would be clean and free, excluded by the no-cloud/no-API
  rule *and* gated behind manual Google approval.
- **D2 via the IndexedDB fallback** — message bodies and call outcomes are encrypted at rest; the outcome is a
  getter on the decrypted in-memory model. Not closable from that path. (The store-bridge path is a different
  question — that is probe 1 in §7.)

---

## 9 · Owner decisions — options and consequences, no recommendation acted on

Three and a half open. Detail in [`owner-decisions.md`](../owner-decisions.md).

| # | Question | Options | Consequence of doing nothing |
|---|---|---|---|
| **1½** | The SLA **tile**. Threshold is decided (15 min, closed). Still open: does the tile keep reading `SLA met 0%`? | **B.** Show "median first reply 3h 20m · target 15m" — same true thing, no permanent zero, **no threshold change**. **C.** Leave it. | The most prominent figure on the dashboard reads as a broken metric rather than as distance from a standard, and trains the owner to disregard the band around it. **One instruction either way.** |
| **2** | Google review reply time measured from installation? | **A.** Build it, labelled "since \<install date\>" — real data in weeks, permanent caveat. **B.** Drop the tile. **C.** Leave it saying "not available". | The tile occupies space to explain its own absence. |
| **3** | Backlog cutoff stays at 7 days? | Shorter (3–5) = smaller, more urgent live queue, more hidden. Longer (14) = risks returning to the 466-item list it replaced. | Nothing breaks. This is the one that genuinely benefits from waiting for usage data. |
| **4** | Drop the "Audit Files" commit (`954145e`, ~112 MiB)? | **A.** Leave it — one-off download cost, zero risk. **B.** `filter-repo` + force-push — **every SHA after `954145e` changes**, tags move, every commit link breaks. | Nothing. Files were untracked going forward at v4.99.53, so nothing new accumulates. **Must not be done without explicit instruction.** |

---

## 10 · Verification plan — run for every increment

Identical each time; no increment is done until all of it passes.

```powershell
# 1 · kill the app first — SecondInstanceActivatorTests fails against a live named pipe
Stop-Process -Name UnifiedMessenger -Force -ErrorAction SilentlyContinue

# 2 · full suite, Release, unfiltered. Filters have hidden red tests in this repo before.
dotnet test UnifiedMessenger.Tests/UnifiedMessenger.Tests.csproj -c Release --nologo -v quiet
#    expect: Failed: 0, Passed: >= 1865
```

For any increment touching app code (92, 93):

```powershell
# 3 · publish — -p:Platform=x64 is MANDATORY; without it the installer ships a stale binary
dotnet publish UnifiedMessenger/UnifiedMessenger.csproj -c Release -r win-x64 -p:Platform=x64 --self-contained true --nologo -v quiet

# 4 · installer
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "D:\Projects\Unified Messenger\installer.iss"

# 5 · install, launch, confirm ALIVE
Start-Process "dist\UnifiedMessengerSetup.exe" "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART"
Start-Sleep -Seconds 12
Start-Process "$env:LOCALAPPDATA\Programs\UnifiedMessenger\UnifiedMessenger.exe"
Start-Sleep -Seconds 5
Get-Process UnifiedMessenger

# 6 · confirm the installed binary is the one just built — not the previous version
(Get-Item "$env:LOCALAPPDATA\Programs\UnifiedMessenger\UnifiedMessenger.exe").VersionInfo.FileVersion
```

Then **read `app.log`** on that launch. Three of the last four S1/S2 findings in this repo came from reading
it rather than from reading code; it is not an optional step.

**Version sync — five files in lockstep, every bump:** `UnifiedMessenger.csproj` (`Version`,
`AssemblyVersion`, `FileVersion`) · `app.manifest` · `installer-shared.iss` · `README.md` (the
`**Current release:**` line only) · `CHANGELOG.md` (new section at top). Plus `docs/phase-status.md` header
date and baseline.

**Increment 92 additionally requires seeing it on screen**, in both themes, at 100% and 200% scaling. It is an
accessibility fix; a green contrast test is necessary and not sufficient. Everything measured in this
roadmap is arithmetic over `Tokens.xaml` — **no UI has been rendered or driven in this audit so far**, and
that limitation carries until Phase B.

---

## 11 · Working rules for whoever picks this up

- Branch `feat/<slice>`. **Never work on `main`.** Commit per increment, never a red tree.
- **Never write under `%LOCALAPPDATA%\UnifiedMessenger` from an agent shell** — it is MSIX-containerised and
  the write forks the owner's store invisibly. See [00-remaining-work.md §A0-1](00-remaining-work.md).
- Live data on this machine is the owner's real business data. Log lengths and types, never names, phone
  numbers or message text.
- `*-prompt.md` and `docs/completion-todo.md` are gitignored. Never commit prompts or session scaffolding.
- Do not push, tag, merge or delete without being asked.
- Label every new finding CONFIRMED / LIKELY / UNKNOWN. An UNKNOWN with a named next artifact is worth more
  than a confident guess — this repo has three wrong diagnoses on one CI failure to prove it.
