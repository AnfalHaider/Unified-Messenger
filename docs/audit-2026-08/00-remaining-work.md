# Phase A — the true remaining-work list

**Session:** 2026-08-28 · **Branch:** `feat/audit-2026-08` · **Baseline:** `main` @ `a146d33`, v4.99.59
**Suite at baseline:** 1865 passed, 0 failed, 0 skipped, 25 s (run this session, Release, app killed first)

Every claim below carries how it was checked. Labels are used strictly:

- **CONFIRMED** — observed it happen this session (command run, output seen).
- **LIKELY** — the code says so; not executed.
- **UNKNOWN** — needs an artifact I do not have. The artifact is named.

Nothing here is inherited. Where a prior document and the tree disagree, the tree wins and the disagreement
is recorded in §A5.

---

## A0 · Corrections to the session brief's own ground truth (§1)

The brief opens by warning that earlier briefs got these wrong and cost real time. Four of its own rows are
wrong. These come first because two of them change how the rest of the work must be done.

| # | Brief said | Measured this session | Label |
|---|---|---|---|
| **A0-1** | "This shell is **NOT** containerised. Proven by writing a marker file via Win32_Process and reading it back." | **It IS containerised.** Redirect target located on disk. | **CONFIRMED** |
| **A0-2** | "`dotnet` — **not on PATH**." | `dotnet` **is** on PATH; `dotnet --version` → `8.0.424`. | **CONFIRMED** |
| **A0-3** | "352 `Opacity="0.x"` uses in XAML … plus 24 more in C#." | **88** in tracked XAML. The 24 in C# is right. 352 is exactly the count *including `bin/`*. | **CONFIRMED** |
| **A0-4** | "135 `SystemFillColor*` references." | **69** in the app directory — exactly the number `StatusContrastTests` pins. | **CONFIRMED** |

Correct rows, re-verified: ISCC at `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`; `api.github.com` returns
200; job *logs* are 403; network works.

### A0-1 · This shell is inside an MSIX container — the brief has it backwards

The brief's probe design is what misled it. Writing a marker *outside* and reading it *inside* succeeds
whether or not a container exists, because container reads fall through to the real path when no local copy
has been written. The discriminating direction is the opposite one.

Written from this shell to `C:\Users\anfal\AppData\Local\um-container-probe.txt`, then read by a process
created via `Invoke-CimMethod Win32_Process Create`. That process reports
`LOCALAPPDATA=C:\Users\anfal\AppData\Local`, `USER=anfal` — same path, same user — and **`File Not Found`**.
This shell sees the file. Same absolute path, two different contents. The redirected copy was then located
directly:

```
C:\Users\anfal\AppData\Local\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Local\um-container-probe.txt
```

Both markers were deleted afterwards.

**Why this matters, concretely.** There is currently **no** container copy of `…\Local\UnifiedMessenger`
(checked — the directory does not exist under `Packages\Claude_pzs8sxrjxfjjc\LocalCache\Local`), so *reads*
of the owner's real store from this shell fall through and are accurate **today**. But the first *write*
from this shell to any file under that path forks it: the app would keep reading the real file while this
shell reads its own shadow, and both would then show identical bytes on comparison. That is exactly the
failure mode `AGENTS.md` already documents, and the brief's inverted claim would have walked into it.

