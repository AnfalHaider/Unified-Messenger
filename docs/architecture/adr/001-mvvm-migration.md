# ADR-001: Full MVVM migration with presenters

## Status

Accepted (Wave 6+)

## Context

Large code-behind files (especially Operations Command Center) mixed UI event wiring, snapshot building, and business rules.

## Decision

- Adopt CommunityToolkit.Mvvm `ViewModelBase` + observable state.
- Keep WinUI views thin; map domain snapshots in **presenters** (pure static helpers).
- Migrate incrementally: shell → OCC → personal → settings (Waves 7–9).

## Consequences

- Testable presenter/VM units without UI thread.
- OCC main code-behind split into partials and `Controls/Occ/` helpers (Wave 13; main file &lt; 400 lines).
