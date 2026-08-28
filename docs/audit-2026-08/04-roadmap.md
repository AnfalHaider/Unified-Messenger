# Roadmap — remaining work

**Updated:** 2026-08-28 · **Branch:** `feat/audit-2026-08` (6 commits, **not pushed**)
**Head:** `dd54f01`, v4.99.65 · **Suite:** 1882 pass / 0 fail / 23 s · **App builds:** 0 warnings
**Audit basis:** [00-remaining-work.md](00-remaining-work.md) (Phase A, complete)

Evidence labels are used strictly throughout:

- **CONFIRMED** — observed it happen (command run and output seen, or seen on screen).
- **LIKELY** — the code says so; not executed.
- **UNKNOWN** — needs an artifact not available. The artifact is named.

---

## 1 · Shipped this session — increments 91–96

Six increments, every one verified. Four of the six defects were **invisible to a green 1882-test suite**
and were found by rendering the app and looking at it.

| Inc | Version | What | Verified by |
|---|---|---|---|
| 91 | v4.99.60 | The contrast check measured surfaces the app does not ship. Found and fixed 2 light-theme AA failures (`UmStatusMuted` 4.15, `UmStatusDanger` 4.34 on the light sunken surface) | Test seen RED on exactly those two, then green |
| 92 | v4.99.61 | AI insight strips were white-on-white in dark. Plus card elevation: dark surface `#17191D`→`#20242B`, hairlines strengthened, 19 card edges migrated off the 1.15:1 system stroke | On screen |
| 93 | v4.99.62 | **Every** themed token was unsafe through `Application.Current.Resources` — the Reviews page was six blank white tiles and white cards | On screen |
| 94 | v4.99.63 | Eight files had each rolled their own brush resolver, bypassing `ThemeBrushResolver`. Plus `KpiStatCard` resolving brushes in its constructor, before `ActualTheme` exists | On screen |
| 95 | v4.99.64 | Every `ContentDialog` rendered light in dark — they live in a popup outside the themed root. Fixed at `DialogHost`, the one choke point | On screen |
| 96 | v4.99.65 | Eight `FontIcon`s shipped with an **empty** `Glyph` — invisible but present, laid out and clickable. This is why `AccountDetailDialog` could not be found | On screen (details icon), + new ratchet |

**The through-line, recorded because it should change how the next session works:** the dark-theme complaint
was real, and the session brief's explanation of it was wrong in every particular. Dimmed body text was never
the problem (7.95:1 on dark at 0.65). The actual causes were four unrelated defects in theme resolution and
one in icon authoring, and **not one of them was reachable by reading code or running tests.** Two were found
only because the owner sent a screenshot.

---

## 2 · Ordering rationale

1. **Instrument before fixing.** Increment 91 fixed the measurement first and immediately falsified the
   defect this roadmap had been written around. Keep that order.
2. **Then what the owner can hit today**, worst first. A stranded WebView in the primary channel outranks a
   caption two shades too dim.
3. **Then figures that contradict each other on screen.** The brief classes two disagreeing numbers in one
   viewport as S1, and there are now three confirmed pairs.
4. **Then deletion and record-keeping**, which are cheap and reduce future error.
5. **Gated work and owner decisions are never scheduled** — §6 and §7.

---

## 3 · Scheduled — the ordered list

### Increment 97 · A followed link strands the account, with no way back — **S1**

`v4.99.66: a followed link stranded the account with no way back (Phase 1 — navigation) (Increment 97)`

| | |
|---|---|
| **Problem** | `WebViewNavigationGuard.HandleNewWindowRequested` hops the **current frame** for any allow-listed host. Back/forward are hidden for exactly the platforms that scrape. |
| **Owner impact** | The primary channel becomes a dead end. Recovery is right-click → Refresh WebView, which an owner will not find. Oversight for that account silently stops until they do. |
| **Where** | `Services/Session/WebViewNavigationGuard.cs:337` · `MainWindow.xaml.cs:71–72` |
| **Effort** | **S** |
| **Evidence** | **CONFIRMED by reading**, both halves. **Not reproduced live** — that is task one inside this increment. |

