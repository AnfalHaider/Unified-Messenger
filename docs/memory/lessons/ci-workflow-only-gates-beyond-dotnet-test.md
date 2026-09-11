---
id: ci-workflow-only-gates-beyond-dotnet-test
date: 2026-08-28
agent: claude
title: A green dotnet test is not the CI verify gate; build.yml has steps no local run executes
triggers: [CI failed, verify, Enforce shell DI gate, build.yml, singleton, ShellController, ApplicationServices, tag, release, substring]
files: [.github/workflows/build.yml, UnifiedMessenger.Tests/ShellLayerDiTests.cs, UnifiedMessenger/Services/Shell/ShellController.cs]
cost: v4.99.58 tag and the merge to main both failed CI after 1863 local tests passed; tag deleted and re-released as v4.99.59
evidence: CHANGELOG.md ## v4.99.59; build.yml step "Enforce shell DI gate"; ShellLayerDiTests
status: live
---
The assumption was "full suite green, so CI will pass", and the owner was told so before the push. `verify` failed at "Enforce shell DI gate", a step that exists only in the workflow. It fired because `ShellController.StartupWarmCount` used a banned app-settings singleton as an optional default. The gate substring-matches each file's whole text, so a comment naming the banned singleton trips it as well as a call. `ShellLayerDiTests` now mirrors that gate. Other `verify` steps still have no local twin, such as the coverage threshold and the vulnerable-package check. Before tagging, read `build.yml` and run every non-test step locally rather than predicting the result.
