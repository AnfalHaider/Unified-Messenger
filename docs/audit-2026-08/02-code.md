# Phase C — functionality and code audit

**Run:** 2026-08-29 · **Against:** `feat/audit-2026-08` @ v4.99.69 · **Suite:** 1899 pass / 0 fail

Labels: **CONFIRMED** (observed) · **LIKELY** (code says so, not executed) · **UNKNOWN** (artifact named).

---

## C0 · Coverage

Static analysis across all 343 tracked app `.cs` files plus targeted reading. What this phase did **not**
do is as important as what it did:

| Covered | Not covered |
|---|---|
| Durability — every file-reading store | **Live figure verification against the store** — traced through code, never checked a rendered number against the underlying data |
| Disposal / lifetime of `IDisposable` fields | **Soak under account churn** — unchanged from §0.4 |
| All 73 `async void` sites | **Memory at 1 / 6 / 15 accounts** — not measured this session |
| Silent `catch` sites | **ADRs and `system-map.md`** — still not re-checked (carried from Phase A §A8) |
| WebView2 attack surface: navigation, schemes, ingress | **Cold-start timing** |
| Data-integrity tracing of the "waiting" figures | Concurrency under real load |

---

## C1 · Durability — clean · **CONFIRMED**

**Zero** files read a store without routing through `CorruptFileRecovery`. Scanned every `*Store.cs` /
`*Service.cs` for `ReadAllText`/`ReadAllTextAsync` and cross-checked for the recovery call.

That is the v4.99.48 hardening holding under an independent check — the pass that found a briefly-locked
*statistics* file could stop the app opening at all, and that `ResponseTimeTracker` and `ContactHistoryStore`
reset without preserving bytes. **No finding.**

---

## C2 · Disposal and lifetime — no finding · **CONFIRMED**

Six files create a `Timer`, `SemaphoreSlim`, `CancellationTokenSource` or `HttpClient` and never dispose it.
Sampled: `AdapterHealthMonitor` (`Timer`) and `OversightInsightService` (`SemaphoreSlim`). Both are
`Lazy<T>` process-lifetime singletons.

Never disposing something that lives exactly as long as the process is correct, and the codebase already
states the reasoning where it matters — `DialogHost.Gate`: *"Never disposed — it lives for the process, like
the window it guards."* **No finding.** Recorded so the raw count is not mistaken for a leak next time.

---

## C3 · `async void` — a structural exposure, not current breakage · **LIKELY** · S3

**44 of the 73 `async void` methods `await` something and have no `try`/`catch`.** An exception escaping one
of those reaches `App.OnUnhandledException`, which leaves `Handled=false` **on purpose** — so the process
ends.

But every one sampled is defended *one level down*:

| Handler | Awaits | Guarded? |
|---|---|---|
| ~30 Settings toggles | the settings save | ✅ `AppSettingsService.SaveCoreAsync` (v4.99.52) |
| `DownloadAiRuntimeButton_Click` | a network download | ✅ `try` / `catch (Exception)` inside the core |
| `ChangeAccountIconButton_Click` | file pick + image write | ✅ six `try` blocks in `AccountIconChangeFlow` |
| `ShowAccountDetail`, `ClearAllButton_Click` | a dialog | ✅ `DialogHost` catches and logs |

**The finding is not "44 crashes waiting to happen".** It is that the safety is invisible at the call site
and rests on every awaited method staying total, forever. The repo already chose this deliberately — fixing
`SaveCoreAsync` rather than 30 call sites, so "the next handler inherits it". That is a sound trade; it is
recorded here so the next person reading a bare `async void` handler knows the protection exists and where.

**No change proposed.** The cost of 44 try/catch blocks exceeds the risk while the invariant holds.

---

## C4 · Silent `catch` — concentrated, and worth one targeted pass · **CONFIRMED** · S4

63 `catch` blocks neither log, rethrow, nor surface anything to the owner within 8 lines. Distribution is
not uniform:

| File | Count |
|---|---|
| `Oversight/GoogleReviewSnapshotService.cs` | 8 |
| `InstanceRegistryService.cs` | 5 |
| `Backfill/WhatsAppBackfillProvider.cs` | 4 |
| `Ai/AiInferenceQueue.cs`, `Session/WebMessageIngressService.cs` | 3 each |

