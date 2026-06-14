# STAGE 1 — Agent-Core-Architect Assessment

**Domain:** C# async boundaries, Cs/WinRT native reference lifecycles, Win32 interop pinning, `InstanceSessionManager`, WebView2 lifecycle, UDF isolation, disposal order, `System.Threading.Channels` for metrics, source-generated `System.Text.Json`, `ReadOnlySpan<char>` parsing opportunities.

**Scope:** Stage 1 (2026 Web Research) + Stage 2 (Local Codebase Audit) only. No product code changes.

**Date:** 2026-06-14

---

## Executive Summary

Unified Messenger has a **sound architectural skeleton** for WebView2 profile sharing (single UDF + shared `CoreWebView2Environment`, per-instance `ProfileName`) and explicit UI-thread marshaling via `UiThreadRunner` / `WebViewUiAwaiter`. However, the runtime path **mixes two mutually exclusive WebView2 memory APIs** (`TrySuspendAsync`/`Resume` and `MemoryUsageTargetLevel`), leaves **WinRT event handlers attached** on navigation hooks that cannot be unsubscribed, parses every `postMessage` payload with **reflective `JsonDocument`**, and uses **Channels with inconsistent backpressure semantics** (including a helper that logs "dropped oldest" but does not drop). Legacy Meta/Google/Discord platform remnants remain in resolver/branding/navigation code despite a WhatsApp-only product gate.

---

## 1. Benchmarking Matrix (Core-Architect Vectors)

| Vector | Metric | Instrumentation Hook | Current Baseline (observed / inferred) | Target Bound (Stage 3) | Primary Files |
|--------|--------|----------------------|----------------------------------------|-------------------------|---------------|
| **Telemetry** | `postMessage` parse + dispatch latency (p50/p99) | ETW / `Stopwatch` around `HandleWebMessage`, count `JsonDocument` allocations | Every message: `JsonDocument.Parse` (+ possible double-parse for string-wrapped JSON); runs on UI thread via `WebMessageReceived` | p99 < 2 ms for badge/telemetry; zero double-parse on hot path | `PlatformAdapters.cs:158-218`, `WebMessageParser.cs:8-29`, `InstanceSessionManager.cs:658-663` |
| **Telemetry** | Ingress → registry mutation queue depth | `UnifiedMessengerStateSyncService.PendingCount`, triage `_channel.Reader.Count` | Sync channel capacity 128, `FullMode.Wait`; triage capacity 64, `DropOldest` | Sync pending ≤ 16 under burst; triage drop rate < 0.1% | `UnifiedMessengerStateSyncService.cs:33-44`, `MessageTriageService.cs:19-25` |
| **Telemetry** | JSON allocations per ingress event | `dotnet-counters` `System.Runtime` alloc rate during sidebar snapshot flood | Reflection `JsonElement` traversal; no `JsonSerializerContext` anywhere in repo | ≥ 50% alloc reduction on ingress hot path | `WebMessageParser.cs`, `WhatsAppIngressHandler.cs:146-171` |
| **Dispatcher** | UI-thread queue depth during WebView2 ops | `DispatcherQueue` enqueue latency; count nested `RunAsync` during switch | Every WebView2 await: `ConfigureAwait(false)` then `YieldToUiAsync()` — **two thread hops per operation** | Single hop for serial WebView2 work; p99 switch < 150 ms (6 instances) | `WebViewUiAwaiter.cs:10-21`, `UiThreadRunner.cs:26-63`, `InstanceSessionManager.cs:132-194` |
| **Dispatcher** | WinRT event handler count per live WebView | Native ref dump / handler detach audit at session dispose | `WebMessageReceived` detached ✓; `NavigationCompleted` anonymous lambda never detached ✗ | Zero orphaned handlers after `CloseSessionAsync` | `InstanceSessionManager.cs:594-602`, `PlatformAdapters.cs:221-234` |
| **WebView2 Memory** | Working set per active WebView (MB) | `ResourceMonitorService.Capture` + Process Explorer `msedgewebview2` tree | Default env (no browser flags); background uses **both** suspend + `MemoryUsageTargetLevel.Low` | ≤ 120 MB per background instance (Low tier only); ≤ 250 MB foreground | `WebViewProfileManager.cs:55-59`, `InstanceSessionManager.cs:154-161`, `605-629` |
| **WebView2 Memory** | Live WebView count vs cap | `InstanceSessionManager.ActiveSessionCount`, `MaxConcurrentWebViews` | Default cap 6 (`AppSettingsService`); LRU eviction closes non-visible sessions | Cap enforced with ≤ 1 eviction per ensure; no cap bypass via `WarmAll` | `InstanceSessionManager.cs:429-448`, `AppSettingsService.cs:154-155` |
| **WebView2 Memory** | Suspend/resume success rate | Log `TrySuspendAsync` HRESULT / catch rate | Suspend on switch, hide, and pre-close dispose; resume only on switch-to | 100% resume within 500 ms; no mixed-mode with MemoryUsageTarget | `InstanceSessionManager.cs:356-401`, `557-582` |
| **Layout** | Dashboard rebuild cost on `ThreadRegistryService.Changed` | Time `BuildSnapshot` / `BuildThreadMetricsOnly` | Full sort + multiple LINQ passes; sorted cache invalidated on **every** upsert | Incremental snapshot or coalesced refresh ≤ 16 ms for 500 threads | `ThreadRegistryService.cs:604-608`, `UnifiedMessengerDashboardService.cs:29-61`, `DashboardRefreshCoordinator.cs:10-115` |
| **Layout** | Thread registry read amplification | Count `GetAllThreads()` calls per refresh cycle | Cached sort rebuilt on any `NotifyChanged`; dashboard calls `RefreshOperationalFlags` then full read | O(1) delta notifications; stable sort cache with version stamp | `ThreadRegistryService.cs:25-39`, `447-487` |

