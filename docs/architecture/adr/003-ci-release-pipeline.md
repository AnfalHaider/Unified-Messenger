# ADR-003: CI-built release artifacts

## Status

Accepted (Wave 3)

## Context

Releases previously risked using stale committed installers instead of CI publish output.

## Decision

GitHub Actions `package` job publishes win-x64 and win-arm64, builds Inno installers, writes SHA-256 sidecars, uploads artifacts. `release` job downloads CI artifacts for tagged releases. `ui-smoke` validates published x64 binary.

## Consequences

- Reproducible release binaries.
- Smoke tests gate packaging regressions before tag release.
