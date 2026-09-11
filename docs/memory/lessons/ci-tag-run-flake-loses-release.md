---
id: ci-tag-run-flake-loses-release
date: 2026-09-06
agent: claude
title: A flaky test on the v* tag run silently loses the release even when main is green on the same sha
triggers: [release missing, tag run failed, flaky test, CI timing, named pipe timeout, same sha different result, SecondInstanceActivatorTests, Releases stuck]
files: [.github/workflows/build.yml, UnifiedMessenger.Tests/SecondInstanceActivatorTests.cs, docs/remaining-work.md]
cost: v5.0.1 was tagged but never released (sidebar stayed on v4.99.72); only a later v5.1.0 tag shipped it.
evidence: Commit 859e989 passed its main run and failed its v5.0.1 tag run in the same minute; fix 8e4ab6d raised the 3s pipe caps to PipeHangGuard=30s (comment in SecondInstanceActivatorTests.cs); db10fef showed the same main-pass/tag-fail pattern (docs/remaining-work.md).
status: live
---
Pushing `main` and a `v*` tag together starts two CI runs on the same commit. Only the tag run builds installers: `package` needs `verify`, and `release` needs `package` (`build.yml`). So a timing-sensitive test that trips on a loaded runner removes the release, while the matching `main` run stays green. The quick diagnosis is to compare the two runs on that sha. If `main` passed and the tag failed, suspect a flake before the code. In test code, a wall-clock cap on an operation that normally takes milliseconds (like the old 3s named-pipe caps) is really timing the runner. Make hang-guards generous and name them that way. The CI log needed authentication, so the failing test was never actually named and the 30s fix is unproven. Get the test name from the log before calling the flake fixed. Don't delete and re-push the orphan tag. The next version is a superset, and auto-update reads Releases.