---

## 2. Current State vs Target Bounds (with Citations)

### 2.1 WebView2 Environment & UDF Isolation

| Aspect | Current | Target | Citation |
|--------|---------|--------|----------|
| Shared UDF | Single folder under `ApplicationPaths.UserDataRoot/WebView2` | Keep — correct for memory pooling | `WebViewProfileManager.cs:22`, `14-15` |
| Profile isolation | `CreateCoreWebView2ControllerOptions().ProfileName = profileName` | Keep | `WebViewProfileManager.cs:112-113` |
| Environment options | `options: null` — no `AdditionalBrowserArguments` | Set V8 scavenger / memory flags at env creation | `WebViewProfileManager.cs:55-59` |
| Profile ownership | Registry enforces one profile per instance | Keep | `InstanceWebViewRegistry.cs:79-90`, `InstanceSessionManager.cs:218-222` |

### 2.2 Memory Management (TrySuspend vs MemoryUsageTargetLevel)

Microsoft's WebView2 spec states apps should use **either** `TrySuspend`/`Resume` **or** `MemoryUsageTargetLevel` Low/Normal — **not both**, because `TrySuspend` auto-sets target level to Low and `Resume` restores Normal.

| Behavior | Current | Target | Citation |
|----------|---------|--------|----------|
| Background visual state | Collapsed + `MemoryUsageTargetLevel.Low` (except High tier) | If using suspend: skip explicit Low; if keeping scripts alive: Low only, no suspend | `InstanceSessionManager.cs:605-629` |
| On instance switch away | `TrySuspendSessionAsync` **and** prior `SetSessionVisualState(..., false)` already set Low | Pick one strategy per settings profile | `InstanceSessionManager.cs:149-161`, `356-378` |
| On hide / app inactive | Suspend + Low via `ApplyAppWindowState` | Consistent with chosen strategy | `InstanceSessionManager.cs:474-487`, `246-254` |
| Pre-close dispose | Suspend then `Close()`; message handler detached | Add navigation hook detach; verify WinRT ref release | `InstanceSessionManager.cs:557-582`, `594-602` |
| Aggressive unload setting | `EnablePerInstanceSleepUnload` → full `CloseSessionAsync` on switch | Keep as opt-in "hard" mode | `InstanceSessionManager.cs:154-157` |

### 2.3 Async / Dispatcher Boundaries

