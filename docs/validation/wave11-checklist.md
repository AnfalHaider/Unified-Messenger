# Wave 11 validation checklist

## Automated gates (CI)

| Gate | Command / job |
|------|----------------|
| Unit tests (750+ target) | `dotnet test UnifiedMessenger.Tests -c Release` |
| UI smoke | `ui-smoke` job in `.github/workflows/build.yml` |
| Vulnerable packages | `dotnet list package --vulnerable --include-transitive` |

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

### Keyboard paths (`UxValidationChecklist.KeyboardPaths`)

1. Dashboard tabs → Operations Command Center
2. Branch workspace pill → thread card
3. Thread card / notification → instance with conversation focus
4. Settings section nav → in-page section
5. Personal search → account navigation

### AutomationProperties / TabIndex

- Dashboard tabs (`TabIndex=10`)
- OCC refresh (`20`), branch pills (`30`), layout (`40`)
- Settings nav (`10`) and content (`20`)
- Personal search (`20`)
