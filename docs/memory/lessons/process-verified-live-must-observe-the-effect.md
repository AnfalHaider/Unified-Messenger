---
id: process-verified-live-must-observe-the-effect
date: 2026-09-04
agent: claude
title: "Verified live" must observe the end effect, not a healthy channel or a green unit test
triggers: [verified live, toast, notification fallback, coverage notice, wiring, unit tests green, smoke test, feature renders nothing]
files: [UnifiedMessenger/Services/Notifications/AppNotificationService.cs, UnifiedMessenger/Services/Oversight/ChannelCoverage.cs, UnifiedMessenger.Tests/ChannelCoverageTests.cs, docs/scraper-foundation-roadmap.md]
cost: Two features shipped dead while their increments said "verified live". The classic-toast fallback threw on every delivery, and the A8 channel-coverage notice (v4.99.81) never rendered.
evidence: AppNotificationService.cs comment on ToastNotification.Group ("Value does not fall within the expected range"); ChannelCoverageTests.TheQueueDoesNotComputeCoverageFromItsAlreadyFilteredList; docs/scraper-foundation-roadmap.md "the suite proves functions, the app proves features".
status: live
---
Increment 68 was called "verified live" because the toast channel opened and Windows reported the AUMID `Enabled`. Both were true, but copying a null `Group`/`Tag` onto `ToastNotification` threw on every info toast, so nothing was ever delivered. The A8 notice's `ChannelCoverage.DescribeGaps` was fully unit-tested, but the caller passed a list already filtered by `ContributesConversationMetrics`, which removed every channel the notice exists to name. Unit tests that feed a function synthetic input prove the function, not the wiring. Before writing "verified", see the actual result: a toast on screen, the line drawn in the running app, or a log showing zero delivery failures over a fixed window. Where the wiring can regress, add a source guard like the ChannelCoverageTests one.