| Aspect | Current | Target | Citation |
|--------|---------|--------|----------|
| WebView2 API thread affinity | `WebViewUiAwaiter` re-marshals to UI after every WinRT task | Batch UI work; avoid double-hop on read-only ops | `WebViewUiAwaiter.cs:10-21` |
| Public session API | All entry points via `UiThreadRunner.RunAsync` | Keep — correct for WinUI | `InstanceSessionManager.cs:75`, `129-130`, `196-197` |
| `ConfigureAwait` usage | `ConfigureAwait(true)` inside UI runner; `false` in WebView awaiter | Document contract; audit for pool-thread XAML touch | `UiThreadRunner.cs:40-62`, `InstanceSessionManager.cs:114` |
| Fire-and-forget sync enqueue | `_ = EnqueueAsync(...)` from WebMessage path | `TryWrite` or bounded drop; avoid unbounded `Task` fan-out when channel full | `UnifiedMessengerStateSyncService.cs:86-103` |

### 2.4 Channels & Backpressure

| Channel | Capacity | FullMode | Current Risk | Citation |
|---------|----------|----------|--------------|----------|
| `UnifiedMessengerStateSyncService` | 128 | `Wait` | Producers await; each enqueue spawns async Task; can pile under burst | `UnifiedMessengerStateSyncService.cs:38-44`, `89-103` |
| `MessageTriageService` | 64 | `DropOldest` | Correct pattern for lossy ingress | `MessageTriageService.cs:19-25` |
| `AiInferenceQueue` signal | `OllamaOptions.QueueCapacity` | `DropOldest` | OK for AI backpressure | `AiInferenceQueue.cs:19-25` |
| `ChannelWriteHelper` | N/A | Claims drop, **retries TryWrite only** | Misleading telemetry; second write fails if still full | `ChannelWriteHelper.cs:7-21` |

### 2.5 JSON & Parsing

| Aspect | Current | Target | Citation |
|--------|---------|--------|----------|
| Source generation | **None** — grep finds zero `JsonSerializerContext` | `JsonSerializerContext` for adapter message DTOs | Repo-wide search |
| Ingress parse | `JsonDocument.Parse` per message; double parse if root is string | `Utf8JsonReader` / source-gen deserialize; `ReadOnlySpan<char>` for type dispatch | `WebMessageParser.cs:8-29` |
| `ReadOnlySpan<char>` | **Not used** in ingress or key resolution | Use for `type` switch, platform ID normalize, JID checks | `WhatsAppIngressHandler.cs:299-308`, `ConversationKeyResolver.cs` |
| Script template prep | `string.Replace` chain per registration | Cache prepared scripts per `(platform, settings hash)` | `PlatformAdapters.cs:613-626` |

### 2.6 Legacy Platform Remnants (Meta / Google / Telegram / Discord)

| Remnant | Status | Citation |
|---------|--------|----------|
| `PlatformKind.Meta`, `PlatformKind.Google` | Enum + mapping still present | `PlatformKind.cs:8-11`, `17-25` |
| `ConversationKeyResolver` Meta/Google rules | Active code paths for `metabusiness`, `review:` keys | `ConversationKeyResolver.cs:14-16`, `64-67`, `163`, `190` |
| `PlatformDefinition.All` | **WhatsApp only** (2 entries) | `PlatformDefinition.cs:17-35` |
| `PlatformModuleSettingsHelper` | Gates to whatsapp/whatsappbusiness only | `PlatformModuleSettingsHelper.cs:10-11` |
| `PlatformAdapterInternals` | WhatsApp adapters + `NullPlatformAdapter` fallback | `PlatformAdapterInternals.cs:7-16` |
| Discord WebView config | Dead branch — no discord platform in `PlatformDefinition` | `WebViewPlatformConfigurator.cs:34-43` |
| OAuth allowlist (Google/Facebook) | Still in navigation guard | `WebViewNavigationGuard.cs:12-21` |
| UI glyphs for meta/google | `OperationsThreadCardViewModel` | `OperationsThreadCardViewModel.cs:185-186` |
| Telegram | **No code references** in product layer | Grep clean |

Product is effectively **WhatsApp-only** at runtime, but resolver, enums, navigation allowlists, and UI affordances retain multi-platform debt.

---

## 3. Critical Flaws (P0–P2)

### P0 — Correctness / Memory Safety

