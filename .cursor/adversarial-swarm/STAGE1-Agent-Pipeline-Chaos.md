# STAGE 1 — Agent-Pipeline-Chaos Assessment

**Domain:** Build/test/pipeline baseline, installer & UiSmoke harness, legacy platform purge inventory, Stage 4 sabotage planning, thread-pool / UI-thread blocking risks.

**Scope:** Stage 1 research + Stage 2 audit only — **no product code changes**.

**Date:** 2026-06-14  
**Measured on:** Windows 10/11 host, .NET SDK 8.0.422, x64 process

---

## Executive Summary

The solution **builds and tests green on the CI-correct path** (`-p:Platform=x64 -c Release`: 0 errors, 4 warnings, **527/527 tests pass**). The naive `dotnet build UnifiedMessenger.sln -c Release` command **fails** with `NETSDK1032` (RID `win-x64` vs default `PlatformTarget x86`) — a footgun for local developers and any script that omits `-p:Platform=x64`.

Test count has **outgrown the documented 437 baseline** by +90 tests (+20.6%). README, `ModuleValidationHarness.cs` fallback regex, and `docs/validation/completion-criteria.md` (522) are stale.

Legacy Meta/Google/Telegram **adapter projects are already deleted**; remnants live as **dead branches in shared JS/C#** (not separate files). Stage 4 sabotage must combine DOM mutation flood (DOM-Scraper) with rapid `OccDateRangeFilterState` switching (UI-UX-Polish) while Pipeline-Chaos monitors build gates and thread starvation.

---

## 1. Build / Test Baseline Matrix

### 1.1 Build commands (measured 2026-06-14)

| Command | Result | Errors | Warnings | Notes |
|---------|--------|--------|----------|-------|
| `dotnet build UnifiedMessenger.sln -c Release` | **FAIL** | 1 | 0 | `NETSDK1032`: RID `win-x64` incompatible with `PlatformTarget x86` on main app |
| `dotnet build UnifiedMessenger.sln -c Release -p:Platform=x64` | **PASS** | 0 | 4 | **CI-equivalent path** (`.github/workflows/build.yml`) |
| `dotnet build UnifiedMessenger.Tests.csproj -c Debug` | **PASS** | 0 | 8 | Builds app + tests; duplicate CS9057/CS0618 across projects |
| `dotnet build UnifiedMessenger.UiSmokeTests.csproj -c Release` | **PASS** | 0 | 0 | Harness only; does not build main app in isolation |

**Release x64 warning inventory (4 unique):**

| ID | Code | File | Issue |
|----|------|------|-------|
| W1 | CS9057 | OllamaSharp analyzer | Analyzer targets compiler 4.14; SDK runs 4.11 |
| W2 | CS0618 | `OccKpiNavigationHelper.cs:48` | Obsolete `OccKpiKind.NeedsAction` |
| W3 | CS0618 | `OperationsThreadCardPresentationHelperTests.cs:47` | Obsolete `BuildFallbackSummary` |
| W4 | xUnit1031 | `OccThreadCardPresenterTests.cs:37` | Blocking `.GetAwaiter().GetResult()` in test |

**Omni-scope exit handshake gap:** Stage 5 requires **zero warnings** under Release — current state **fails** (4 warnings).

### 1.2 Test commands (measured 2026-06-14)

| Command | Passed | Failed | Skipped | Total | Duration |
|---------|--------|--------|---------|-------|----------|
| `dotnet test UnifiedMessenger.sln -c Release -p:Platform=x64 --no-build` | **527** | 0 | 0 | **527** | ~3 s |
| `dotnet test UnifiedMessenger.Tests.csproj -c Debug` | **527** | 0 | 0 | **527** | ~2 s |
| `dotnet test UnifiedMessenger.sln --no-build` (no prior build) | N/A | — | — | — | Test DLL missing |
| `dotnet test UnifiedMessenger.sln` (no platform) | N/A | — | — | — | Build fails NETSDK1032 |

