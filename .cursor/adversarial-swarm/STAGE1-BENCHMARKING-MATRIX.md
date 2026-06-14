# STAGE 1 — Master Benchmarking Matrix (4-Vector Synthesis)

**Adversarial Swarm — Unified Messenger**  
**Date:** 2026-06-14  
**Synthesized by:** Agent-Pipeline-Chaos (with merge from Core-Architect, DOM-Scraper, UI-UX-Polish)  
**Scope:** Assessment only — no code changes

---

## Hard Failure Thresholds (Overlord Mandate)

From the Adversarial Swarm Overlord prompt — **any refactor that violates these thresholds fails Stage 4/5 and must not merge.**

| Target Vector | Metric Component | Performance Bound | Hard Failure Threshold |
|---------------|------------------|-------------------|------------------------|
| **Telemetry Ingress** | Allocation overhead | Zero-allocation O(1) heap churn in hot loops | **Any string instantiation inside looping tracks** |
| **UI Dispatcher** | Frame render delta | 0 ms frame drops (60/120 Hz locked) | **UI thread stall > 8 ms** |
| **WebView2 Memory** | Subprocess footprint | `MemoryUsageTargetLevel.Low` upon focus loss | **RAM leakage > 15 MB post-suspend** |
| **Layout Measurement** | Multi-pass measure | Single-pass evaluation per visual tree update | **Layout storm looping > 2 passes** |

**Stage 5 additional gates (Pipeline-Chaos measured):**

| Gate | Bound | Hard Failure |
|------|-------|--------------|
| Release build | 0 errors | Any error |
| Release warnings | 0 (exit handshake) | **4 warnings today** → FAIL |
| Unit tests | 0 failed | Any failure |
| Test count | ≥ 527 (current) | Regression without approval |

---

## Consolidated 4-Vector Matrix

### Vector 1 — Telemetry Ingress (Agent-DOM-Scraper + Agent-Core-Architect)

| Metric | Instrumentation | Current Measured / Inferred | Hard Threshold | Gap | Primary Owner |
|--------|-----------------|------------------------------|----------------|-----|---------------|
| MO callback fan-out | Count observers per instance | **5 observers** on `#main` subtree | 1 scoped MO + scheduler | **FAIL** — 5× redundancy | DOM-Scraper |
| Sync DOM work per MO batch | Long tasks > 50 ms | **2–4 full scrapes** unthrottled | 0 sync work in MO callback | **FAIL** | DOM-Scraper |
| `postMessage` rate under 5000 mut/2s | Hook `postMessage` | Unbounded; debounce 250–300 ms only on subset | ≤ 4 msg/s per type | **FAIL** | DOM-Scraper |
| JS string alloc per publish | V8 heap snapshot | ~15–40 strings/publish | ≤ 2 strings | **FAIL** | DOM-Scraper |
| C# JSON parse path | Alloc profiler | `JsonDocument.Parse` every message; possible double-parse | Source-gen / span; zero loop strings | **FAIL** | Core-Architect |
| Ingress coalescing | Queue depth | None — sync `HandleWebMessage` | Channel + coalescer | **FAIL** | Core-Architect |
| Survivability @ 5000 mut/2s | Stage 4 Scenario A | **2/10** (DOM-Scraper estimate) | Interactive app within 5 s post-burst | **FAIL** | Pipeline-Chaos |

**Pipeline baseline (build/test):** Ingress code builds in Release x64; **527** unit tests cover `WebMessageParser`, `WhatsAppIngressHandler`, adapter scripts — all green.

---

### Vector 2 — UI Dispatcher (Agent-UI-UX-Polish + Agent-Core-Architect + Pipeline-Chaos)

