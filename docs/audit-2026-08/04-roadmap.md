# Roadmap — what is pending

**Updated:** 2026-08-29 · **Branch:** `feat/audit-2026-08` (15 commits, **not pushed**)
**Head:** `fd11631`, v4.99.70 · **Suite:** 1899 pass / 0 fail / 23 s · **App builds:** 0 warnings

Labels used strictly: **CONFIRMED** (observed) · **LIKELY** (code says so, not executed) · **UNKNOWN**
(artifact named).

---

## 0 · Status at a glance

| | |
|---|---|
| **Shipped this session** | 13 increments, v4.99.60 → v4.99.70 |
| **Phases complete** | A (verification), B (UI/UX), C (code), D (bug hunt) |
| **Scheduled and outstanding** | **2** — §2 below |
| **Recorded, unscheduled** | 11 — §3 |
| **Gated on someone else** | 5 — §4 |
| **Owner decisions** | 3½ — §5 |

**Nothing that was scheduled is outstanding.** The two items in §2 are new: one you asked for, one you asked
for in substance.

---

## 1 · What shipped (v4.99.60 → v4.99.70)

| Inc | Ver | What | Verified |
|---|---|---|---|
| 91 | .60 | Contrast tests measured surfaces the app doesn't ship; 2 tokens failed AA | Test seen RED then green |
| 92 | .61 | AI strips white-on-white; card elevation (3.9 → 8.4 HSL pts) | On screen |
| 93 | .62 | **Every** themed token unsafe through `Application.Current.Resources` | On screen |
| 94 | .63 | 8 files each rolled their own brush resolver; `KpiStatCard` resolved in its constructor | On screen |
| 95 | .64 | Every `ContentDialog` rendered light in dark | On screen |
| 96 | .65 | 8 `FontIcon`s with an **empty** `Glyph` — invisible but clickable | On screen |
| 97 | .66 | A followed link replaced the scraped session, no way back | 14 test cases; live repro **blocked** |
| 98 | .67 | 3 contradicting figure-pairs + text that could never wrap | **All four on screen** |
| 99 | .69 | Captions at 0.55 were 3.84:1 in light | Token test |
| 100 | .68 | Third status palette + its dead code, deleted | Enumerated |
| 101 | — | UI-smoke exit code printed to CI annotations (**probe**) | Needs a run |
| 102 | — | 4 false doc claims corrected; 5 gotchas recorded | — |
| 103 | .70 | Corner-radius scale enforced in XAML only; 11 values in C# | Test + snapped |

---

## 2 · SCHEDULED — the two open items

### Increment 104 · Filter reports by branch — **requested** · effort **S**

> *"I want to be able to filter through the reports by branch."*

**Most of this already exists.** The groundwork is in place and unused:

| Piece | State |
|---|---|
| `MessengerInstance.BranchKey` | ✅ real field, editable via right-click → **Set location** and Edit account details |
| `BranchWorkspaceHelper.ResolveBranchKey(instance)` | ✅ falls back to `BranchNameResolver` on the display name, so every account resolves to a branch even unset |
| `BranchWorkspaceHelper.FilterByBranchKey(instances, key)` | ✅ **exists and has no consumer anywhere** — written for exactly this and never wired up |
| `DashboardReportHelper.GatherInputs(instances, days)` | ✅ already takes an **arbitrary subset** — no engine change needed |

**The work is one control and one filter call.**

| Where | Change |
|---|---|
| `Pages/ReportsPage.xaml:51` | Add a `ComboBox x:Name="BranchBox"` beside `RangeBox`, `AutomationProperties.Name="Branch"`, first item **"All branches"** |
| `Pages/ReportsPage.xaml.cs:67` | `var instances = BranchWorkspaceHelper.FilterByBranchKey(_services.Registry.Instances, SelectedBranchKey()).ToList();` |
| `Pages/ReportsPage.xaml.cs` | Populate `BranchBox` from `Registry.Instances.Select(ResolveBranchKey).Distinct()`, sorted; re-render on `SelectionChanged` exactly as `RangeBox` does |

**Do the same on Analytics** (`Pages/AnalyticsPage.xaml:57`) — it has the identical `RangeBox` shape and the
same all-instances call. Filtering one and not the other is how two screens start disagreeing, which this
audit has already had to fix three times.

**Three things to get right, none of them optional:**

1. **Say the scope in the report body, not just the dropdown.** A saved `.md` or exported `.csv` of a
   single branch that does not say so becomes a wrong document the moment it leaves the app. Put the branch
   in `ReportInputs.PeriodLabel`, or beside it, so it reaches the export.