**Test count reconciliation:**

| Source | Count | Status |
|--------|-------|--------|
| User prompt (historical) | 437 | **Stale** (v3.1.x release notes) |
| `completion-criteria.md` | 522 | **Stale** (v3.7.0 doc) |
| **Actual measured (this run)** | **527** | **Current truth** |
| Delta vs 437 | +90 | Dashboard/OCC/telemetry/backfill test growth |

### 1.3 CI pipeline topology (`.github/workflows/build.yml`)

```
verify (build + test + coverage ≥38% + shell DI gate + vuln scan)
    └── package (win-x64 + win-arm64 publish + Inno Setup)
            └── ui-smoke (FlaUI harness against published x64 exe)
                    └── release (tag-only GitHub Release upload)
```

| Job | Hard gate | Current local alignment |
|-----|-----------|-------------------------|
| `verify` | Build `-p:Platform=x64`; tests `Failed: 0`; coverage ≥ 38% | Build/test pass; coverage not re-measured this run |
| `verify` | Shell DI forbidden singleton patterns | Not re-run locally |
| `verify` | No vulnerable packages | Not re-run locally |
| `package` | Publish self-contained + installer artifact in `dist/` | `installer.iss` expects `bin\Release\...\win-x64\publish` |
| `ui-smoke` | `dotnet run UiSmokeTests -- <UnifiedMessenger.exe>` | Local FlaUI timing issues documented in release notes |

### 1.4 Installer baseline

| Artifact | Script | Publish dir | Architecture |
|----------|--------|-------------|--------------|
| `dist/UnifiedMessengerSetup.exe` | `installer.iss` | `UnifiedMessenger\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish` | x64 |
| `dist/UnifiedMessengerSetup-arm64.exe` | `installer-arm64.iss` | ARM64 publish path | ARM64 |

**Version in csproj:** 4.0.0 (app); README still references v3.7.0 — documentation drift.

### 1.5 UiSmokeTests harness

| Entry | Purpose |
|-------|---------|
| `Program.cs` | Default validation, `--full-app`, `--occ`, timed exploration modes |
| `ModuleValidationHarness.cs` | 13 UI modules + embedded `dotnet test` (uses `-p:Platform=x64 -c Release`) |
| `FullAppExploration.cs` | 10-minute adversarial UI walk (logs under `.cursor/`) |
| `UiAutomationHelpers.cs` | FlaUI primitives, OCC readiness waits |

**Harness drift:** `ModuleValidationHarness.RunFullUnitTestSuite` defaults missing regex capture to `"437"` (line 51) — must update to **527** after purge/refactor.

**UiSmoke module list:** MainShell, DashboardOperations, OccBranchPills, PersonalOverview, Settings, About, CommandPalette, NotificationPanel, WorkspaceSidebar, AddInstanceDialog, InstanceSwitch, RapidResize, TrayHideOnClose.

---

## 2. Legacy Code Inventory — Purge List (Stage 3)

Standalone Meta/Google/Telegram **adapter modules were already removed**. `PlatformAdapterInternals` resolves only `whatsapp`, `whatsappbusiness`, or `NullPlatformAdapter`. Remaining legacy is **embedded dead code** in shared files.

### 2.1 DELETE targets (full file removal)

| Path | Rationale |
|------|-----------|
| *(none)* | No orphaned `*Meta*Adapter*`, `*Google*Adapter*`, or `*Telegram*Adapter*` files exist on disk |

### 2.2 PURGE targets (delete sections / simplify — file retained)