**Working rule for the rest of this audit:** never write under `%LOCALAPPDATA%\UnifiedMessenger` from this
shell. Reads are fine while no shadow exists; re-check before trusting a read after any tool has written
there. To verify the real store, drive `Win32_Process` and write output to `D:\` — writing it under
`%LOCALAPPDATA%` gets redirected too.

### A0-3 · The opacity defect is real, but a quarter the size claimed

`grep -o 'Opacity="0\.[0-9]+"'` over `UnifiedMessenger/` including build output returns **352**. Over
git-tracked files it returns **88**. Every one of the brief's per-level figures is exactly 4× the tracked
count — 168/52/32/28 against a measured 42/13/8/7 — because each `.xaml` is copied into four build
directories.

| Opacity | Tracked uses |
|---|---|
| 0.65 | 42 |
| 0.75 | 13 |
| 0.7 | 8 |
| 0.55 | 7 |
| 0.85 | 6 |
| 0.8 | 5 |
| 0.9 / 0.6 / 0.45 | 2 each |
| 0.5 | 1 |
| **Total** | **88** |

The brief's one correct detail here is the most useful one: **`SettingsPage.xaml` alone holds 51 of the 88**
— 58% of all opacity dimming in the app sits in one file. That makes remediation far smaller than "352
sites" implied, and it names where to start. C# is 24 sites, concentrated in `FirstRunOnboardingHelper` (8)
and the chart controls.

---

## A1 · The dark-theme contrast defect — re-derived, and the brief understates it

I recomputed every ratio from `Themes/Tokens.xaml` independently (WCAG 2.1 relative luminance, foreground
composited over the real surface at the real alpha, then measured against that surface).

**The brief's dark-theme table reproduces exactly** — Success 7.72 / 4.85 / 3.96, Warning 8.19 / 5.14 / 4.13,
Danger 6.36 / 4.10 / 3.37, Info 6.92 / 4.45 / 3.66, Neutral 6.86 / 4.40 / 3.66, Muted 5.92 / 3.91 / 3.26 on
`#17191D` at 1.0 / 0.75 / 0.65. **CONFIRMED.**

**But the brief's reassurance about light theme is wrong**, and it is the sentence that frames this as "dark
theme is broken" rather than "the dimming pattern is broken":

> "light theme survives the same treatment (near-black text at 0.65 on white is 5.41:1)"

That holds for near-black **body text**. It does not hold for the **status tokens**, which are the colours
carrying meaning. On the light sunken surface `#F1F3F6`, at **full opacity, before any dimming**:

| Light token | on `#F1F3F6` @1.0 |
|---|---|
| Muted `#6B7684` | **4.15** ✗ below AA |
| Danger `#DC2626` | **4.34** ✗ below AA |
| Success `#15803D` | 4.51 — hairline |
| Warning `#B45309` | 4.52 — hairline |

So light theme has status tokens that **fail AA before opacity is applied at all**, and every token fails
once dimmed. Correct framing: *dark fails when dimmed; light fails when dimmed and is already marginal on
the sunken surface.* Treating this as dark-only leaves half the defect in place.

### The measurement bug is bigger than "it doesn't model opacity"

`UnifiedMessenger.Tests/WcagContrast.cs` has **no** alpha, blend or composite function anywhere —
**CONFIRMED** (grepped `opacity|alpha|blend|composit` across the contrast tests: zero hits). It exposes only
`Ratio(a, b)` over two opaque hex strings. The brief's point 2 stands.

There is a second drift the brief did not catch. The surfaces the tests measure against are stale:

```csharp
public const string LightCard  = "#FFFFFF";
public const string DarkCard   = "#2D2D30";   // not a colour the app ships
public const string DarkChrome = "#1E1E1E";   // not a colour the app ships
```

The comment says these were "taken from the WinUI defaults the app uses" — true when written, false now. The
app's shipped dark surfaces are `#17191D`, `#121418`, `#0E0F12`, and the light sunken `#F1F3F6` is not tested
at all. So the suite measures the right tokens against **backgrounds that no longer exist**, and never
measures the surface where the light-theme full-opacity failures live.

One saving grace, and it is accidental: `#2D2D30` is *lighter* than `#17191D`, so for light-on-dark tokens
the stale constant is the more demanding test — passing it does imply passing on the real surface. That is
luck, not design, and it does not extend to the light sunken surface, which is simply absent.

**Same shape as the defects the hardening pass kept finding: the check and the thing being checked had
drifted apart, and the suite stayed green.**

---

## A2 · `ui-smoke` — the inherited open question, materially narrowed

The largest single result of Phase A. Three things established that were not known before, and **both
previous diagnoses are now definitively excluded** — not by argument, but because they targeted exit codes
the job does not produce.

### A2-1 · It is currently GREEN on `HEAD` — **CONFIRMED**

The brief states "`ui-smoke` is red on CI". Run **216** (`a146d33`, current `HEAD`) completed
`conclusion: success`; its `ui-smoke` job and its `Run UI smoke validation` step both report `success`. Not
skipped.

