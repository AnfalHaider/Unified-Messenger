# Cursor execution prompt — Professional Dashboard cards improvement

Copy everything below the line into a **new Cursor Agent** session (Agent mode). Repo: `d:\Projects\Unified Messenger`. Baseline: **v1.0.7** (`BackfillSyncManager`, startup backfill, 484 x64 tests).

---

## Role & mission

Act as an Elite **WinUI 3 / .NET 8** desktop architect and **dashboard/telemetry** engineer for Unified Messenger.

**Mission:** Improve **Professional Operations** dashboard cards so that **connected accounts** show accurate, actionable data—not misleading empty states. Connected ≠ scraped ≠ triaged ≠ replied; each card must express that distinction.

**Ground truth (read first):**

- `d:\Projects\Unified Messenger\README.md` (v1.0.7, data paths)
- `d:\Projects\Unified Messenger\ENHANCEMENT_ROADMAP.md` (DASH-* items)
- Prior analysis: connected sidebar + sparse dashboard = **pipeline gaps**, not auth failures

**Protocol:** Analyze → Research → Build → Verify. Do not skip Research (read call sites before writing).

| Step | Action |
|------|--------|
| Analyze | Map each card → data source → empty-state cause; cite file/line |
| Research | Read files in §3 before any UI/code change |
| Build | Implement only the approved sub-phase (or full order if cleared) |
| Verify | `dotnet test UnifiedMessenger.Tests\UnifiedMessenger.Tests.csproj -p:Platform=x64`; manual checklist |

**Checkpoint halts:** After each sub-phase **P0 / P1 / P2**, stop and present: architecture table, files touched, test count, manual steps. Wait for explicit green light unless the user message includes:

> **You are cleared to proceed with the entire build order for Dashboard Cards.**

---

## Problem definition (symptoms vs root cause)

| Symptom (user screenshot) | Root cause in tree today |
|---------------------------|---------------------------|
| Google card: “Connect a Google Business Profile…” | `GoogleReviewAlertsEmptyText` is static; shown when `reviewItems.Count == 0` even if `googlebusiness` instances exist (`DashboardPage.xaml` ~L255–260, `ApplyEnterpriseTelemetryToView`) |
| Urgency queue empty; sentiment shows “5 neutral” | `UrgentQueue` only includes `UrgencyScore >= 30` (`MessageTriageService.BuildSnapshot`); neutral/low-urgency items excluded |
| Executive Insights empty | `BuildExecutiveInsights` filters `HasExecutiveInsightContent` (Local AI or rich entities); heuristic-only backfill often excluded |
| Meta: “Last inbound 4m ago”, Samples 0, Awaiting data | `SampleCount` from reply pairs (`RecordMessageSent`); backfill does not fake sends; inbound from `_metaInbound` / analytics receive only |
| Avg reply time / Response rate “—” | `HasReplyMetrics` false when `replyCount == 0` (`MessageAnalyticsService.CaptureProfessionalSnapshot`) |
| SLA breaches 0 | Threshold not exceeded or no aged backfill latencies |
| Message volume 5 received, 0 sent | Backfill inbound works; no outbound logged |
| Operational Highlights empty | `BuildOperationalHighlights` requires `LastChatHint` + send (`LastSentUtc`) |
| Sentiment chart looks blank | Sparse `WeeklySentiment` + minimal dot rendering (`SentimentActivityChart`) |

**Non-goal:** Do not fake `RecordMessageSent` or notification toasts to populate KPIs.

---

## Build order (phases)

### P0 — Truthful empty states + urgency visibility (2–4 days)

#### P0a — Context-aware empty states

**Deliverables:**

| Component | Requirement |
|-----------|-------------|
| `DashboardCardEmptyReason` (enum) | e.g. `NoPlatformInstance`, `ConnectedAwaitingScrape`, `ConnectedNoData`, `HasData` |
| `DashboardPageHelper` | Helpers: `ResolveGoogleTrustEmptyReason`, `ResolveMetaResponseEmptyReason`, `ResolveUrgencyEmptyReason`, `ResolveExecutiveInsightsEmptyReason` — inputs: instance lists, scrape/trust/triage snapshots |
| `DashboardPage.xaml` | Replace static empty strings with bound or code-behind-driven text per reason |
| `DashboardPage.xaml.cs` | `ApplyEnterpriseTelemetryToView`, `ApplyTriageTelemetryToView`, `ApplyExecutiveInsightsToView` set empty text + optional action hint |