| Metric | Instrumentation | Current Measured / Inferred | Hard Threshold | Gap | Primary Owner |
|--------|-----------------|------------------------------|----------------|-----|---------------|
| UI stall p99 | `DispatcherQueue` latency / ETW | WebView `HandleWebMessage` on UI path; nested `UiThreadRunner` hops | **≤ 8 ms** | **UNKNOWN** — likely FAIL under Scenario A | Core-Architect |
| Frame drops during OCC refresh | Frame timing API | Full snapshot apply: 5 collection paths + chart render | 0 drops | **LIKELY FAIL** under load | UI-UX-Polish |
| `RefreshCoreAsync` re-entrancy | `_isRefreshing` lock | Skips overlapping refresh | No deadlock | **PASS** (structural) | UI-UX-Polish |
| Date-range debounce | Timer 300 ms | Coalesces picker changes | ≤ 8 ms UI work per tick | **PASS** (design); untested at 50 ms sabotage | Pipeline-Chaos |
| OccDateRange + telemetry composite | Stage 4 Scenario B | Not run | Stall ≤ 8 ms | **PENDING** | Pipeline-Chaos |
| Thread pool pending work | `ThreadPool.PendingWorkItemCount` | Multiple `Task.Run` workers (sync, triage, AI) | No starvation | **UNKNOWN** | Core-Architect |

**UiSmoke signal:** FlaUI runs report intermittent UIA failures (WinUI load timing) — not a dispatcher metric but affects Stage 5 UI validation confidence.

---

### Vector 3 — WebView2 Memory (Agent-Core-Architect)

| Metric | Instrumentation | Current Measured / Inferred | Hard Threshold | Gap | Primary Owner |
|--------|-----------------|------------------------------|----------------|-----|---------------|
| Working set per background WebView | Process Explorer / `ResourceMonitorService` | Default env; no browser memory flags | Low tier on focus loss | **PARTIAL** | Core-Architect |
| Post-suspend RAM delta | Measure before/after `TrySuspendAsync` | Mixed **TrySuspend + MemoryUsageTargetLevel.Low** (Microsoft: pick one) | **≤ 15 MB** leakage | **UNKNOWN** — API mixing risk | Core-Architect |
| Active session cap | `MaxConcurrentWebViews` default 6 | LRU eviction implemented | Cap enforced | **PASS** (design) | Core-Architect |
| Orphaned WinRT handlers | Handler audit at dispose | `NavigationCompleted` lambda not detached | Zero orphans | **FAIL** | Core-Architect |
| UDF isolation | Profile per instance | Shared UDF + unique `ProfileName` | Isolated cookies/storage | **PASS** | Core-Architect |
| V8 heap under mutation storm | Scenario A | +5–30 MB estimated (DOM-Scraper) | No runaway growth | **LIKELY FAIL** | DOM-Scraper |

---

### Vector 4 — Layout Measurement (Agent-UI-UX-Polish)

| Metric | Instrumentation | Current Measured / Inferred | Hard Threshold | Gap | Primary Owner |
|--------|-----------------|------------------------------|----------------|-----|---------------|
| ListView virtualization | Item count vs viewport | **4× ListView** in outer `ScrollViewer`; scroll disabled on lists | Virtualized repeater | **FAIL** — O(n) measure | UI-UX-Polish |
| Passes per OCC refresh | Layout debug / count | **Up to 4 ListView resets** + chart geometry | **≤ 2 passes** | **FAIL** | UI-UX-Polish |
| Chart `SizeChanged` storm | `RenderChart` call count | Full `PathGeometry` parse on UI thread each resize | Background build + debounce | **FAIL** | UI-UX-Polish |
| High-DPI clipping | 100–200% scaling test | Hard widths KPI 180px; kanban 2/3 columns UIA | Zero clip | **FAIL** (P0-2 kanban) | UI-UX-Polish |
| OccDateRange refresh cost | Time `ApplySnapshot` | Full rebuild on debounced timer | Single-pass batch | **FAIL** under rapid filter | UI-UX-Polish + Pipeline |

**Layout storm signature (documented):**  
`RefreshCoreAsync` → `ApplyKanban` + `ApplyWorkQueue` + `ApplyImmediateQueue` + KPI + chart = **> 2 measure passes** per snapshot.

---

## Build / Test / Pipeline Baseline (Pipeline-Chaos)