| Run | SHA | ui-smoke |
|---|---|---|
| 216 | `a146d33` | ✅ |
| 215 | `17faed8` | ❌ |
| 214 | `7563a39` | ❌ |
| 213 | `db10fef` | ❌ |
| 212 | `db10fef` | ✅ |
| 211 | `0f96bb2` | ❌ |
| 210 | `594534e` | ❌ |

212 and 213 are the **same commit**, opposite outcomes — intermittency confirmed independently.

### A2-2 · It is not a timeout — **CONFIRMED**

`Run UI smoke validation` duration, from the API's `started_at`/`completed_at`:

| Run | Outcome | Duration |
|---|---|---|
| 212 | pass | 126 s |
| 216 | pass | 159 s |
| 214 | fail | 150 s |
| 215 | fail | 183 s |

The ranges overlap and run 214 **failed faster than run 216 passed**. No boundary is being hit; the step
runs to completion and reports a failure.

### A2-3 · The failures exit **1** — and nothing in the harness returns 1 after doing work

From the check-run annotations on the failing jobs:

> run 215 — `"annotation_level": "failure"`, `"message": "Process completed with exit code 1."`
> run 214 — `"annotation_level": "failure"`, `"message": "Process completed with exit code 1."`

**CONFIRMED** for both failures whose annotations were readable. Annotations need no repo admin — only the
*log text* is 403. That is why the exit code was available and the log was not, and it is why this was
reachable this session when it was not before.

Against the harness contract (`UnifiedMessenger.UiSmokeTests/Program.cs`):

| Exit | Meaning | Workflow treats as |
|---|---|---|
| 0 | all modules passed | pass |
| **1** | **`Program.cs:17` — "executable not found at …"** — the only `return 1` in the file | fail |
| 3 | one or more `[FAIL]` module rows | fail |
| 4 | app opened no top-level window (real launch failure) | fail |
| 5 | window opened, UI Automation could not attach (headless runner) | **tolerated → exit 0** |

- **Diagnosis #1** ("`AliveWithoutWindow` misreported as a launch failure") targeted the **4 vs 5**
  distinction. Its tri-state probe is in the tree and works. Irrelevant — the job never returns 4 or 5.
- **Diagnosis #2** ("first-run `ContentDialog` blocks the shell", i.e. a `[FAIL]` row) targeted **exit 3**.
  Also not what happens.

Both were reasonable, both aimed at code paths that do not execute. That is why shipping the first fix
changed nothing.

### A2-4 · What exit 1 can and cannot be

Two further mechanisms tested and **eliminated**:

- **An unhandled exception in the harness.** Measured directly this session: a console app that throws, run
  via `dotnet run`, returns **-532462766** (`0xE0434352`), not 1. **CONFIRMED** by building and running a
  probe app.
- **A failing unit suite.** `ModuleValidationHarness.ExecuteDotnetTest` shells out to `dotnet test` and
  converts a non-zero result into `ModuleValidationResult.Fail(...)` → **exit 3**, not 1. **LIKELY** (read).

That leaves a genuine contradiction, which I am not going to paper over:

> `Program.cs:17` is the only `return 1` and it fires **before any work** — but the failing steps run
> 150–183 s, which is the full harness (two 45 s waits plus the Release unit suite). An immediate
> "executable not found" bail would take seconds.

So exit 1 is **most likely not the harness's return value at all**, but the `pwsh` step wrapping it:

```pwsh
$exe = Resolve-Path "publish/win-x64/UnifiedMessenger.exe"
dotnet run --project ... -- $exe.Path
$code = $LASTEXITCODE
if ($code -eq 5) { Write-Warning "..."; exit 0 }
exit $code
```

GitHub's `shell: pwsh` prepends `$ErrorActionPreference = 'stop'`. If a terminating error is raised between
`dotnet run` and `exit $code` — candidates being PowerShell's native-command error handling reacting to the
harness's `Console.Error.WriteLine` output, or to a non-zero native exit code — the script aborts and the
**step exits 1 without the `if ($code -eq 5)` tolerance ever running**.

That fits every observation, including the one that defeated diagnosis #1: the tri-state probe *did* work,
exit 5 *was* computed, and PowerShell discarded it before the tolerance could act.

