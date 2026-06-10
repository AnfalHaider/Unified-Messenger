# Wave 11 validation checklist

## Automated gates (CI)

| Gate | Command / job |
|------|----------------|
| Unit tests (750+ target; v2.0.6 baseline **966**) | `dotnet test UnifiedMessenger.Tests -c Release -p:Platform=x64` |
| Line coverage gate (≥38%; measured baseline ~38.0%, aspirational 40%) | `verify` job — `COVERAGE_LINE_THRESHOLD` in `build.yml` |
| Benchmark regression (`ResolveLayout` mean ≤ 6 μs) | `verify` job — `UnifiedMessenger.Benchmarks --job short` |
| UI smoke (PR/push) | `ui-smoke` job in `.github/workflows/build.yml` |
| UI smoke (nightly 06:00 UTC) | `ui-smoke-nightly` job in `.github/workflows/ui-smoke-nightly.yml` |
| Vulnerable packages | `dotnet list package --vulnerable --include-transitive` |
| Dependency updates (weekly) | Dependabot — NuGet + GitHub Actions in `.github/dependabot.yml` |

## Integration scenarios

- Refresh coalescing: `DashboardRefreshCoordinator` debounces operational events (450 ms); `PerformanceValidationHelper.EstimateCoalescedRefreshCount` expects one refresh per burst.
- Import validation: invalid `StartUrl`, empty store, missing file rejected by `InstanceRegistryService`.
- Clear scopes: `OperationalDataService.ClearAllAsync` clears analytics, triage, threads, workspace operational data, backfill dedupe, and notification alerts/badges.

## Performance harness expectations

| Scenario | Expectation |
|----------|-------------|
| OCC refresh burst | ≤ 1 coalesced refresh per debounce window |
| Startup warm (5 instances) | `VisibleOnly` / `Lazy` warm 1 WebView; `WarmAll` warms all 5 |
| Instance switch latency | ≤ 2000 ms sample threshold (`PerformanceValidationHelper`) |

## Security re-audit (S1–S10)

All items tracked in `SecurityAuditChecklist.cs` with `IsResolved = true`. Re-validate with source grep tests in `Wave11ValidationTests`.

## UX / accessibility manual pass

### XAML surfaces (`UxValidationChecklist.XamlSurfaces`)

Verify layout, empty states, and destructive confirmations on each listed surface.

### Navigation (manual)

- [x] Dashboard ↔ Settings ↔ Instance ↔ back — no orphan stack entries — Implemented in v1.1.1 - verify on release
- [x] Notification panel open state visible in sidebar and title bar — Implemented in v1.1.1 - verify on release
- [x] Command palette reaches all destinations — Implemented in v1.1.1 - verify on release

### Keyboard paths (`UxValidationChecklist.KeyboardPaths`)

1. Dashboard tabs → Operations Command Center
2. Branch workspace pill → thread card
3. Thread card / notification → instance with conversation focus
4. Settings section nav → in-page section
5. Personal search → account navigation
6. OCC layout edit → preset picker, drag move, keyboard nudge, hide/restore tray
7. Personal layout edit → move section up/down
8. Shell navigation → sidebar selection survives registry refresh; notification hub shows active state

### Accessibility (manual)

- [x] Complete keyboard path for layout edit — Implemented in v1.1.1 - verify on release
- [x] Narrator announces panel operations — Implemented in v1.1.1 - verify on release
- [x] Settings fully named — Implemented in v1.1.1 - verify on release

### Performance UX (manual)

- [x] Conversation focus &lt;2s p95 — Implemented in v1.1.1 - verify on release

### Branch pulse (manual)

- [ ] Branch pulse panel visible in OCC (default in WhatsApp-focus preset)
- [ ] Refresh pulse generates themes + summary when Local AI is running
- [ ] Branch-scoped pill filters pulse to selected branch
- [ ] Post-backfill auto-invalidates pulse cache for that branch

### OCC grid builder (manual)

- [x] All seven OCC sections draggable across columns — Implemented in v1.1.0 - verify on release
- [x] Resize with Shift+arrow or +/− when focused — Implemented in v1.1.0 - verify on release
- [x] Presets apply and persist across restart — Implemented in v1.1.0 - verify on release
- [x] Restore default layout resets grid — Implemented in v1.1.0 - verify on release
- [x] Undo InfoBar restores prior layout — Implemented in v1.1.0 - verify on release

### AutomationProperties / TabIndex

- [x] Dashboard tabs (`TabIndex=10`) — Implemented in v1.1.0 - verify on release
- [x] OCC refresh (`20`), branch pills (`30`), layout (`40`) — Implemented in v1.1.0 - verify on release
- [x] Settings nav (`10`) and content (`20`) — Implemented in v1.1.0 - verify on release
- [x] Personal search (`20`) — Implemented in v1.1.0 - verify on release