**Blast radius is wider than first stated.** `BuildDefaultAllowedHosts` (`:162`) adds each platform's host
*and its whole registrable domain*, plus `CommonOAuthHosts`. From WhatsApp Web that includes `whatsapp.com`
**and `google.com`** — so a `google.com` link in a customer message strands the account too. Any fix that
special-cases `whatsapp.com` is incomplete.

**Correction.** Narrow the in-frame hop to a genuine same-site redirect and send everything else to the
owner's browser, which the handler already does for non-allow-listed hosts — and for the reason its own
comment gives: *"Anything else the owner deliberately clicked is THEIR link."*

```csharp
if (IsAllowedNavigationUri(args.Uri, allowlist) && IsSameSiteAsStartUrl(args.Uri, startHost))
{
    coreWebView.Navigate(args.Uri);   // a real in-app redirect (sign-in hop)
    return;
}
if (TryOpenExternally(args.Uri, args.IsUserInitiated)) { return; }
```

Preferred over "always show Back" because the adapter injects on document creation, so a frame hop costs the
scraper until reload — Back fixes the escape route and leaves the interruption.

**What could break.** Google Business needs the `business.google.com → www.google.com` hop, and the rating
scrape deliberately parks on `www.google.com/search…`. `IsSameSiteAsStartUrl` must treat those as same-site
via the registrable domain, or the Google channel breaks — the bug the comment at `:177` records fixing once.

**Test.** `WebViewNavigationGuardTests`: an allow-listed *cross-site* URI (`faq.whatsapp.com`, `google.com`)
from a WhatsApp account routes to `IsExternallyOpenableUri`, not to an in-frame navigate. Assert on that
predicate, never call the launcher — the first version of those tests opened four browser windows.

---

### Increment 98 · Figures that contradict each other on screen — **S1/S2**

`v4.99.67: three pairs of figures disagreed with each other on screen (Phase 3 — data accuracy) (Increment 98)`

All four **CONFIRMED on screen** this session. Grouped because they are one class: the dashboard asserting
two incompatible things in one viewport.

| # | What the owner sees | Why it is wrong | Where |
|---|---|---|---|
| **98-A** | Sidebar badge **"Notification Hub 21"** beside a panel reading **"No notifications yet."** | Two counts of the same thing, in one screenshot, disagreeing. One of them is lying and the owner cannot tell which. | `WorkspaceSidebar` footer badge vs `NotificationFeedPanel` empty state |
| **98-B** | **"Reply speed is healthy — median 1 min across 29 replies"** six rows below **"103 customers waiting on a reply right now."** | Survivorship bias in a headline verdict. The 29 are the conversations that *got* answered; the 103 are not in the denominator. The verdict is computed over the survivors and presented as the state of the business. | `WeeklyReportDialog.Populate` insight rows |
| **98-C** | **"Median first reply, last 7 days"** with one bar (Fri) and six empty days | Reply history restarted 2026-08-28, so six days have **no data** — but they render as zero-height, which reads as "we replied to nobody". The brief names this exactly: a figure showing 0 where it means unknown. | `WeeklyReportDialog.BuildResponseTrend` |
| **98-D** | AI insight line on the dashboard clipped mid-word, no ellipsis | `TextTrimming` not set on that block. | `CommandCenterPanel.BuildInsightStrip` |

**98-B is the one that matters most.** It is the only place in the app that tells the owner their reply speed
is *fine*, and it does so while a hundred people wait. Either qualify it ("across the 29 that were answered")
or compute it over the full population.

**Effort:** A **S**, B **S**, C **S**, D **XS**.

---

### Increment 99 · `UmOpacitySubtle` (0.55) on text is 3.84:1 in light — **S3**

`v4.99.68: dimmed captions fell below AA in light theme (Phase 3 — accessibility) (Increment 99)`