**Label: LIKELY, not confirmed.** I could not reproduce it. `pwsh` (PowerShell 7) is **not installed on this
machine** — only Windows PowerShell 5.1 — and the behaviour is version-specific
(`$PSNativeCommandUseErrorActionPreference` exists only in 7.3+ and its default changed in 7.4). Testing it
under 5.1 would be a reproduction that does not reproduce — the exact trap §2 of the brief warns about.

### A2-5 · What would settle it — two options, both cheap

1. **The log tail** (definitive; needs repo admin): last ~30 lines of *Run UI smoke validation* on a failing
   run. Now that the exit code is known the target is **narrower than before** — not a `[FAIL]` row and not a
   `Win32 probe:` line, but whether the output ends in a PowerShell `NativeCommandError` *after* the
   harness's own report printed normally.
2. **A CI-side probe needing no admin** (better first move) — put the answer in the *annotation* rather than
   the log:

   ```pwsh
   $ErrorActionPreference = 'Continue'
   $PSNativeCommandUseErrorActionPreference = $false
   dotnet run ... -- $exe.Path
   $code = $LASTEXITCODE
   Write-Host "::notice::ui-smoke harness exit code = $code"
   if ($code -eq 5) { exit 0 }
   exit $code
   ```

   If failures stop and the notice reads 5, the diagnosis is confirmed and fixed in one change. If it reads
   3 or 4, this hypothesis is dead and the real code is visible without admin.

   **This is a probe, not a fix.** It must be labelled as one and reverted if the notice shows anything but 5.

**Not actioned this session** — the brief forbids fixing what has not been diagnosed, and forbids pushing
without being asked.

---

## A3 · `remaining-work.md` §0, re-checked item by item

### §0.1 · UI/UX

| # | Doc status | Verdict |
|---|---|---|
| U1–U5, U7, U11 | ✅ done, pinned by `DesignScaleTests` | **STILL DONE.** |
| U6 | ✅ measured, "every pairing passes AA in both themes" | **WRONG AS WRITTEN — see A1.** What was measured is measured against stale surfaces and at full opacity only. |
| U8 | ✅ empty-state sweep | **STILL DONE** (LIKELY — not rendered). |
| U9 | ◑ partly; nobody has tabbed by hand | **STILL OPEN, unchanged.** |
| U10 | ✅ pinned by `AccountVocabularyTests` | **STILL DONE.** |

### §0.1a · Two status palettes

**STILL OPEN, exactly as documented.** `SystemFillColor` occurrences in `UnifiedMessenger/` excluding
`bin`/`obj`: **69** — identical to the ceiling `StatusContrastTests.TheSystemPaletteDoesNotSpreadFurther`
asserts (`references <= 69`). The ratchet sits exactly on its limit, so any new use fails the build. The doc
is right; the brief's "135" is wrong.

### §0.2 · Data accuracy

| # | Doc status | Verdict |
|---|---|---|
| D1, D3, D7 | ✅ done | **STILL DONE** (LIKELY). |
| D2 | ◑ disclosed, unclosable from IndexedDB | **STILL OPEN** — and the falsifiable next step it names (run `diag.stores` on a live account) is now *actionable*, because an account is signed in. |
| D4 | ✅ "gone. Verified from outside the container: **no `.bak` files**" | **PARTLY WRONG.** Read from outside the container this session: `instances.json.bak` is genuinely gone (the D4 defect is closed), but the directory **does** contain `oversight-snapshot.json.pre-clean-20260828112513.bak` and `response-times.json.pre-clean-20260828114609.bak` — the deliberate §0.7 backups. The sentence as written is false and would mislead the next reader doing exactly what I did. |
| D5 | ☐ open, deliberately; `MaxPages = 1` | **STILL OPEN, verified.** `GoogleReviewSnapshotService.cs:987` — `internal const int MaxPages = 1;`, consumed at line 1118. |
| D6 | ☐ unobtainable | **STILL OPEN** (owner decision — A4). |
| D8, D9, D10 | ✅ done | **STILL DONE** (LIKELY). |

### §0.2b · Review Desk

`ReviewHealthPanel` is gone, as claimed. Tier 1's stated gap is real: **there is no review equivalent of
`AwaitingOverrideStore`** — `git ls-files` for `*ReviewOverride*` / `*ReviewSnooze*` returns nothing, while
`AwaitingOverrideStore.cs` and its tests exist. **STILL OPEN as documented.**