2. **Hide the control when there is one branch.** Same rule the sidebar's scope switch already follows —
   a filter with one option is furniture.
3. **Empty state.** A branch with no accounts in range must say so, not render zeros. The Analytics page
   already has `NoAccountsState`; reuse it rather than inventing a second empty state.

**Test:** `BranchWorkspaceHelper.FilterByBranchKey` has no coverage at all today (it has no callers). Add
cases for: null/empty key returns everything; an unset `BranchKey` still matches via the display-name
fallback; a key matching nothing returns empty rather than everything. That last one is the failure mode
that would silently show the whole business under one branch's name.

---

### Increment 105 · Verify every displayed figure against its source — **requested** · effort **L**

> *"Ensure that all data is accurate."*

This is the largest genuinely-open item in the product and `remaining-work.md` §0.4 concedes it:
*"not one displayed figure has been checked against reality."* One figure was traced during Phase C and it
found a defect, which is the argument for doing the rest.

**What Phase C found by tracing exactly one number** (§C6 of [02-code.md](02-code.md)): the dashboard
headlines the **live** queue (41) while the report headlines the **total** (100), and both phrase it as the
count waiting *right now*. The arithmetic was right — 41 live + 59 backlog = 100 — and the wording was
wrong. **That is the shape to expect: not bad maths, but one noun covering two populations.**

**Method, per figure** — anything less is not verification:

1. Name the figure and the surface it appears on.
2. Trace it to the service call that produces it.
3. State the **population** it covers (which accounts, which date range, live or total, customer-only or all).
4. Check it against the underlying store for one account, by hand.
5. Record whether any *other* surface shows the same noun over a different population.

**The surfaces and roughly what is on them:**

| Surface | Figures |
|---|---|
| Dashboard hero + KPI band | waiting, caught-up %, backlog, response time, SLA met, messages/day, busiest window |
| Dashboard per-account card | reply time, answered today, past-15m target, open count, caught-up % |
| Analytics | messages, response time, replies (15m), SLA met, 4 charts, leaderboard, share donut |
| Reviews | rating, lifetime total, unanswered, oldest waiting, reply rate, new reviews, median reply time, quietest branch |
| Reports | every insight line plus the trend chart |

Roughly **30 figures**. This is an **L**, and it is the one item on this roadmap that cannot be shortened
without making it worthless.

**Known-honest already, do not "fix":** Reviews shows `—` with a reason where Google publishes nothing
(reply dates), the coverage line states how many of the lifetime total were read, and the reply-time trend
now states how many days were measured. Those are correct and are the model for the rest.

**Prerequisite:** do this on a machine with the app **running and scanning**, against the live store, and
**never write to `%LOCALAPPDATA%\UnifiedMessenger` from an agent shell** — it is MSIX-redirected and the
write forks the owner's data invisibly.

---

## 3 · Recorded, unscheduled

| # | Item | Severity | Why not scheduled |
|---|---|---|---|
| **R1** | Six of the eight glyph fixes never seen rendered | debt | Covered by test; desktop shell kept stealing focus |
| **R2** | ~20 of 23 dialogs never opened | debt | `DialogHost` fix should reach them — **LIKELY**, not confirmed |
| **R3** | 10 glyphs remain in the fragile inline form (34 escaped) | S4 | None empty; conversion is mechanical |
| **R4** | Report date-range header near-illegible in dark | S3 | One `Foreground`; batch with the next visual pass |
| **R5** | `AwaitingChatActions` in compact has icon but no label | S4 | Design question, not a defect |
| **R6** | `SystemFillColor*` sits exactly on its ratchet (69) | S4 | Shrinking needs its own contrast pass |
| **R7** | Increment 97 never reproduced live | debt | **WebView2 content is masked by the screenshot filter** — see §6 |
| **R8** | Increment 99's four captions never seen rendered | debt | Token asserted; sites unviewed |
| **R9** | **The installer has never been compiled or run** across any of 13 increments | debt | Every deploy copied publish output over the install. `ISCC`, silent install and the `FileVersion` check are all unexercised |
| **R10** | `themePreference` flipped Dark → Light mid-session | **UNKNOWN** | Cannot attribute — a stray click is as likely as the app. Repro: set Dark, relaunch repeatedly, watch the file |
| **R11** | `GoogleReviewSnapshotService` has 8 silent catches | S4 | Logging pass; drive it from `app.log` on a real Re-sync, not a static count |

---

## 4 · Gated on someone else

