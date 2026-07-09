# ADR-002: Dashboard refresh coordinator

## Status

Accepted (Wave 2)

## Context

Operational events (triage, threads, analytics, notifications, backfill) caused duplicate OCC/personal refreshes (architecture issue A1).

## Decision

Introduce `DashboardRefreshCoordinator` as the single debounced subscriber (450 ms). `DashboardPage` owns the coordinator; OCC no longer subscribes to operational events directly.

## Consequences

- ≤1 coalesced refresh per event burst (validated in Wave 11).
- `RefreshOperationalFlags(raiseChanged: false)` on read paths resolves A4 notification storms.