### §0.3 · Audit findings

| ID | Doc status | Verdict |
|---|---|---|
| F-SNAP-02 | ✅ closed v4.99.51 | **STILL CLOSED** (LIKELY). |
| F-OFFLINE-07 | ☐ open, deliberate | **STILL OPEN.** |
| F-OFFLINE-08 | ✅ closed, "not seen rendered" | **STILL CLOSED in code** (`OfflineAdviceOnScreenTests` present); the caveat stands. |
| F-ORCH-06 | ✅ closed | **STILL CLOSED.** |
| F-METRICS-11 | WONTFIX | **UNCHANGED.** |

### §0.4 · Untested and material

All **STILL OPEN** except one now provably stale:

- "**`ui-smoke` is red on CI**" — **STALE.** Green on `HEAD`, intermittent at roughly 2-in-7. See A2.
- "No screen reader has ever been run" — **STILL OPEN.** Largest single gap.
- "Live metric accuracy unverified end to end" — **STILL PARTLY OPEN**; the doc's own correction is right.
- Toast delivery · taskbar badge glyph · ask-for-a-review with a live candidate · soak under churn · network
  drop with pages loaded · ARM64 never installed · uninstall erasure · five dialogs never opened — all
  **STILL OPEN**, none contradicted.

### §0.5 · Gated on an external dependency — all five still genuinely gated

| # | Item | Still gated? | What unblocks it |
|---|---|---|---|
| 1 | Telegram / Messenger / Instagram DOM scrapers | **YES.** `PlatformDefinition.All` has 9 platforms; `HiddenFromPicker = {telegram, metabusinesssuite, instagram}` so the picker offers 6. All three TODOs in the tree are this item. | A live logged-in account per channel. Owner action. |
| 2 | P3-D multi-channel L1 view | **YES** — depends on #1. | #1. |
| 3 | Tier-1 ONNX | **YES.** No model or runtime packaging in the tree. | A chosen, downloaded model. Owner decision on which. |
| 4 | Icon import robustness · brand-logo import | **YES** — per-platform live DOM tuning. | #1. |
| 5 | Code-signing the installer | **YES.** | A certificate — it costs money, so it needs an explicit owner decision against "no recurring cost", not just a purchase. |

---

## A4 · Owner decisions still open

| # | Decision | Status |
|---|---|---|
| 1 | SLA threshold | **DECIDED 2026-08-28 — stays at 15 minutes.** Closed; not to be reopened. **A sub-question is explicitly still live:** whether the *tile* keeps reading `SLA met 0%` or shows distance from target ("median first reply 3h 20m · target 15m"). Option B changes no threshold. |
| 2 | Google review reply time measured from installation | **OPEN.** A / B / C; recommendation "B or A, not C". |
| 3 | Backlog cutoff stays at 7 days | **OPEN.** Recommendation: leave at 7 until there is usage data. |
| 4 | Drop the "Audit Files" commit from history | **OPEN.** Recommendation A (leave it). Must not be done without explicit instruction. |

So **three and a half** are open, not three. Decision 1's tile question is a real, unanswered,
one-instruction item that a roadmap can silently drop because the heading says "DECIDED".

I am not deciding any of these.

---

## A5 · Where the docs and the code disagree

Beyond A0 (brief), A1 (U6) and A3 (D4):

| # | Where | Claim | Reality | Severity |
|---|---|---|---|---|
| **X1** | `AGENTS.md` service-namespace table | "`Oversight` (39)", "`Shell` → `.Shell` (7)" | `Oversight` = **40**, `Shell` = **6**. | Trivial. The *structural* claim (which four folders are nested) is exactly right — verified with the doc's own command. Only counts drifted. |
| **X2** | `AGENTS.md` running tests | "1863 tests, ~25 s" | **1865**, ~25 s. | Trivial. |
| **X3** | `AGENTS.md` vs the brief on `dotnet` | AGENTS.md uses bare `dotnet build`/`dotnet test`; brief says not on PATH | AGENTS.md is **right**. | Worth noting — a reader trusting the brief adds a full path everywhere for no reason. |
| **X4** | `remaining-work.md` §0.4 | "`ui-smoke` is red on CI" | Green on `HEAD`; intermittent. | Material — frames a live intermittent flake as a steady failure. |
| **X5** | `remaining-work.md` §0.1a | "69 references remain" | **69.** Correct. | None — recorded because the brief contradicts it and the doc is right. |