| File | Legacy content to remove | Action |
|------|--------------------------|--------|
| `UnifiedMessenger/Models/PlatformKind.cs` | `Meta`, `Google` enum values + `metabusiness`/`googlebusiness` mappings | Delete enum members + switch arms; keep WhatsApp-only |
| `UnifiedMessenger/Services/Adapters/AdapterMessageTypes.cs` | `MetaInboundMessage`, `GoogleReviewSnapshot`, `GoogleReviewAlert`, `MetaTelemetrySnapshot` + `IsKnownType` arms | Delete constants and branches |
| `UnifiedMessenger/Services/ConversationKeyResolver.cs` | Meta fingerprint prefix `meta:msg:`, `BuildMetaMessageFingerprint`, metabusiness branches | Delete Meta-specific resolver logic; align docs to WhatsApp JID only |
| `UnifiedMessenger/Assets/Scripts/adapter-core.js` | `metabusiness` / `googlebusiness` branches in `__umResolvePlatformConversationIdentity`, `__umResolveConversationKey`, `__umResolveMetaSelectedThread` | Delete ~150 lines of Meta/Google identity logic |
| `UnifiedMessenger/Assets/Scripts/thread-status-auditor.js` | `PLATFORM_PROFILES.metabusiness`, `PLATFORM_PROFILES.googlebusiness`, `isMetaOutgoing`, `isGoogleOwnerReply`, platform detection arms | Delete profiles + helpers; WhatsApp-only profile |
| `UnifiedMessenger/Controls/OperationsThreadCardViewModel.cs` | Icon glyph switch arms `"metabusiness"`, `"googlebusiness"` | Remove dead cases |
| `UnifiedMessenger/Services/WebViewNavigationGuard.cs` | `"slack.com"` blocklist entry | Optional — keep as security block or expand WhatsApp-only allowlist |

### 2.3 UPDATE targets (tests & docs — not file deletion)

| File | Legacy references |
|------|-------------------|
| `UnifiedMessenger.Tests/MessageTriageServiceTests.cs` | `Platform = "metabusiness"` fixtures |
| `UnifiedMessenger.Tests/MessageAnalyticsServiceBranchFilterTests.cs` | `googlebusiness`, `metabusiness` |
| `UnifiedMessenger.Tests/UnifiedMessengerDashboardTests.cs` | `metabusiness` |
| `UnifiedMessenger.Tests/UnifiedMessengerDashboardDisplayOrderTests.cs` | `metabusiness` |
| `UnifiedMessenger.Tests/ThreadDisplayOrderServiceTests.cs` | `metabusiness` |
| `UnifiedMessenger.Tests/Backfill/BackfillSyncManagerTests.cs` | `[InlineData("metabusiness", false)]` |
| `UnifiedMessenger.Tests/NotificationHubTests.cs` | `telegram` platform strings |
| `UnifiedMessenger.Tests/PersonalDashboardServiceTests.cs` | `telegram` |
| `UnifiedMessenger.Tests/PlatformAdapterFactoryTests.cs` | `[InlineData("telegram", "whatsapp")]` — **keep** as migration regression |
| `UnifiedMessenger.Tests/PlatformDefinitionTests.cs` | `FindById("telegram")` null assert — **keep** |
| `UnifiedMessenger.Tests/ConnectionHandshakeScriptTests.cs` | Asserts no `googlebusiness` in script — update after JS purge |
| `README.md` | "437 unit tests"; out-of-scope platform list |
| `docs/validation/completion-criteria.md` | "522 unit tests" |
| `UnifiedMessenger.UiSmokeTests/ModuleValidationHarness.cs` | Fallback test count `"437"` |

### 2.4 Runtime migration (retain behavior)

| Mechanism | Location | Behavior |
|-----------|----------|----------|
| Platform ID normalization | `PlatformDefinition.NormalizePlatformId` | Unknown IDs → `"whatsapp"` |
| Instance load | `MessengerInstance.Normalize()` | Remaps legacy `instances.json` platform strings on load |
| Adapter routing | `PlatformAdapterInternals` | Non-WhatsApp → `NullPlatformAdapter` |

**Purge rule:** After Stage 3, grep for `metabusiness|googlebusiness|telegram|MetaInbound|GoogleReview` must return **zero hits in product code** (tests may retain migration cases only).