| ID | Flaw | Impact | Evidence |
|----|------|--------|----------|
| **P0-1** | **Mixed WebView2 memory APIs** — `TrySuspendAsync` used alongside explicit `MemoryUsageTargetLevel.Low/Normal` | Undefined behavior per Microsoft spec; suspend/resume may fight Low/Normal toggles; unpredictable memory on resume | `InstanceSessionManager.cs:154-161`, `356-378`, `605-629`; [MemoryUsageTargetLevel spec](https://github.com/MicrosoftEdge/WebView2Feedback/blob/main/specs/MemoryUsageTargetLevel.md) |
| **P0-2** | **`NavigationCompleted` handler never unsubscribed** — anonymous lambda in `RegisterNavigationHooks` | Cs/WinRT holds delegate → `CoreWebView2` → closure over `instance`; native ref leak across session recycle | `PlatformAdapters.cs:221-234`; contrast with proper detach at `InstanceSessionManager.cs:594-602` |
| **P0-3** | **Ingress JSON parsing on UI thread** — `HandleWebMessage` → `WebMessageParser.Parse` in `WebMessageReceived` | UI jank during badge storms / sidebar snapshots; GC pressure blocks dispatcher | `InstanceSessionManager.cs:658-663`, `PlatformAdapters.cs:158-165` |

### P1 — Performance / Scalability

| ID | Flaw | Impact | Evidence |
|----|------|--------|----------|
| **P1-1** | **No environment-level memory tuning** — `CreateWithOptionsAsync(..., options: null)` | Cannot cap V8 scavenger / renderer memory across profiles sharing one browser process | `WebViewProfileManager.cs:55-59` |
| **P1-2** | **`ChannelWriteHelper` false drop semantics** | Under load, triage enqueue silently fails after log; message loss without `DropOldest` taking effect | `ChannelWriteHelper.cs:17-20` |
| **P1-3** | **`UnifiedMessengerStateSyncService` Wait + fire-and-forget Tasks** | Burst resolve events spawn many blocked Tasks; thread-pool pressure | `UnifiedMessengerStateSyncService.cs:38-44`, `86` |
| **P1-4** | **Thread registry full sort on every mutation** | Dashboard debounce (500 ms) still rebuilds O(n log n) lists frequently | `ThreadRegistryService.cs:604-608`, `DashboardRefreshCoordinator.cs:10` |
| **P1-5** | **Double thread hop per WebView2 await** | Switch latency scales with instance count | `WebViewUiAwaiter.cs:10-21` |

### P2 — Maintainability / Technical Debt

| ID | Flaw | Impact | Evidence |
|----|------|--------|----------|
| **P2-1** | **Legacy Meta/Google/Discord code paths** after WhatsApp slim-down | Confusing resolver behavior if stale platform IDs in persisted data | `PlatformKind.cs`, `ConversationKeyResolver.cs`, `WebViewPlatformConfigurator.cs` |
| **P2-2** | **No source-generated JSON** | Larger IL, slower parse, harder to trim/AOT | Repo-wide |
| **P2-3** | **Discord comment in `WebViewPlatformConfigurator`** | Misleading docs for WhatsApp-only app | `WebViewPlatformConfigurator.cs:7-8` |
| **P2-4** | **`ConditionalWeakTable` for adapter registration** without paired cleanup hook | Low risk (weak), but reinject path relies on `__umResetAdapterRuntime` script | `PlatformAdapters.cs:24`, `588-596` |

---

## 4. Refactor Strategy (Stage 3 Preview)

### DELETE

| Item | Rationale |
|------|-----------|
| Discord branch in `WebViewPlatformConfigurator` | No `discord` platform in `PlatformDefinition.All` |
| `PlatformKind.Meta` / `Google` **or** gate behind feature flag with zero runtime paths | Product is WhatsApp-only per `PlatformModuleSettingsHelper` |
| Meta/Google glyph branches in `OperationsThreadCardViewModel` if UI never shows those platforms | Dead UI |
| Misleading log line in `ChannelWriteHelper` | Replace with real `TryWrite` + `DropOldest` channel config or explicit `TryRead` loop |
| One of the two memory strategies (recommend: **drop TrySuspend** for background monitors; keep `MemoryUsageTargetLevel` + optional `EnablePerInstanceSleepUnload` for hard close) | Align with WebView2 spec for script-keeping background tabs |

### REFACTOR

| Item | Approach |
|------|----------|
| **Memory policy enum** | `WebViewMemoryStrategy { TargetLevelOnly, SuspendResume, AggressiveUnload }` wired to settings; single code path in `SetSessionVisualState` / switch |
| **Event handler lifecycle** | Store `TypedEventHandler` for `NavigationCompleted` on `SessionEntry`; detach in `DisposeSessionEntryCoreAsync` alongside `WebMessageReceived` |
| **Ingress pipeline** | `WebMessageReceived` → `Channel<ReadOnlyMemory<byte>>` (bounded, drop-oldest) → background parse → UI dispatch for mutations only |
| **JSON** | Add `[JsonSerializable]` context for adapter envelopes (`type`, `instanceId`, common fields); keep `JsonElement` escape hatch for forward compat |
| **`ReadOnlySpan<char>` hot paths** | Type dispatch (`"badge"`, `"whatsappTelemetry"`), platform normalize, delivery status normalize without alloc |
| **Environment creation** | `CoreWebView2EnvironmentOptions` with documented flags (e.g. `scavenger_max_new_space_capacity_mb` per [WebView2 browser flags](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/webview-features-flags)) — validate empirically |
| **Thread registry** | Version counter + incremental `Changed` args; `GetAllThreads()` returns immutable snapshot ref until version bump |
| **Dashboard refresh** | Separate "metrics only" from "full kanban rebuild"; tie to `BuildThreadMetricsOnly` for telemetry vector |
| **ConversationKeyResolver** | Strip Meta/Google branches or isolate to `LegacyPlatformKeyResolver` used only during import migration |

### KEEP (Working Well)

- Shared UDF + profile-per-instance model (`WebViewProfileManager`, `InstanceWebViewRegistry`)
- Explicit `WebMessageReceived` unsubscribe (`DetachMessageHandler`)
- LRU eviction respecting visible instance (`SelectLruEvictionCandidate`)
- `UiThreadRunner` centralization for WebView2 creation
- Bounded channels with `SingleReader` worker pattern (triage, AI, sync)
- `WebViewNavigationGuard` HTTPS allowlist

---

## 5. Stress Test Recommendations (Agent-Pipeline-Chaos)

These scenarios target the async/memory/ingress vectors above:

| # | Scenario | Steps | Pass Criteria | Exposes |
|---|----------|-------|---------------|---------|
| **C-1** | **Switch storm** | 8 instances, `MaxConcurrentWebViews=6`, rapid sidebar switch 30/min for 10 min | Working set stable ±15%; no orphaned `msedgewebview2` > cap+1; p99 switch < 300 ms | P0-1 mixed memory APIs, LRU eviction |
| **C-2** | **Badge flood** | Script inject or mock adapter sending 200 badge updates/sec × 3 instances | UI responsive (input lag < 100 ms); no OOM; parse alloc rate plateaus | P0-3 UI-thread JSON |
| **C-3** | **Sidebar snapshot burst** | Trigger `WhatsAppSidebarSnapshot` with 500 rows × 5 instances | Triage queue drop rate logged accurately; registry count correct | P1-2 ChannelWriteHelper, ingress |
| **C-4** | **Resolve burst** | 1000 `UpdateThreadStatus: RESOLVED` events in 10 s | Sync `PendingCount` returns to 0 within 30 s; no thread-pool exhaustion | P1-3 sync channel Wait |
| **C-5** | **Sleep-unload toggle** | Enable `EnablePerInstanceSleepUnload`, switch across all instances 50 times | No duplicate profile errors; handler count stable | P0-2 navigation handler leak |
| **C-6** | **WarmAll at cap** | `StartupWarmMode.WarmAll`, 10 instances, cap 6 | Exactly 6 live WebViews; visible instance never evicted | LRU + warm policy |
| **C-7** | **Background + DOM** | Minimize app 30 min with 4 background instances receiving simulated messages | Background memory ≤ target; scripts still deliver events | MemoryUsageTarget vs suspend |
| **C-8** | **Session dispose cycle** | Add/remove instance 100 times | Private bytes return near baseline ±10%; WinRT ref count stable | Disposal order, event tokens |
| **C-9** | **Legacy platform data** | Import thread store with `platform: "metabusiness"` keys | Resolver doesn't corrupt WhatsApp keys; or explicit migration | P2-1 legacy remnants |
| **C-10** | **Dashboard churn** | 200 thread upserts/sec + OCC visible | Debounced refresh ≤ 2 rebuilds/sec; frame time < 16 ms | P1-4 registry sort |

**Instrumentation to add (Stage 3, not now):** ETW provider or `DiagnosticSource` for `InstanceSessionManager` (switch/suspend/resume), ingress queue depth, JSON parse duration, and working set sampled in `ResourceMonitorService`.

---

## 6. Research Findings (2025–2026)

### 6.1 Cs/WinRT Long-Lived WinRT Event Token Leaks

- **CsWinRT issue #842:** `EventSource` subscribe/unsubscribe caches combined delegates; improper unsubscribe patterns can leave handlers attached ([github.com/microsoft/CsWinRT/issues/842](https://github.com/microsoft/CsWinRT/issues/842)).
- **WebView2 issue #3633:** IAsyncOperation Complete handlers registered before termination can leak native memory ([github.com/MicrosoftEdge/WebView2Feedback/issues/3633](https://github.com/MicrosoftEdge/WebView2Feedback/issues/3633)).
- **WinUI page leaks (CsWinRT #413):** C#/WinRT projection can retain native refs if event cycles aren't broken ([github.com/microsoft/CsWinRT/issues/413](https://github.com/microsoft/CsWinRT/issues/413)).
- **Application implication:** Always store delegate identity for `-=` unsubscribe; anonymous lambdas for long-lived `CoreWebView2` events are high-risk. `TypedEventHandler` used for `WebMessageReceived` is the correct pattern — extend to `NavigationCompleted`.

### 6.2 CoreWebView2Environment Chromium Args (V8 / Memory)

- **`AdditionalBrowserArguments`** on `CoreWebView2EnvironmentOptions` passes Chromium/V8 flags at environment creation ([WebView2 browser flags](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/webview-features-flags)).
- Documented flag: **`scavenger_max_new_space_capacity_mb`** — caps V8 minor GC scavenger space (preferred over deprecated `--js-flags=--max_old_space_size` for modern Chromium).
- **Caution:** Flags apply to shared browser process — affects all profiles in the UDF; tune conservatively and measure.
- **Current gap:** `WebViewProfileManager` passes `options: null`.

### 6.3 WebView2 TrySuspendAsync / MemoryUsageTargetLevel.Low

- Official guidance ([WebView2 performance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/performance)):
  - Set `MemoryUsageTargetLevel = Low` on inactive WebViews to reduce memory while **keeping scripts running**.
  - Use `TrySuspendAsync`/`Resume` when WebView won't be used for a while — pauses scripts.
- **Spec explicitly warns:** Do not mix both approaches ([MemoryUsageTargetLevel.md](https://github.com/MicrosoftEdge/WebView2Feedback/blob/main/specs/MemoryUsageTargetLevel.md)):
  - `TrySuspend` auto-sets target level Low.
  - `Resume` on suspended WebView auto-sets Normal.
  - Setting `MemoryUsageTargetLevel` while suspended is ignored.
- **Recommendation for Unified Messenger:** Background notification monitoring needs scripts → **MemoryUsageTargetLevel only** for background; reserve `TrySuspendAsync` for dispose/minimize-without-monitoring paths only.

### 6.4 System.Threading.Channels — High-Throughput .NET 8 Patterns

- **Prefer bounded channels** with explicit `FullMode` over unbounded queues for backpressure ([dev.to/kleinhouzin/channels-in-net-8](https://dev.to/kleinhouzin/channels-in-net-8-4hog)).
- **SingleReader = true** enables internal optimizations; all three app channels already set this ✓.
- **Non-allocating patterns:**
  - Use `TryWrite` on hot path instead of `WriteAsync` when drop/wait policy allows.
  - Use `ReadAllAsync` with `IValueTaskSource` consumer (already in sync/triage workers).
  - For struct messages, prefer `readonly record struct` envelopes to reduce Gen0.
  - Consider **`Channel.CreateBounded` with `BoundedChannelFullMode.DropWrite`** (explicit) vs misleading helper retry.
- **.NET 8+:** `Channel` implementations improved; package 10.x available on NuGet for downlevel ([nuget.org/packages/System.Threading.Channels](https://www.nuget.org/packages/System.Threading.Channels)).

### 6.5 Source-Generated System.Text.Json & ReadOnlySpan Parsing

- **Source generators** (`[JsonSerializable(typeof(T))]`) eliminate reflection overhead for known DTOs — critical at ingress rates > 50/sec.
- **`Utf8JsonReader`** over `ReadOnlySpan<byte>` avoids UTF-16 string allocations for raw postMessage bytes.
- **`ReadOnlySpan<char>`** suitable for fixed vocab parsing (`type`, platform IDs, delivery status labels) without substring allocations.
- **Current codebase:** 100% reflective `JsonDocument` / `JsonElement` path.

---

## 7. Research URLs / Sources

| Topic | URL |
|-------|-----|
| WebView2 performance overview | https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/performance |
| WebView2 feature APIs overview (Aug 2025) | https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/overview-features-apis |
| MemoryUsageTargetLevel spec | https://github.com/MicrosoftEdge/WebView2Feedback/blob/main/specs/MemoryUsageTargetLevel.md |
| CoreWebView2.MemoryUsageTargetLevel API | https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.memoryusagetargetlevel |
| WebView2 browser flags (V8 scavenger) | https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/webview-features-flags |
| CsWinRT EventSource unsubscribe bug | https://github.com/microsoft/CsWinRT/issues/842 |
| WebView2 IAsyncOperation handler leak | https://github.com/MicrosoftEdge/WebView2Feedback/issues/3633 |
| CsWinRT WinUI page leak | https://github.com/microsoft/CsWinRT/issues/413 |
| Channels in .NET 8 (patterns) | https://dev.to/kleinhouzin/channels-in-net-8-4hog |
| System.Threading.Channels NuGet | https://www.nuget.org/packages/System.Threading.Channels |

**Local research artifacts:** `.crawl4ai/webview2-performance.md`, `.crawl4ai/webview2-memory-spec.md`, `.crawl4ai/search-*.json`

---

## 8. Files Audited

| File | Role |
|------|------|
| `UnifiedMessenger/Services/InstanceSessionManager.cs` | WebView2 session lifecycle, suspend/resume, memory tier |
| `UnifiedMessenger/Services/WebViewProfileManager.cs` | Shared environment, UDF, profile creation |
| `UnifiedMessenger/Services/InstanceWebViewRegistry.cs` | Profile ownership tracking |
| `UnifiedMessenger/Services/WebViewUiAwaiter.cs` | WinRT await marshaling |
| `UnifiedMessenger/Services/UiThreadRunner.cs` | Dispatcher queue |
| `UnifiedMessenger/Services/WebViewNavigationGuard.cs` | Navigation allowlist |
| `UnifiedMessenger/Services/WebViewPlatformConfigurator.cs` | Per-platform WebView settings |
| `UnifiedMessenger/Services/Adapters/PlatformAdapters.cs` | Ingress dispatch, navigation hooks |
| `UnifiedMessenger/Services/Adapters/WebMessageParser.cs` | JSON parse |
| `UnifiedMessenger/Services/Adapters/WhatsAppIngressHandler.cs` | WhatsApp-specific ingress |
| `UnifiedMessenger/Services/Adapters/PlatformAdapterInternals.cs` | Adapter resolution |
| `UnifiedMessenger/Services/UnifiedMessengerStateSyncService.cs` | Sync channel |
| `UnifiedMessenger/Services/MessageTriageService.cs` | Triage channel |
| `UnifiedMessenger/Services/Ai/AiInferenceQueue.cs` | AI signal channel |
| `UnifiedMessenger/Services/ChannelWriteHelper.cs` | Channel write helper |
| `UnifiedMessenger/Services/ThreadRegistryService.cs` | Thread registry |
| `UnifiedMessenger/Services/UnifiedMessengerDashboardService.cs` | Dashboard snapshots |
| `UnifiedMessenger/Services/DashboardRefreshCoordinator.cs` | Debounced refresh |
| `UnifiedMessenger/Services/ResourceMonitorService.cs` | Working set telemetry |
| `UnifiedMessenger/Services/ConversationKeyResolver.cs` | Legacy platform key rules |
| `UnifiedMessenger/Services/PlatformModuleSettingsHelper.cs` | WhatsApp-only gate |
| `UnifiedMessenger/Models/PlatformDefinition.cs` | Platform catalog |
| `UnifiedMessenger/Models/PlatformKind.cs` | Legacy enum |
| `UnifiedMessenger/Models/AppSettings.cs` | Memory/session settings |

---

*Agent-Core-Architect — Stage 1 complete. Ready for Stage 3 implementation planning upon swarm approval.*