**The repo's own warning holds**, and the pattern in what I found is worth stating: **structural claims
survive; counted ones rot.** Every disagreement above is a number that moved — except U6 and D4, which are
claims of *verification* that were true of something narrower than the sentence describes. Those are the
dangerous kind, because they read as closed.

---

## A6 · Codebase sweep

- **TODO / HACK / FIXME / XXX in tracked source: 3**, all identical, all `PlatformDefinition.cs`
  (lines 108, 127, 147): `// TODO: Replace with brand-specific glyph or image asset when Phase 5 is
  implemented.` **Judgement: leave.** Accurate markers on a §0.5-gated item, not debt. (A fourth hit is
  `PlatformDescriptionTests.cs:79`, which *forbids* "TODO" in customer-visible copy — a guard, not a marker.)
- **"for now" / "temporary" / "not implemented":** remaining hits are prose in explanatory comments on the
  same gated platforms (`NullPlatformAdapter for now`), plus generated `*.g.cs` under `obj/`. No actionable
  debt.
- **File counts** (tracked): 345 `.cs` in the app, 35 `.xaml`, 23 dialog files, 176 test `.cs`. Matches the
  brief.
- **Verified present** — claims the roadmap will lean on: `AwaitingOverrideStore`, `CorruptFileRecovery`,
  `StoreLoadDurabilityTests`, `TestIsolationTests`, `OfflineAdviceOnScreenTests`, `BusinessHoursCalculator`,
  `LocalDayBoundary`, `InstanceSessionManager.ResolveWarmMode` — all exist, all with tests where claimed.

---

## A7 · A confirmed bug found while verifying Phase A

Not a Phase A deliverable, but confirmed by reading during it — and it widens the seeded example in the
brief's §8.

`WebViewNavigationGuard.HandleNewWindowRequested` hops the **current frame** for any allow-listed host:

```csharp
if (IsAllowedNavigationUri(args.Uri, allowlist))
{
    coreWebView.Navigate(args.Uri);   // Services/Session/WebViewNavigationGuard.cs:337
    return;
}
```

`MainWindow.xaml.cs:71–72` hides back/forward for exactly the platforms that scrape:

```csharp
var isEmbed = !PlatformModuleSettingsHelper.IsPlatformModuleEnabled(instance.Platform);
NavControlsPanel.Visibility = isEmbed ? Visibility.Visible : Visibility.Collapsed;
```

**The blast radius is larger than the brief states.** `BuildDefaultAllowedHosts` adds not just each
platform's host but its whole **registrable domain**, plus `CommonOAuthHosts`. So from WhatsApp Web the
allowlist includes `whatsapp.com` *and* `google.com` and the OAuth hosts — every `*.whatsapp.com` help/FAQ
link and every `google.com` link in a customer message replaces the scraped session in-frame, with no back
button. Recovery is right-click → Refresh WebView, which an owner is unlikely to find.

**Label: CONFIRMED by reading** (both halves read this session, `path:line` above). **Not yet reproduced
live** — that is Phase D, and I will reproduce it before proposing a correction, because a mechanism read is
LIKELY until observed.

---

## A8 · What Phase A did not cover

Stated plainly rather than left implied:

- I did **not** re-verify every ✅ row in §0.2/§0.3 by executing the behaviour. Those are marked LIKELY;
  doing so is Phase C/D work.
- I did **not** read `CHANGELOG.md` v4.99.47→ in full; I used it as a lookup. If the roadmap depends on a
  specific shipped claim, that claim gets verified against the tree, not the changelog.
- `docs/architecture/system-map.md`, `settings-ia-map.md` and the ADRs are **not yet re-checked** against the
  code. The repo's history says two ADRs have carried false statements, so this is a real gap, carried into
  Phase C.
- **No UI was rendered or driven this session.** Everything about appearance in A1 is arithmetic over the
  token file, not observation.
