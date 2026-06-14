# STAGE 3 — Agent-Core-Architect Changelog

**Date:** 2026-06-14  
**Agent:** Core-Architect (Stages 3–4 implementation)

## P0 — Correctness / Memory Safety

### WebView2 memory strategy (P0-1)
- **Chose `MemoryUsageTargetLevel` only** for background/foreground transitions (`SetSessionVisualState`).
- **Removed `TrySuspendAsync`** from instance switch and hide paths.
- **Kept `TrySuspendAsync`** only in `DisposeSessionEntryCoreAsync` pre-close (hard teardown path).
- Aligns with [MemoryUsageTargetLevel spec](https://github.com/MicrosoftEdge/WebView2Feedback/blob/main/specs/MemoryUsageTargetLevel.md): do not mix suspend/resume with explicit Low/Normal toggles while scripts must keep running.

### NavigationCompleted leak (P0-2)
- Added `PlatformNavigationHooks` with stored `TypedEventHandler` identity.
- `BasePlatformAdapter.RegisterNavigationHooks` now uses `PlatformNavigationHooks.Attach`.
- `InstanceSessionManager.DisposeSessionEntryCoreAsync` calls `PlatformNavigationHooks.Detach`.

### UI-thread JSON ingress (P0-3)
- Added `WebMessageIngressService`: bounded channel (`DropOldest`), coalescing for `badge-count`, `adapter-heartbeat`, `whatsapp-telemetry`.
- `WebMessageReceived` handler enqueues only (O(1) on UI thread).
- Background worker parses JSON off UI thread; `HandleParsedWebMessage` runs on dispatcher.
- Added source-generated `AdapterMessageJsonContext` + `WebMessageEnvelope` for fast type extraction.

### WebViewProfileManager environment options (P1-1)
- `CoreWebView2EnvironmentOptions.AdditionalBrowserArguments`:
  `--js-flags=--scavenger_max_new_space_capacity_mb=32`
- Applies to shared browser process across all profiles in the UDF.

## P1 — Performance / Scalability

### ChannelWriteHelper (P1-2)
- Renamed to `TryWriteWithDropOldest(reader, writer, item, channelName)`.
- Actually drops oldest via `reader.TryRead` before retry; honest logging.

### UnifiedMessengerStateSyncService (P1-3)
- Removed fire-and-forget `EnqueueAsync` Task per resolve event.
- Hot path uses `TryWrite`; overflow uses `ConcurrentQueue` drained by worker.

### ThreadRegistryService (P1-4)
- Lazy sorted cache with `_mutationVersion` / `_sortedCacheVersion`.
- `NotifyChanged` increments version; `GetAllThreads` rebuilds only when version differs.
- `RefreshOperationalFlags` no longer invalidates sort cache (sort key unchanged).

## Legacy purge (WhatsApp-only)

| File | Change |
|------|--------|
| `PlatformKind.cs` | Removed `Meta`, `Google` enum values and mappings |
| `AdapterMessageTypes.cs` | Removed Meta/Google message type constants |
| `ConversationKeyResolver.cs` | Removed Meta fingerprint, review keys, platform-specific branches |
| `OperationsThreadCardViewModel.cs` | Removed dead glyph arms |

Runtime gating via `PlatformModuleSettingsHelper` / `PlatformAdapterInternals` unchanged.

## Files added
- `Services/WebMessageIngressService.cs`
- `Services/Adapters/AdapterMessageJsonContext.cs`
- `Services/Adapters/PlatformNavigationHooks.cs`

## Files modified
- `InstanceSessionManager.cs`, `PlatformAdapters.cs`, `NullPlatformAdapter.cs`
- `WebViewProfileManager.cs`, `ChannelWriteHelper.cs`
- `UnifiedMessengerStateSyncService.cs`, `ThreadRegistryService.cs`
- `MessageTriageService.cs`, `Ai/AiInferenceQueue.cs`
- Legacy files above + affected unit tests

## Not touched (per scope)
- `whatsapp-adapter.js`, OCC XAML (UI agent domain)