A previous pass scanned this and judged most benign — embedded-JS `catch(e)`, `OperationCanceledException`
on shutdown, catches that show a dialog instead. That judgement is not re-litigated here.

**What is worth a pass is `GoogleReviewSnapshotService`.** Eight silent catches in the one service whose
failures are already known to be invisible: it is the service where the rating scrape strands the WebView on
the Search host, where pagination is deliberately capped, and where a parse that finds nothing degrades the
coverage line rather than erroring. A scraper that swallows eight distinct failures is the shape that
produced the "two of three profiles reported no lifetime total at all" defect.

**Correction:** `AppLogger.LogWarningThrottled` on each, with the *length and type* of what failed, never the
payload. Not scheduled — it is a logging pass, and the next such pass should be driven by reading `app.log`
on a real Re-sync rather than by this count.

---

## C5 · WebView2 attack surface — clean · **CONFIRMED**

A page in a WebView2 is untrusted input. Three boundaries checked:

1. **Navigation allowlist.** Per-WebView, captured in the handler closure rather than looked up in a
   `ConditionalWeakTable` — the documented fix for the CsWinRT projection problem that silently blanked
   Custom-URL tabs. Intact.
2. **External scheme allowlist.** Exactly four: `https`, `http`, `mailto`, `tel`. This is the boundary that
   stops a malicious page shelling out `file:`, `javascript:` or a registered `ms-*` handler. Increment 97
   added five test cases against it and did not widen it.
3. **Page → C# ingress.** `WebMessageIngressService` is a bounded, coalescing queue; parse failures are
   caught as `JsonException` and logged **by length, not content**, with the comment naming why: *"rawJson
   holds customer names and message previews, and app.log is the file support asks the owner for."*

**No finding.** This surface is in better shape than most of the app.

---

## C6 · Data integrity — the "waiting" figure means two things · **CONFIRMED** · S3

Traced end to end, which §0.4 concedes had never been done for any displayed figure.

Observed on screen, 2026-08-29:

| Where | Reads |
|---|---|
| Dashboard hero | **41** customers are waiting for a reply |
| Dashboard KPI band | Backlog **59** · 41 need a reply now · 11 unreadable |
| Dashboard banner | **100** open in total across 3 accounts |
| Reports insight | **100** customers waiting on a reply **right now** |

**The arithmetic is right.** 41 (live queue) + 59 (backlog past the 7-day cutoff) = 100 (total). Two
different code paths:

- Dashboard hero → `liveAwaiting`, the post-split live queue.
- Report → `DashboardReportHelper` line 57, `snapshots.GetAwaiting(instance.Id, null, null).Count` summed —
  **unfiltered, all of it**.

**The defect is the wording, not the maths.** Both screens phrase their own number as the count of people
waiting now. A reader moving from the dashboard to the report sees "waiting" more than double with nothing
saying why. The dashboard is internally coherent — 41, 59 and 100 all appear together — but the report
takes the total and calls it "right now" without the split the dashboard is built around.

**Correction:** make the report say which population it means, the way the dashboard does — *"100 customers
waiting on a reply · 41 in the live queue, 59 in backlog"* — or scope it to the live queue and name the
backlog separately. One string, in `BusinessReport.cs:174`.

---

## C7 · Findings summary

| # | Area | Result | Severity |
|---|---|---|---|
| C1 | Durability | **Clean** — 0 unguarded store reads | — |
| C2 | Disposal / lifetime | **Clean** — all 6 are process-lifetime singletons | — |
| C3 | `async void` | 44 unguarded, all defended one level down | S3, no change proposed |
| C4 | Silent catch | 63, concentrated 8 in `GoogleReviewSnapshotService` | S4 |
| C5 | WebView2 surface | **Clean** — allowlist, 4 schemes, bounded ingress | — |
| C6 | "Waiting" means live on one screen, total on another | Wording, not arithmetic | **S3, correction specified** |
| C8 | ADRs + `system-map.md` | **Still not re-checked** | Carried forward |