Body text at 0.55 measures **3.84:1** on the light surface — below AA. On dark the same dimming is 6.09:1 and
fine. **CONFIRMED** by composite measurement.

- **Where:** `Themes/Tokens.xaml:204` (`UmOpacitySubtle`) · `Pages/SettingsPage.xaml:827, 871, 896` ·
  `Controls/NotificationFeedPanel.xaml:45`
- **Correction (preferred):** add `UmTextTertiaryColor` per theme — light `#5B6773` (5.20:1 on the sunken
  surface), dark `#8A97A6` (5.92:1) — and give these four sites a real foreground instead of a dim. Four
  sites, not the 88 the original plan assumed.
- **Leave the 0.55 `FontIcon` glyphs alone** (`CommandPalette.xaml:59`, `PersonalOverviewPanel.xaml:397`,
  `ReviewsPage.xaml:28`): non-text, so 1.4.11's 3:1 applies and 3.84 clears it.
- **Test:** once the token exists it can be read from `Tokens.xaml` and asserted on all three surfaces per
  theme, exactly as the status colours now are. It could not be written before the token existed, which is
  why increment 91 used a ratchet instead.
- **Effort:** **S**

---

### Increment 100 · A third status palette, and the dead code holding it up — **S3**

`v4.99.69: a third status palette, and the dead code holding it up (Phase 3 — deletion) (Increment 100)`

The palette exists in **three** places. Two are kept in lockstep by a test. `Services/UmSemanticColors.cs` is
a third that nothing checks — and it is already incoherent, predating any change made this session:

| Const | Value | What that value actually is |
|---|---|---|
| `StatusSuccess` | `#22C55E` | the **dark** theme's success |
| `StatusWarning` | `#F59E0B` | the **dark** theme's warning |
| `StatusDanger` | `#DC2626` | the **light** theme's danger (now `#C81E1E`) |
| `StatusNeutral` | `#64748B` | **neither** theme |
| `StatusMuted` | `#94A3B8` | the dark **Neutral**, not Muted |

A `const string` cannot be theme-aware, so it is unfixable in place — that is the point, not an oversight.

**Correction: delete it.** It is referenced only by itself, by
`Services/UnifiedMessengerDashboardPresentationHelper.cs`, and by two docs. That helper is referenced only by
itself and its own two test files — **no application code calls it**, and its surface (`FormatRevenue`,
`ClientSentimentLabel`) describes a product this app is not. Delete both plus the two test files; update the
two doc references; record the new test count in `AGENTS.md` in the same commit.

**Verify before deleting**, and state the result in the commit body: `grep -rn` both type names across
`*.cs`, `*.xaml`, `Assets/Scripts` and `Assets/Config`. Deleting live code because a grep was too narrow is
the failure mode here. **Effort: S** · **CONFIRMED** by enumeration.

---

### Increment 101 · The UI-smoke exit code never reaches the workflow — **S3, PROBE**

`v4.99.70: the UI smoke job's exit code never reached the workflow (Phase 3 — CI) (Increment 101)`

**Label this a probe in the commit body and revert it if the notice does not read 5.**

What Phase A established, all **CONFIRMED**:

- `ui-smoke` is **green on `HEAD`** and intermittent (~2 of 7 recent `main` runs pass). Runs 212 and 213 are
  the *same commit* with opposite outcomes.
- It is **not a timeout**: passes took 126 s and 159 s, failures 150 s and 183 s — run 214 failed *faster*
  than run 216 passed.
- The failures **exit 1** (runs 214 and 215, from check-run annotations, which need no repo admin).
- The harness returns 1 at exactly one place — `Program.cs:17`, "executable not found" — which fires before
  any work, yet the failing steps run for 150–183 s.
- An unhandled exception via `dotnet run` returns **-532462766**, not 1 — measured directly.

