# STAGE 4 — Sabotage Stress Test Results

**Agent:** Pipeline-Chaos (integrator)  
**Date:** 2026-06-14  
**Build under test:** v4.1.0 Release x64

---

## Automated probes (CI-safe)

| Test | File | Result | Notes |
|------|------|--------|-------|
| Ingress badge burst (500 msgs) | `IngressBurstStressTests.BadgeCountBurst_500Messages_LatestCountWinsWithoutThrow` | **PASS** | 500 synchronous `HandleWebMessage` calls in ~&lt;5 s; latest count wins (49). No coalescing channel — every message parsed. |
| Telemetry burst (200 msgs) | `IngressBurstStressTests.TelemetryBurst_200Messages_HandlerAcceptsAllWithoutThrow` | **PASS** | All 200 handled; analytics counts unchanged (telemetry path). |
| OccDateRange rapid switch (10×50 ms cycles) | `OccDateRangeFilterStressTests.RapidFilterChanges_10CyclesAt50Ms_ProducesConsistentFinalRange` | **PASS** | Final range = 7-day default; `Changed` fires on each mutation (no debounce at state layer). |
| OccDateRange FromUtc-only flood | `OccDateRangeFilterStressTests.RapidFromUtcOnlyChanges_DoesNotThrowOrDeadlock` | **PASS** | 100 rapid assignments; no throw/deadlock. |

**New test count:** +3 automated sabotage tests (530 total solution tests).

---

## Hard failure thresholds (honest assessment)

| Vector | Threshold | Measured / inferred | Gate |
|--------|-----------|---------------------|------|
| **Telemetry ingress** | Zero loop string alloc; coalesced ingress | Sync JSON parse per message; no C# channel coalescer | **FAIL (RED)** — burst test passes functionally but architecture still uncoalesced |
| **UI dispatcher** | UI stall ≤ 8 ms p99 | Not instrumented in CI; burst runs on test thread not WebView UI | **UNKNOWN (AMBER)** — no ETW/long-task capture this run |
| **WebView2 memory** | ≤ 15 MB leakage post-suspend | Not measured in automated run | **UNKNOWN (AMBER)** |
| **Layout measurement** | ≤ 2 measure passes per update | OccDateRange state test does not exercise WinUI layout; Kanban migrated to ItemsRepeater (partial UI agent work) | **UNKNOWN (AMBER)** — manual OCC required |

---

## Manual Scenario A — `__umStressTestDomFlood` (DOM flood)

**Hook present:** `whatsapp-adapter.js` exports `window.__umStressTestDomFlood(count)` (dev-only unless `window.__umStressTestEnabled`).

### Steps

1. Build/install v4.1.0; open a logged-in WhatsApp instance with `#main` visible.
2. Enable dev mode: in WebView devtools console run `window.__umStressTestEnabled = true`.
3. Execute: `window.__umStressTestDomFlood(5000)` — spreads ~5000 mutations over ~2 s.
4. For 30 s after flood, observe: sidebar click latency, tab switch, `% Time in GC`, managed heap.
5. Optional: compare `postMessage` rate via Performance tab long tasks (&gt;50 ms).

### Expected pass criteria (Overlord)

- UI thread stall ≤ 8 ms p99
- App interactive within 5 s of flood end
- No WebView2 crash / OOM

### Expected outcome (Stage 1 estimate)

**LIKELY FAIL (RED)** — DOM-Scraper Stage 1 rated survivability **2/10** at 5000 mut/2 s due to unthrottled MutationObservers. Manual run not executed in integrator window (requires live WhatsApp session).

---

## Manual Scenario B — OccDateRange @ 50 ms + Live/Historical

Automated state-layer probe **PASS**; full WinUI debounce (300 ms in `OperationsCommandCenter.DateRange.partial.cs`) not stress-tested at 50 ms in FlaUI this run.

**Manual steps:** Dashboard Operations visible → cycle date pickers / Clear every 50 ms for 60 s while telemetry active → count layout passes (debug overlay) and frame drops.

**Expected:** Layout &gt; 2 passes under load (**LIKELY FAIL RED** per UI-UX Stage 1).

---

## Scenario C (composite)

Not executed — depends on Scenarios A+B + instance switching. Document as **PENDING**.

---

## Integration notes (parallel agents)

- **Core-Architect:** `PlatformNavigationHooks` + ingress path changes merged; fixed `Func<Task>` dispatch compile break.
- **UI-UX-Polish:** Kanban `ItemsRepeater` migration landed; `ColumnOrderChanged` wired via drop handler.
- **DOM-Scraper:** `__umStressTestDomFlood` present; MO throttling not verified green.
- **Legacy purge:** Partial — `PlatformKind` Meta/Google removed in working tree; grep may still find remnants in JS until DOM agent completes.

---

*Stage 4 automated probes green; hard vector gates remain RED/AMBER until manual instrumentation confirms otherwise.*