**Copy requirements (examples):**

- Google, instances exist, no reviews: *“No unreplied reviews right now. Open Reviews in the instance, then press Refresh.”*
- Google, no instances: *“Add a Google Business Profile professional instance…”*
- Meta, connected, samples 0, recent inbound: *“Inbound detected. Reply in Meta Business Suite to log response time.”*
- Urgency, triage items exist but none ≥ 30: *“No urgent items. See Recent inbound below.”* (feeds P0b)

**Tests:** `DashboardPageHelperTests` — empty-reason resolution for mocked snapshots.

#### P0b — Urgency queue: “Recent inbound” section

**Deliverables:**

| Component | Requirement |
|-----------|-------------|
| `MessageTriageService.BuildSnapshot` | Add `RecentInbound` (e.g. top 8 by time, all scores) OR lower urgent threshold to 15 with `UrgencyLabel` “Low” |
| `MessageTriageDashboardSnapshot` | New property + model update |
| `DashboardPage.xaml` | Second `ListView` or combined template: **Urgent** (≥30) + **Recent** (&lt;30) |
| Empty state | Only when zero professional triage items total |

**Preference (document choice):** Option B — secondary list “Recent inbound” without lowering urgent semantics.

**Tests:** Snapshot includes recent items when score &lt; 30; urgent still capped at 12.

#### P0c — Branch filter subtitle

- Show “Showing: {branch name}” or “All Branches (N)” under Professional Operations tab.
- **Files:** `DashboardPage.xaml`, `DashboardPage.xaml.cs` (`RefreshBranchFilter` / `BindProfessionalTelemetryToView`).

---

### P1 — Fill cards from existing pipelines (3–5 days)

#### P1a — Customer Trust Card (Google)

| Task | Detail |
|------|--------|
| Scrape trigger | Ensure `DashboardScrapeOrchestrator.RefreshProfessionalInstancesAsync` runs for filtered Google instances on Dashboard Refresh + after branch change (verify `RequestBranchScrapeRefreshAsync`) |
| Scrape status UI | Surface last scrape ok/fail from `DashboardScrapeStatusHandler` / `AdapterMessageTypes` on card footer |
| Rating | `CaptureCustomerTrust` — show aggregate when `google-review-snapshot` unreplied &gt; 0 or pending alerts exist |
| Empty reason | Wire P0a; never show “Connect…” when `FilteredGoogleBusinessInstances.Any()` |

**Research:** `google_business_scraper.js`, `PlatformAdapters.cs` (GoogleReviewSnapshot/Alert), `ProfessionalWorkspaceService.cs`.

**Tests:** Extend `DashboardPageHelperTests` or handler tests for trust display when instances exist, zero reviews.

#### P1b — Meta Response Time card

| Task | Detail |
|------|--------|
| DOM hints | Verify `meta-telemetry-snapshot` consumed in `ProfessionalWorkspaceService`; `ResolveDomAverageDisplay` / `ClassifyEfficiencyFromDomHint` used when `sampleCount == 0` |
| UX | When `LastInbound` fresh and `SampleCount == 0`, show P0a copy + optional “Pending response” chip from `ActiveUnreadCount` |
| `BuildMetaResponseDisplay` | Distinguish `HasData` for inbound-only vs full reply metrics |

**Tests:** `ProfessionalWorkspaceService` or `DashboardPageHelperTests` for inbound-only meta state.

#### P1c — Executive Insights

| Task | Detail |
|------|--------|
| Heuristic fallback | New `BuildHeuristicInsightCard` when item has triage but fails `HasExecutiveInsightContent` — label **“Heuristic”**, show preview + urgency + sentiment |
| Local AI gate | Empty state: “Enable Local AI and ensure Ollama is running for rich extraction.” |
| Cap hint | Optional subtitle when backfill AI slots exhausted (read `BackfillContext` / triage `InferenceSource`) |