| # | Item | What unblocks it |
|---|---|---|
| 1 | Telegram / Messenger / Instagram DOM scrapers | **A live logged-in account per channel.** Highest user-facing value once unblocked |
| 2 | P3-D multi-channel L1 view | #1 |
| 3 | Tier-1 ONNX | A chosen, downloaded model + runtime packaging. **Owner decision** on which |
| 4 | Icon import robustness · brand-logo import | #1 |
| 5 | Code-signing the installer | A certificate — it costs money, so a deliberate exception to "no recurring cost" |

**WONTFIX-BY-CONSTRAINT:** Google message metrics (Business Messages shut down July 2024, data deleted);
the Business Profile API (no-cloud/no-API rule *and* manual Google approval); D2's call outcome via the
IndexedDB fallback (encrypted at rest).

---

## 5 · Owner decisions — nothing acted on

| # | Question | Options | Cost of leaving it |
|---|---|---|---|
| **1½** | The SLA **tile**. Threshold is settled at 15 min and closed. Does the tile keep reading `SLA met 0%`? | **B.** "median first reply 3h 20m · target 15m" — same truth, no permanent zero, **no threshold change**. **C.** Leave it | Monitoring practice is blunt: a threshold nothing ever meets teaches people to ignore the whole band. **One instruction.** |
| **2** | Google review reply time measured from installation? | **A.** Build it, labelled "since \<date\>". **B.** Drop the tile. **C.** Leave it saying "not available" | The tile occupies space to explain its own absence |
| **3** | Backlog cutoff stays at 7 days? | Shorter = smaller, more urgent queue. Longer = risks the 466-item list it replaced | Nothing breaks; benefits from usage data |
| **4** | Drop the "Audit Files" commit (~112 MiB)? | **A.** Leave it. **B.** `filter-repo` + force-push — **every SHA after it changes** | Nothing. **Must not be done without explicit instruction** |

---

## 6 · What no phase could reach

Unchanged, and the honest ceiling on everything above.

- **No screen reader has ever been run.** Still the single largest gap in the product.
- **Nothing behind WebView2.** Its content renders in separate processes the screenshot filter masks, so the
  embedded channels — the actual product surface — have never been visually inspected, and increment 97's
  bug could not be reproduced live.
- **No 150% / 200% display scaling, no narrow-window reflow, no high-contrast toggle, no RTL/Urdu, no 15+
  accounts, no very long names.** Changing the owner's display settings or injecting synthetic accounts into
  their live store was not something to do unasked.
- **ADRs and `system-map.md` still not re-checked** against the code, carried from Phase A. The repo's own
  history says two ADRs have carried false statements.

---

## 7 · Verification plan — every increment

```powershell
# 1 · kill the app — SecondInstanceActivatorTests AND TriagePersistenceServiceTests both fail against a
#     live instance
Stop-Process -Name UnifiedMessenger -Force -ErrorAction SilentlyContinue

# 2 · full suite, Release, unfiltered
dotnet test UnifiedMessenger.Tests/UnifiedMessenger.Tests.csproj -c Release --nologo -v quiet
#    expect: Failed: 0, Passed: >= 1899

# 3 · publish — -p:Platform=x64 is MANDATORY or the installer ships a stale binary
dotnet publish UnifiedMessenger/UnifiedMessenger.csproj -c Release -r win-x64 -p:Platform=x64 --self-contained true --nologo -v quiet
```

Then **read `app.log`**, and for anything visual **look at it on screen in both themes**. Of the twelve
defects fixed this session, four were invisible to a green suite and two were found only because the owner
sent a screenshot.

**Deploying to look at it on this machine** — the installed app is under a redirected path, so drive the
copy from **outside** the MSIX container:

```
Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine='cmd /c "D:\um-swap-new.cmd" > D:\out.txt 2>&1'}
```

The owner's original **v4.99.59 is backed up at `D:\um-installed-backup`**; the machine runs v4.99.70.
**R9 stands: the real installer path is still unexercised.**

---

## 8 · Working rules

- Branch `feat/<slice>`. **Never work on `main`.** Commit per increment, never a red tree.
- **Never write under `%LOCALAPPDATA%\UnifiedMessenger` from an agent shell** — MSIX-redirected; the write
  forks the owner's store invisibly and both paths then read identical.
- Live data on this machine is the owner's real business data — customer names, phone numbers, message
  previews. Log lengths and types, never content, and never transcribe it into a document.
- **Do not bulk-edit XAML by regex.** It mangled the Re-sync button this session, closing a `<Grid>` with
  `</StackPanel>`. Explicit edits.
- Label every finding CONFIRMED / LIKELY / UNKNOWN. An UNKNOWN with a named artifact beats a confident guess
  — this repo has four wrong diagnoses on record, one of them from this session's own brief.
- Do not push, tag, merge or delete without being asked.
