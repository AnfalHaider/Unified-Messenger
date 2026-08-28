# Roadmap — remaining work

**Updated:** 2026-08-29 · **Branch:** `feat/audit-2026-08` (13 commits, **not pushed**)
**Head:** `abf3824`, v4.99.69 · **Suite:** 1899 pass / 0 fail / 23 s · **App builds:** 0 warnings
**Audit basis:** [00-remaining-work.md](00-remaining-work.md) (Phase A, complete)

> ### ✅ Every increment this roadmap scheduled (91–102) has shipped.
>
> Twelve increments, v4.99.60 → v4.99.69. What remains is in **§4** (recorded, unscheduled), **§5**
> (phases B/C/D, which have not run), **§6** (gated) and **§7** (owner decisions). Nothing in §3 is
> outstanding.

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

## 3 · Shipped — increments 97–102

All six landed. Each verified as noted; none pushed.

| Inc | Version | What | Verified |
|---|---|---|---|
| **97** | v4.99.66 | **A followed link replaced the scraped session with no way back.** `HandleNewWindowRequested` hopped the frame for any *allow-listed* host, and the allowlist spans each platform's whole registrable domain plus the OAuth hosts — so from WhatsApp Web it covered all of `whatsapp.com` **and `google.com`**. Back/forward are hidden for exactly the WhatsApp family. Routing is now `ResolveNewWindowAction`: same **host** or an OAuth host replaces the frame, everything else opens in the owner's browser. | 14 test cases. **Live repro blocked, not skipped** — WebView2 renders in separate processes the screenshot filter masks, so the page came back blank and no link could be clicked. |
| **98** | v4.99.67 | **Three pairs of figures that disagreed on screen, and text that could never wrap.** Badge-vs-panel, the survivorship verdict, the no-data-as-zero chart, and wrapping text inside horizontal `StackPanel`s. | **All four on screen.** |
| **99** | v4.99.69 | **Captions at 0.55 were 3.84:1 in light — below AA.** The app had *no* foreground token at all, which is why 88 sites reached for `Opacity`. Added `UmTextTertiary` per theme; four caption sites migrated; ratchet 88 → 84. | Test on all three surfaces per theme. Not seen rendered. |
| **100** | v4.99.68 | **A third status palette and the dead code holding it up, deleted.** `UmSemanticColors` was theme-blind, unchecked, and already incoherent; its only consumer's only consumers were its own tests. | Enumerated across `*.cs`, `*.xaml`, `Assets/Scripts`, `Assets/Config`. |
| **101** | — | **UI-smoke exit code printed into the run annotations.** A probe, labelled as one. | Needs a CI run. |
| **102** | — | **Four false doc claims corrected; five gotchas recorded** in `AGENTS.md`, each of which cost time this session. | — |

### What increment 98 fixed, in the owner's words

- **Badge vs panel.** "Notification Hub 21" beside "No notifications yet." Two different quantities under
  one label — the badge counts unread *messages*, the panel lists *alerts*. Neither number changed; the
  empty state now names what the badge counts instead of denying it.
- **The survivorship verdict.** "Reply speed is healthy — median 1 min across 29 replies" sat six rows below
  "103 customers waiting". The median only includes conversations that *got* a reply. Now: *"Reply speed
  looks good for the 30 that were answered … but 100 customers are still waiting and are not in that
  figure."*
- **No data drawn as zero.** A day with no measured replies contributed an *empty* column — identical to a
  zero-height bar. Those days now carry a "–" and the heading states coverage: *"2 of the last 7 days
  measured."*
- **Text that could not wrap.** A horizontal `StackPanel` measures children with infinite width, so
  `TextWrapping` had nothing to act on. The dashboard's AI briefing was cut mid-word; the same shape was in
  the Activity insights and all three About-page rows. All converted to `Grid` Auto+`*`.

> **A note on method, because it nearly cost a regression.** The first attempt at that last item converted
> the XAML by regex and silently mangled the Re-sync button, closing a `<Grid>` with `</StackPanel>`. It was
> caught by reading the diff, reverted, and redone as explicit edits. Bulk-editing XAML by pattern is how
> that happens.

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
| **R7** | **The navigation fix (97) was never reproduced live.** WebView2 renders in separate processes that the computer-use screenshot filter masks, so an account's page comes back as a blank frame and no link can be clicked. Proven deterministically by 14 test cases instead. **LIKELY-observed, not CONFIRMED-on-screen** — the one increment this session that is not. | Verification debt |
| **R8** | **Increment 99's four caption sites were not seen rendered.** The token is asserted on all three surfaces per theme; the migrated captions themselves are unviewed. | Verification debt |
| **R9** | **The installer was never compiled or run this session.** Every deploy went by copying the publish output over the installed app from outside the MSIX container. `ISCC` + a silent install + the `FileVersion` check are therefore unexercised across all twelve increments. | Verification debt |
| **R10** | **`themePreference` flipped from `Dark` to `Light`** in `settings.json` mid-session (file written 22:43). Cannot be attributed — a stray click of mine during dialog hunting is as likely as the app rewriting it. Recorded rather than claimed. Reproducing means setting Dark, relaunching several times, and watching the file. | **UNKNOWN** |
| **R11** | **"median · 1 replies"** — the reply-time tile does not singularise. Seen on screen 2026-08-29. | S4 |

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