**Both previous diagnoses are excluded**: #1 targeted the 4-vs-5 distinction, #2 targeted exit 3. The job
returns none of those. So exit 1 most likely comes from the `pwsh` wrapper aborting between `dotnet run` and
`exit $code`, before the `if ($code -eq 5)` tolerance can act. **LIKELY** — not reproducible here because
`pwsh` (PowerShell 7) is not installed on this machine and the behaviour is 7.3+-specific.

**The probe** puts the answer in an annotation rather than the log:

```pwsh
$ErrorActionPreference = 'Continue'
$PSNativeCommandUseErrorActionPreference = $false
dotnet run ... -- $exe.Path
$code = $LASTEXITCODE
Write-Host "::notice::ui-smoke harness exit code = $code"
if ($code -eq 5) { exit 0 }
exit $code
```

Reads 5 → confirmed and fixed in one change. Reads 3 or 4 → hypothesis dead, but the true code is now
visible without admin.

**Still UNKNOWN, artifact named:** the tail of *Run UI smoke validation* on a failing run
(`/actions/jobs/{id}/logs`, 403 without repo admin). Now that the exit code is known, the thing to look for
is narrow — whether the output ends in a PowerShell `NativeCommandError` **after** the harness's own report
printed normally. **One paste from the owner removes the guesswork.**

---

### Increment 102 · Correct the record — **S3/S4, docs only**

`v4.99.71: the docs stated things the tree contradicts (Phase 3 — correctness of the record) (Increment 102)`

| Fix | Where |
|---|---|
| U6 claims "every pairing passes AA in both themes" — measured against stale surfaces, full opacity only, and two pairings failed | `remaining-work.md` §0.1 |
| D4 claims "no `.bak` files" — two `pre-clean-*.bak` are present (the deliberate §0.7 backups). The D4 defect itself *is* closed | `remaining-work.md` §0.2 |
| "`ui-smoke` is red on CI" — green on `HEAD`, intermittent | `remaining-work.md` §0.4 |
| §0.2b tier-1 "Left: mark handled / snooze for reviews" — still true, confirm and keep | `remaining-work.md` |
| Namespace counts: `Oversight` 39→**40**, `Shell` 7→**6**. Structural claim is correct | `AGENTS.md` |
| Test count → **1882** | `AGENTS.md` |

**Three new gotchas to add to `AGENTS.md`** — each cost real time this session:

> **Deciding whether your shell is inside an MSIX container.** Write the marker **from your shell** and read
> it from a `Win32_Process`-created process — not the other way round. Container reads fall through to the
> real path when no local copy exists, so an outside-write/inside-read test passes either way and proves
> nothing. Measured 2026-08-28: this shell **is** containerised; the redirect target is
> `…\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Local\`. Never write under `%LOCALAPPDATA%\UnifiedMessenger`
> from an agent shell — it forks the owner's store invisibly, and both paths then compare identical.

> **A `FontIcon` with an empty `Glyph` draws nothing and stays clickable.** Write glyphs as `"\uXXXX"`, never
> as an inline character. Eight shipped empty from the initial commit; all eight were inline, and all 26 in
> escape form were intact. Pinned by `DesignScaleTests.NoFontIconShipsWithAnEmptyGlyph`.

> **`TriagePersistenceServiceTests` is not isolated from a running app** either — it failed once during this
> work with the app open. The existing warning names only `SecondInstanceActivatorTests`.

---

## 4 · Recorded, not yet scheduled

Real, evidenced, and deliberately not in an increment above — either because they need a decision, or
because they are verification debt rather than defects.

| # | Item | Label |
|---|---|---|
| **R1** | **Six of the eight glyph fixes have not been seen rendered.** The details icon and the dialog were confirmed; the two warnings, the scope chip, the two mark-done icons and the two ChangeIconDialog icons are covered by the new test but unseen. The desktop shell kept taking focus. | Verification debt |
| **R2** | **~20 of 23 dialog files have never been opened.** The `DialogHost` theme fix should reach them all — that is **LIKELY**, not confirmed. Five were already on the "never opened by anyone" list before this session. | Verification debt |
| **R3** | **10 glyphs remain in the fragile inline form** (34 are now escaped). None are currently empty. Converting them is mechanical and would remove the failure mode entirely. | S4 |
| **R4** | **Report date-range header** (`Aug 21 – Aug 28, 2026`) is near-illegible in dark — dark grey on a dark card. Confirmed by zoom. | S3 |
| **R5** | **`AwaitingChatActions` in compact density** now has an icon, but the underlying design question stands: a split button with no label in compact is discoverable only by tooltip. | S4 / design |
| **R6** | The **`SystemFillColor*` ratchet sits exactly on its ceiling (69)**. Any new use fails the build. Shrinking it is deliberate work with its own contrast pass. | S4 |

---

## 5 · Outstanding discovery — phases that have not run

This roadmap is built on **Phase A plus what six increments of fixing happened to surface.** It is precise
about what it names and is **not** a complete remaining-work list for the product.

| Phase | Status | What it would produce |
|---|---|---|
| **B — UI/UX audit** | **Not started.** Six pages seen in dark; nothing seen at 150%/200% scaling, in a narrow window, in high contrast, with 15+ accounts, or with Urdu/Arabic RTL text — which matters for a Pakistani business. | `01-ui-ux.md`: per-view state matrix, the refinement pass (hierarchy, density, restraint), a design-token diff. |
| **C — code audit** | **Not started.** | `02-code.md`: data-integrity tracing, lifetime/disposal, the 73 `async void` sites, durability, WebView2 attack surface, performance. Includes re-checking `system-map.md`, `settings-ia-map.md` and the ADRs, which Phase A did not reach and whose history includes false statements. |
| **D — bug hunt** | **Partially, by accident.** Six defects found while fixing others, none by systematic hunting. | `03-bugs.md`: siblings of the shapes already found — invisible-but-present controls, state changes with no way back, paths that succeed silently. |

**The single largest gap is unchanged: no screen reader has ever been run against this product.** Everything
in the accessibility work is right by construction and by test; nobody has listened to it.

**Two cheap probes should open Phase C/D**, both newly answerable because an account is signed in:

1. **D2's falsifiable step** — run the adapter's `diag.stores` enumeration on the live account and look for a
   call-log object store. `remaining-work.md` §0.2 names this exactly; it has been unanswerable until now.
2. **Live figure verification** — trace one displayed number end-to-end against the store. §0.4 concedes "not
   one displayed figure has been checked against reality", and the last three S1s here came from a number
   looking wrong.

Both read-only. Neither may write under `%LOCALAPPDATA%\UnifiedMessenger` from this shell.

---

## 6 · Gated — not schedulable

Re-checked against the tree; all five still genuinely gated.

| # | Item | What unblocks it |
|---|---|---|
| 1 | Telegram / Messenger / Instagram DOM scrapers | A live logged-in account per channel. **Owner action.** Highest user-facing value once unblocked. |
| 2 | P3-D multi-channel L1 view | #1 |
| 3 | Tier-1 ONNX | A chosen, downloaded model plus runtime packaging. **Owner decision** on which. |
| 4 | Icon import robustness · brand-logo import | #1 (live per-platform DOM tuning) |
| 5 | Code-signing the installer | A certificate. It costs money, so it is a deliberate exception to "no recurring cost", not a quiet purchase. |

**WONTFIX-BY-CONSTRAINT**, so they are not re-raised: Google message metrics (Business Messages shut down
July 2024, data deleted — reviews + Q&A only, permanently); the Business Profile API for rating/total
(excluded by no-cloud/no-API *and* gated behind manual Google approval); D2 via the IndexedDB fallback
(bodies and call outcomes are encrypted at rest).

---

## 7 · Owner decisions — options and consequences, none acted on

Three and a half open. Full write-ups in [`owner-decisions.md`](../owner-decisions.md).

| # | Question | Options | Cost of doing nothing |
|---|---|---|---|
| **1½** | The SLA **tile**. Threshold is decided (15 min, closed, not to be reopened). Still open: does the tile keep reading `SLA met 0%`? | **B.** Show "median first reply 3h 20m · target 15m" — same true thing, no permanent zero, **no threshold change**. **C.** Leave it. | Reads as a broken metric rather than distance from a standard, and trains the owner to disregard the whole band. **One instruction either way.** |
| **2** | Google review reply time measured from installation? | **A.** Build it, labelled "since \<install date\>" — real data in weeks, permanent caveat. **B.** Drop the tile. **C.** Leave it saying "not available". | The tile occupies space to explain its own absence. |
| **3** | Backlog cutoff stays at 7 days? | Shorter (3–5) = smaller, more urgent live queue, more hidden. Longer (14) = risks the 466-item list it replaced. | Nothing breaks. The one decision that genuinely benefits from waiting for usage data. |
| **4** | Drop the "Audit Files" commit (`954145e`, ~112 MiB)? | **A.** Leave it — one-off download, zero risk. **B.** `filter-repo` + force-push — **every SHA after it changes**, tags move, every commit link breaks. | Nothing. Files were untracked going forward at v4.99.53. **Must not be done without explicit instruction.** |

Note the SLA tile question is live despite its heading saying DECIDED — the threshold is settled, the tile is
not, and a roadmap can silently drop it for exactly that reason.

---

## 8 · Verification plan — run for every increment

```powershell
# 1 · kill the app first — SecondInstanceActivatorTests AND TriagePersistenceServiceTests both fail
#     against a live instance
Stop-Process -Name UnifiedMessenger -Force -ErrorAction SilentlyContinue

