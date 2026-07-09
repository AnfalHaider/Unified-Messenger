# ADR-004: Interface composition root

## Status

Accepted (Wave 5)

## Context

`MainWindow` and pages referenced concrete singletons directly, blocking test doubles and increasing coupling.

## Decision

- Define core interfaces under `Services/Contracts/`.
- Wire defaults in `ApplicationServices` composition root.
- Migrate shell consumers to `_services` field (ongoing).

## Consequences

- Wave 5+ tests validate interface registration.
- Residual `.Instance` usages remain outside shell until fully migrated.
