---
id: gotcha-failed-read-zero-reads-as-caught-up
date: 2026-08-15
agent: claude
title: An account whose read fails contributes zero awaiting, so a missing branch pushed the headline toward "You're all caught up"
triggers: [all caught up, totalAwaiting == 0, ReadFailed, unreadable account, no activity, hero, briefing, render signature, insight cache signature, zero count, missing data reassures]
files: [UnifiedMessenger/Services/Oversight/CaughtUpClaim.cs, UnifiedMessenger/Services/Oversight/AccountReadHealth.cs, UnifiedMessenger/Controls/CommandCenterPanel.xaml.cs, UnifiedMessenger.Tests/CaughtUpClaimTests.cs, UnifiedMessenger.Tests/AccountReadHealthTests.cs, docs/audit/findings/state-matrix.md]
cost: S2. The dashboard could show a green tick and "You're all caught up" while a branch was not being measured at all, and before v4.99.8 an unreadable branch rendered as "no activity".
evidence: 0194134 (v4.99.25, F-STATE-01; reverting Resolve to `totalAwaiting == 0` fails exactly the three defect tests); 1055dc6 (v4.99.8, F-SNAP-02).
status: live
---
The hero and briefing decided caught-up from `totalAwaiting == 0` alone. The card beneath already rendered ReadFailed, but `RenderHero` never looked. Whether data is missing cannot be derived from the numbers, so carry it explicitly. `AccountReadHealth` records the final outcome after all fallbacks, a location is flagged if any member failed, and the flag is never inferred from a zero count. Make one shared decision (`CaughtUpClaim`) for every surface, including the AI prompt, which must name the unmeasured accounts or the model writes the same false reassurance. Any state that changes text without changing a count must be in the render signature and the insight cache signature. Otherwise the redraw is skipped as a no-op, or the cached pre-failure briefing is served.
