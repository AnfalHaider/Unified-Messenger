---
id: ci-ui-smoke-text-match-hit-empty-state
date: 2026-08-29
agent: claude
title: ui-smoke flaked for months because a "contains WhatsApp" matcher clicked the no-accounts guidance text
triggers: [ui-smoke, exit code 3, ModuleValidationHarness, ValidateInstanceSwitch, flaky, empty state, UI automation, CI has no accounts]
files: [UnifiedMessenger.UiSmokeTests/ModuleValidationHarness.cs, .github/workflows/build.yml, docs/audit-2026-08/04-roadmap.md]
cost: ui-smoke red on roughly half of CI runs since June; five diagnoses (two in the fixing session) were wrong before the exit code was printed.
evidence: build.yml step `::notice::ui-smoke harness exit code = $code` reported 3, not the assumed 5; harness detail "Could not click instance 'Click + in the sidebar to add your first WhatsApp account…'"; comment above ValidateInstanceSwitch.
status: live
---
`ValidateInstanceSwitch` took the first automation `Text` element whose name contained "WhatsApp". A CI runner has no accounts, so the dashboard shows its empty state, and that guidance sentence also contains "WhatsApp". The harness clicked a paragraph and returned Fail (exit 3). The `instanceName is null` Warn guard never fired because a match was found, just the wrong kind of element. It was intermittent because the empty-state text rendering before the probe is a race. Before theorising about a smoke failure, make the harness print its exit code and the failing module's detail. Grep every validator, not one file, before claiming which modules can fail. A UI-automation matcher must check the element's shape (short display name, no sentence punctuation), not just a substring.