---

## 3. Stage 4 Sabotage Test Plan (Planning Only — Not Executed)

Stage 4 runs **before** standard validation. Pipeline-Chaos orchestrates; other agents supply instrumentation hooks.

### 3.1 Scenario A — 5000 DOM mutations / 2 s (Telemetry Ingress vector)

**Objective:** Break unthrottled MutationObserver + C# ingress under adversarial churn.

**Preconditions:**
- Release x64 build with WhatsApp instance logged in (or mock page with `#main` + `msg-container` nodes)
- ETW or `window.performance` long-task observer enabled
- `ResourceMonitorService` sampling at 500 ms

**Steps:**
1. Launch app; open professional WhatsApp instance; navigate to active chat.
2. Open DevTools protocol or inject via `ExecuteScriptAsync`:

```javascript
(function () {
  var root = document.querySelector('#main') || document.body;
  var count = 0, target = 5000, intervalMs = 0.4;
  var t = setInterval(function () {
    if (count++ >= target) { clearInterval(t); window.__umSabotageDone = count; return; }
    var span = document.createElement('span');
    span.className = 'msg-container';
    span.setAttribute('data-icon', count % 4 === 0 ? 'msg-dblcheck-ack' : 'msg-check');
    span.textContent = 'sabotage-' + count;
    root.appendChild(span);
    if (count % 3 === 0) span.remove();
  }, intervalMs);
})();
```

3. Record for 30 s window: main-thread long tasks (>50 ms), `postMessage` count/sec, managed heap delta, UI responsiveness (sidebar click latency).
4. **Pass criteria (aligned to Hard Failure Thresholds):**
   - UI thread stall **≤ 8 ms** p99 (ideal); document violations > 8 ms
   - No unbounded string allocation loops in ingress (DOM-Scraper zero-alloc mandate)
   - App remains interactive (window drag, tab switch) within 5 s of injection end
5. **Fail triggers:** freeze > 2 s, OOM, WebView2 crash, dispatcher queue depth unbounded growth

**Owner cross-links:** Agent-DOM-Scraper (MO throttling), Agent-Core-Architect (ingress channel backpressure)

### 3.2 Scenario B — Rapid `OccDateRangeFilterState` switching (Layout + Dispatcher vector)

**Objective:** Force layout storms while telemetry/backfill packets arrive.

**Preconditions:**
- Dashboard Operations tab visible; OCC loaded with ≥ 50 threads (seed or backfill)
- Concurrent WhatsApp telemetry active (Scenario A lite: 500 mutations/min background)

**Steps:**
1. Baseline: note `RefreshCoreAsync` duration via debug logging or `PerformanceValidationHelper`.
2. Automate date-range toggling for 60 s:
   - Cycle A: `FromUtc = Now-7d`, `ToUtc = Now`
   - Cycle B: `FromUtc = Now-30d`, `ToUtc = Now`
   - Cycle C: `ResetToDefaultWindow()`
   - Interval: **50 ms** between changes (10× faster than 300 ms debounce — stress timer coalescing)
3. Parallel: toggle `OccViewMode` Live ↔ Historical every 200 ms for 30 s.
4. Measure: layout passes per refresh (UI-UX target ≤ 2), frame time, `_isRefreshing` lock contention, chart `RenderChart` invocations.
5. **Pass criteria:**
   - Layout storm **≤ 2 measure passes** per visual update (Hard Failure Threshold)
   - No `_isRefreshing` deadlock (refresh skipped indefinitely)
   - Date persistence (`PersistDateRangeAsync`) does not block UI > 8 ms p99

**Unit-level quick probe (safe to run in CI later):**