# 2 · full suite, Release, unfiltered. Filters have hidden red tests in this repo before.
dotnet test UnifiedMessenger.Tests/UnifiedMessenger.Tests.csproj -c Release --nologo -v quiet
#    expect: Failed: 0, Passed: >= 1882
```

For any increment touching app code:

```powershell
# 3 · publish — -p:Platform=x64 is MANDATORY or the installer ships a stale binary
dotnet publish UnifiedMessenger/UnifiedMessenger.csproj -c Release -r win-x64 -p:Platform=x64 --self-contained true --nologo -v quiet

# 4 · installer
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "D:\Projects\Unified Messenger\installer.iss"

# 5 · install, launch, confirm ALIVE, then confirm the FileVersion is the one just built
```

Then **read `app.log`** on that launch, and for anything visual **look at it on screen in both themes**. Six
increments this session; four of their defects were invisible to the suite and visible in one screenshot.

**Version sync — five files in lockstep:** `UnifiedMessenger.csproj` (`Version`, `AssemblyVersion`,
`FileVersion`) · `app.manifest` · `installer-shared.iss` · `README.md` (the `**Current release:**` line) ·
`CHANGELOG.md`. Plus `docs/phase-status.md` header.

**Deploying to look at it on this machine** (the installed app lives under a redirected path):

```
# from OUTSIDE the MSIX container — a plain copy from the agent shell silently shadows it
Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine='cmd /c "D:\um-swap-new.cmd" > D:\out.txt 2>&1'}
```

The owner's original **v4.99.59 is backed up at `D:\um-installed-backup`**; the machine currently runs
v4.99.65 from this branch.

---

## 9 · Working rules

- Branch `feat/<slice>`. **Never work on `main`.** Commit per increment, never a red tree.
- **Never write under `%LOCALAPPDATA%\UnifiedMessenger` from an agent shell** — it is MSIX-containerised and
  the write forks the owner's store invisibly.
- Live data on this machine is the owner's real business data — customer names, phone numbers, message
  previews. Log lengths and types, never content. Do not transcribe it into documents.
- `*-prompt.md` and `docs/completion-todo.md` are gitignored. Never commit prompts or session scaffolding.
- Do not push, tag, merge or delete without being asked.
- Label every finding CONFIRMED / LIKELY / UNKNOWN. An UNKNOWN with a named next artifact beats a confident
  guess — this repo has three wrong diagnoses on one CI failure, and one more from this session's own brief,
  to prove it.