| Metric | Current (2026-06-14) | Target (Stage 5) | Gap |
|--------|----------------------|------------------|-----|
| `dotnet build -c Release` (no platform) | **1 error** NETSDK1032 | 0 errors | **FAIL** — must use `-p:Platform=x64` |
| `dotnet build -c Release -p:Platform=x64` | 0 errors, **4 warnings** | 0 warnings | **FAIL** |
| `dotnet test -c Release -p:Platform=x64` | **527 / 527 pass** | 527+ pass, 0 fail | **PASS** |
| Documented test count | README: 437; criteria: 522 | Match actual 527 | **STALE** |
| CI coverage floor | 38% enforced | ≥ 38% | **PASS** (CI) |
| Installer artifacts | `dist/UnifiedMessengerSetup.exe` | Present on release | **PASS** (local binary in git status) |
| Legacy platform files on disk | 0 adapter files; embedded JS/C# remnants | Zero grep hits | **PARTIAL** |

---

## Gap Analysis Summary

| Vector | Overall vs Hard Thresholds | Top blocker | Stage 3/4 action |
|--------|----------------------------|-------------|------------------|
| **Telemetry Ingress** | **RED** | Unthrottled MO + sync JSON ingress | MO consolidation; source-gen JSON; Scenario A |
| **UI Dispatcher** | **RED/AMBER** | WebView messages on UI thread | Channel ingress; measure 8 ms gate |
| **WebView2 Memory** | **AMBER** | Mixed suspend/Low API; orphan handlers | Pick one memory strategy; fix dispose |
| **Layout Measurement** | **RED** | Non-virtualized ListViews | ItemsRepeater migration; Scenario B |
| **Pipeline** | **AMBER** | 4 warnings; build footgun; stale docs | Fix Platform default; update counts |

**Exit handshake status (Overlord):**

| Criterion | Status |
|-----------|--------|
| 1. Zero warnings + zero errors Release | ❌ 4 warnings; naive build 1 error |
| 2. No dead legacy multi-platform code | ❌ Embedded Meta/Google in JS/C# |
| 3. Leak-proof WebView2 lifecycles | ⚠️ Partial — handler detach gaps |

---

## Stage 4 Sabotage Execution Order (Cross-Agent)

```
1. Pipeline-Chaos: verify G1–G3 green (527 tests, Release x64 build)
2. DOM-Scraper instrumentation: postMessage counter, long-task observer
3. Scenario A: 5000 DOM mutations / 2 s
4. UI-UX-Polish instrumentation: layout pass counter (debug overlay)
5. Scenario B: OccDateRangeFilterState @ 50 ms + Live/Historical toggle
6. Core-Architect: RAM sample pre/post suspend during A+B
7. Scenario C: A + B + instance switch (120 s)
8. Pipeline-Chaos: record pass/fail vs Hard Failure Thresholds
9. If any RED → block Stage 5 merge; return to Stage 3 refactor
```

---

## Agent Document Index

| Document | Agent | Status |
|----------|-------|--------|
| `STAGE1-Agent-Core-Architect.md` | Core-Architect | ✅ Merged |
| `STAGE1-Agent-DOM-Scraper.md` | DOM-Scraper | ✅ Merged |
| `STAGE1-Agent-UI-UX-Polish.md` | UI-UX-Polish | ✅ Merged |
| `STAGE1-Agent-Pipeline-Chaos.md` | Pipeline-Chaos | ✅ Authoritative for build/test/purge/stress |
| `STAGE1-BENCHMARKING-MATRIX.md` | Pipeline-Chaos | ✅ This file |

**Peer merge note:** All four agent Stage 1 documents were present after the 2-minute wait (Core-Architect completed during synthesis window).

---

## Recommended Immediate Actions (Stage 3+ — not executed in Stage 1)

1. Add `Directory.Build.props` or document mandatory `-p:Platform=x64` to eliminate NETSDK1032.
2. Update README, `completion-criteria.md`, `ModuleValidationHarness.cs` to **527** tests.
3. Execute legacy purge list from Pipeline-Chaos §2.2.
4. Resolve 4 Release warnings (CS9057, CS0618×2, xUnit1031).
5. Implement Stage 4 harness as `UnifiedMessenger.Tests` integration fixtures or dedicated stress project.
6. Re-run this matrix after Stage 3 refactors — target all vectors **GREEN**.

---

*Master synthesis complete. No product code was modified during Stage 1.*