**Research:** `DashboardPageHelper.BuildExecutiveInsights`, `MessageTriageInferenceRunner`, `RichTriageStoreService`.

**Tests:** `DashboardPageHelperExecutiveInsightsTests` — heuristic card appears without LocalAi.

#### P1d — Top KPI row (honest metrics)

| Task | Detail |
|------|--------|
| `CaptureProfessionalSnapshot` / `BuildProfessionalDisplay` | When `!HasReplyMetrics` but `received > 0`: show **“Inbound: N · Replied: 0 (0%)”** instead of “—” for response rate |
| Avg reply | “No replies logged yet” subtext instead of bare “—” |
| SLA | Subtext with current `SlaThresholdMinutes` from settings |

**Tests:** `DashboardPageHelperTests` / `MessageAnalyticsService` display builders.

#### P1e — Sentiment chart density

| Task | Detail |
|------|--------|
| `SentimentActivityChart.xaml(.cs)` | Minimum visual height for non-zero counts; show numeric labels on hover or under day |
| Legend | Bind to `PositiveCount` / `NeutralCount` / `NegativeCount` prominently |

**Tests:** `WeeklyActivityChartHelper`-style tests if applicable, or control unit tests for non-zero day rendering logic.

---

### P2 — Operator ergonomics (3–5 days)

#### P2a — Professional data health strip

| Component | Requirement |
|-----------|-------------|
| Banner | Below Professional Operations tab: per-instance chips — Backfill state (`BackfillSyncManager.GetState`), adapter health, triage count |
| Button | **Refresh all professional data** → backfill schedule (if Skipped/Failed and enabled), `DashboardScrapeOrchestrator.RefreshProfessionalInstancesAsync`, `MessageAnalyticsService.NotifyDashboardRefresh()` |

**Files:** new `DashboardDataHealthHelper.cs` or extend `DashboardPageHelper`; `DashboardPage.xaml`.

**Constraint:** Do not block UI thread; mirror `RequestProfessionalTelemetryRefreshAsync` patterns.

#### P2b — Unified Refresh behavior

- Document in code comment: Google/Meta need instance WebView session or visible DOM for scrape.
- Optionally queue scrape for all professional instances with active sessions (not only filtered branch).

#### P2c — Settings: Dashboard section (optional if time)

- Urgency threshold slider (15–50) persisted in `AppSettings` → filters `UrgentQueue`.
- Toggle: “Show heuristic insights” (default on).

---

### P3 — Deferred (do not implement unless user explicitly expands scope)

- Unified “Professional inbox feed” card
- Per-branch mini status tiles in sidebar
- Operational Highlights from inbound/reviews (not only sends)
- Debug observability panel

---

## Integration constraints (non-negotiable)

| Constraint | Rule |
|------------|------|
| Backfill | Do not break `BackfillSyncManager`, `__umCommitInboundBaseline`, or dedupe registry |
| Analytics | No fake `RecordMessageSent`; inbound-only display is allowed |
| Live pipeline | Meta/Google scrapers + WhatsApp live monitors unchanged except scrape triggers |
| Tray / quit | No changes to `_forceShutdown`, tray quit, `RichTriageStoreService.FlushAsync` |
| Ollama | Executive Insights LLM stays **Background**; no Interactive preemption |
| Scope | Minimize unrelated refactors; match `Debug.WriteLine` and naming conventions |
| Tests | Add/update tests in `UnifiedMessenger.Tests`; gate: all pass, report count (484+) |

---

## Files to inspect first (Research checklist)