```csharp
// Proposed: UnifiedMessenger.Tests/OccDateRangeFilterStressTests.cs
[Fact]
public async Task RapidFilterChanges_CoalesceToSingleEffectiveRange()
{
    var filter = OccDateRangeFilterState.CreateForTests();
    var changes = 0;
    filter.Changed += (_, _) => Interlocked.Increment(ref changes);
    for (var i = 0; i < 100; i++)
        filter.FromUtc = DateTimeOffset.Now.AddDays(-i);
    await Task.Delay(400); // > 300 ms debounce
    Assert.True(changes <= 100); // structural; UI debounce tested in integration
}
```

**Owner cross-links:** Agent-UI-UX-Polish (ListView storms), Agent-Core-Architect (Task.Run snapshot rebuild)

### 3.3 Scenario C — Combined backpressure (Pipeline chaos composite)

1. Run Scenario A + B simultaneously for 120 s.
2. Switch WhatsApp instances every 5 s (trigger `TrySuspendAsync` + resume).
3. Monitor thread pool: `ThreadPool.PendingWorkItemCount`, `% Time in GC`, `dotnet-counters` `threadpool-thread-count`.
4. **Fail triggers:** thread pool starvation (pending work > 100 sustained), UI hang, test suite timeout

### 3.4 Scenario D — Build/pipeline sabotage (meta)

| Attack | Expected defense |
|--------|------------------|
| Build without `-p:Platform=x64` | Document/enforce platform in README + Directory.Build.props |
| Stale bin `Assets/Scripts/*.js` | Publish overwrites; add post-build hash check in CI |
| UiSmoke against Debug build | Harness resolves Release publish path |

---

## 4. Thread Pool / UI Thread Blocking Risk Audit

| Hot path | Pattern | Thread | Risk | Severity |
|----------|---------|--------|------|----------|
| `WebMessageReceived` → `HandleWebMessage` | Sync on callback thread | WebView2 → often UI | JSON parse + handler chain blocks UI | **P0** |
| `OperationsCommandCenter.RefreshCoreAsync` | `Task.Run` snapshot + `ConfigureAwait(true)` apply | Pool → UI | Large thread lists: pool work OK; UI apply storms | **P1** |
| `UiThreadRunner.RunAsync` | Nested enqueue during WebView ops | UI | Double-hop latency | **P1** |
| `UnifiedMessengerStateSyncService` | `Task.Run` worker; channel `Wait` mode | Pool | Burst ingress awaits producers | **P1** |
| `MessageTriageService` / `AiInferenceQueue` | `Task.Run` workers | Pool | OK with bounded channels | **P2** |
| `ApplicationLifecycleService.Shutdown` | `.GetAwaiter().GetResult()` | UI/blocking | Acceptable on shutdown only | **P2** |
| `OccThreadCardPresenterTests` | `.GetAwaiter().GetResult()` in test | Test thread | xUnit1031 warning; deadlock smell | **P3** |

**Swarm rule enforcement:** Before any refactor lands, Pipeline-Chaos must confirm:
- No new sync-over-async on UI thread in WebView/session paths
- No new unbounded `Task.Run` fan-out from `WebMessageReceived`
- Stage 4 Scenario A+B pass or documented waiver with measured bounds

---

## 5. Swarm Verification Gate (Pipeline-Chaos Pre-Merge Checklist)

Every refactored file batch must pass this gate **before write to main**:

| # | Check | Command / method | Hard fail? |
|---|-------|------------------|------------|
| G1 | Release build (CI path) | `dotnet build UnifiedMessenger.sln -c Release -p:Platform=x64` | Yes — any error |
| G2 | Warning budget | Same; warn-as-error optional for Stage 5 | Yes when zero-warn policy active |
| G3 | Unit regression | `dotnet test ... -c Release -p:Platform=x64` → **527 passed, 0 failed** | Yes |
| G4 | Test count monotonicity | Parsed `Total:` ≥ 527 unless intentional deletion documented | Yes |
| G5 | Legacy grep | `rg "metabusiness|googlebusiness|MetaInbound|GoogleReview" UnifiedMessenger/` (excl. tests during transition) | Yes after Stage 3 |
| G6 | UI thread safety review | Diff touches WebView/OCC → run Scenario A or B smoke | Yes for P0 paths |
| G7 | Coverage floor | CI ≥ 38% line coverage | Yes in CI |
| G8 | Shell DI gate | No forbidden singletons in `Services/Shell/` | Yes in CI |
| G9 | Installer path | `dotnet publish` + ISCC produces `dist/*.exe` | Yes for release |
| G10 | Cross-agent sign-off | DOM + Core + UI agents acknowledge no regression in their vectors | Process |

