---
id: process-ci-fix-needs-real-exit-code
date: 2026-08-28
agent: claude
title: Do not ship a CI fix inferred from pass/fail patterns; get the job's real exit code first
triggers: [ui-smoke, flaky, CI red, exit 3, exit 4, exit 5, job log, 403, admin rights, pwsh, diagnosis]
files: [.github/workflows/build.yml, UnifiedMessenger.UiSmokeTests/Program.cs, docs/audit-2026-08/00-remaining-work.md]
cost: Two wrong ui-smoke diagnoses in a row; one shipped as commit 17faed8 (tri-state window probe), failed identically, and was reverted
evidence: docs/audit-2026-08/00-remaining-work.md (diagnoses #1 and #2, the exit-code table); build.yml "::notice::ui-smoke harness exit code"
status: live
---
From "the same commit passes one run and fails the next" plus "the harness passes locally", the session guessed exit 4-vs-5 and shipped a fix. Next it guessed exit 3 from a repro that was invalid. Both targeted code paths that never ran. The later audit found the step returned exit 1, most likely the `shell: pwsh` wrapper aborting under `$ErrorActionPreference='stop'` before its exit-5 tolerance could run. That was not reproducible locally, because this machine has only Windows PowerShell 5.1. Job logs cannot be downloaded without repo admin (`/actions/jobs/{id}/logs` returns 403 "Must have admin rights"). Read the `::notice::` exit-code annotation the step now emits, or ask the owner for the step tail, before committing any change that loosens a failure condition.