```
UnifiedMessenger/Pages/DashboardPage.xaml
UnifiedMessenger/Pages/DashboardPage.xaml.cs
UnifiedMessenger/Services/DashboardPageHelper.cs
UnifiedMessenger/Services/MessageAnalyticsService.cs
UnifiedMessenger/Services/MessageTriageService.cs
UnifiedMessenger/Services/MessageTriageScorer.cs
UnifiedMessenger/Services/ProfessionalWorkspaceService.cs
UnifiedMessenger/Services/DashboardScrapeOrchestrator.cs
UnifiedMessenger/Services/Adapters/DashboardScrapeStatusHandler.cs
UnifiedMessenger/Services/Adapters/PlatformAdapters.cs
UnifiedMessenger/Services/Backfill/BackfillSyncManager.cs
UnifiedMessenger/Controls/SentimentActivityChart.xaml(.cs)
UnifiedMessenger/Controls/WeeklyActivityChart.xaml(.cs)
UnifiedMessenger/Assets/Scripts/google_business_scraper.js
UnifiedMessenger/Assets/Scripts/meta_business_scraper.js
UnifiedMessenger.Tests/DashboardPageHelperTests.cs
UnifiedMessenger.Tests/DashboardPageHelperExecutiveInsightsTests.cs
```

---

## Tests (required before commit)

| Test class | Covers |
|------------|--------|
| `DashboardPageHelperTests` | Empty reasons, KPI inbound-only display, Google/Meta copy inputs |
| `DashboardPageHelperExecutiveInsightsTests` | Heuristic fallback cards |
| `MessageTriageService` / snapshot tests | `RecentInbound` vs `UrgentQueue` |
| Extend existing branch/scrape tests if scrape status surfaced |

**Command:**

```powershell
dotnet test UnifiedMessenger.Tests\UnifiedMessenger.Tests.csproj -p:Platform=x64
```

---

## Manual verification (include in final report)

1. 7+ professional instances connected (WhatsApp, Google, Meta mix).
2. **Settings:** Local AI on (for rich Executive Insights); startup backfill on.
3. Restart app; wait for **Connected** on professional instances.
4. **Dashboard → Professional Operations → Refresh**; open Google + Meta instance tabs; Refresh again.
5. **Customer Trust:** Never shows “Connect…” when Google pro instances exist; shows reviews OR explicit caught-up/scrape-pending.
6. **Urgency:** Urgent items **or** Recent inbound list populated when triage has low-urgency items.
7. **Executive Insights:** Heuristic cards **or** Local AI cards **or** clear enable-Ollama message.
8. **Meta Response:** Inbound-only state copy when samples=0; samples increase after real reply in Meta UI.
9. **KPI row:** Response shows inbound/replied split, not misleading “—”.
10. **Sentiment:** Chart visibly reflects “5 neutral” on Today.
11. Reply once in professional WhatsApp/Meta → highlights / reply metrics update.
12. Branch filter: subtitle correct; switching branch updates cards.

---

## Deliverables (final message format)

1. **Architecture table** — Card | Data source | Empty-state rule | Change
2. **Code summary** — New/updated files by sub-phase P0/P1/P2
3. **Test count** — e.g. 484 → N passed
4. **Manual verification checklist** (above)
5. **Known limitations** — DOM scrape requires visible Google/Meta UI; WhatsApp reviews N/A

**Git / release:** Do not bump version, push, or rebuild installers unless the user explicitly asks in that session.

---

## Optional analysis-only opening

If the user pastes only this block first:

```
CHECKPOINT HALT — Analysis only
Do not modify code. Reply ONLY with:
- Per-card data flow diagram (mermaid)
- Empty-state matrix (card × reason × recommended copy)
- P0/P1/P2 risk table
- Exact file/method hook list for P0a
Wait for green light.
```

---

## Success criteria (definition of done)

- No professional dashboard card shows **“Connect Google…”** when a Google Business professional instance is registered and connected.
- User with 5 neutral triage items sees them in **Urgent** or **Recent inbound**, not a blank queue with only sentiment counts.
- Executive Insights shows at least heuristic cards when triage exists, or explicit Local AI guidance.
- Meta card explains samples=0 vs inbound detected without implying disconnection.
- All x64 tests pass; no regression to backfill or live inbound pipelines.

---

*End of prompt — copy from “Role & mission” through “Success criteria” into Cursor.*