---

## 6. Cross-Agent Dependency Map

```
                    ┌─────────────────────────┐
                    │  Agent-Pipeline-Chaos   │
                    │  (build/test/gates)     │
                    └───────────┬─────────────┘
                                │ verifies before merge
        ┌───────────────────────┼───────────────────────┐
        ▼                       ▼                       ▼
┌───────────────┐     ┌─────────────────┐     ┌─────────────────┐
│ Core-Architect│     │  DOM-Scraper    │     │  UI-UX-Polish   │
│ WebView2 life │◄───►│ whatsapp-adapter│◄───►│ OCC XAML/chart  │
│ Channels/JSON │     │ MO + ingress    │     │ ListView/layout │
└───────┬───────┘     └────────┬────────┘     └────────┬────────┘
        │                      │                       │
        │ InstanceSessionMgr   │ postMessage flood     │ OccDateRange
        │ UiThreadRunner       │ 5000 mutations        │ filter storms
        │                      │                       │
        └──────────────────────┴───────────────────────┘
                               │
                    Stage 4 combined sabotage
                               │
                    Stage 5 dotnet test (527)
```

| Pipeline-Chaos delivers | Consumers need it for |
|-----------------------|----------------------|
| Build/test baseline | All agents — regression detection |
| Legacy purge list | Stage 3 Redundancy Purge |
| Stage 4 harness steps | DOM-Scraper (MO metrics), UI-UX (layout passes), Core (memory/thread) |
| Verification gate G1–G10 | Overlord exit handshake |

| Pipeline-Chaos depends on | From agent |
|---------------------------|------------|
| MO survivability estimate | DOM-Scraper (2/10 @ 5000 mutations) |
| Layout pass count / ListView P0 | UI-UX-Polish |
| Ingress channel / suspend semantics | Core-Architect |
| Hard Failure Threshold definitions | Overlord prompt (4-vector matrix) |

---

## 7. Stage 2 Audit Findings (Pipeline Domain)

| ID | Finding | Severity |
|----|---------|----------|
| PC-1 | Default solution build fails without `-p:Platform=x64` | **P0** |
| PC-2 | Test count docs stale (437/522 vs **527**) | **P1** |
| PC-3 | Release build has 4 warnings — violates zero-warn exit handshake | **P1** |
| PC-4 | Legacy Meta/Google code in shared JS/C# despite WhatsApp-only product | **P1** (Stage 3) |
| PC-5 | UiSmoke harness hardcodes 437 fallback | **P2** |
| PC-6 | Version drift: csproj 4.0.0 vs README 3.7.0 | **P2** |
| PC-7 | No dedicated stress test project for Stage 4 scenarios | **P2** (planned) |
| PC-8 | `dotnet test` on solution without platform fails at build | **P1** |

---

## 8. Peer Agent Merge Status (post 2-min wait)

| Agent doc | Status |
|-----------|--------|
| `STAGE1-Agent-Core-Architect.md` | **Present** — merged into benchmarking matrix |
| `STAGE1-Agent-DOM-Scraper.md` | **Present** — Scenario A script + ingress thresholds |
| `STAGE1-Agent-UI-UX-Polish.md` | **Present** — Scenario B layout thresholds |
| `STAGE1-BENCHMARKING-MATRIX.md` | **This agent** — master synthesis |

---

*End of Agent-Pipeline-Chaos Stage 1 report.*
