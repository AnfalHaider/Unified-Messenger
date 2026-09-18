---
id: v6-a-figure-label-must-match-what-it-measures
date: 2026-09-18
agent: claude
title: "Answered on time" on the line was the caught-up share, not replies against the target, and the two disagree in public
triggers: [answered on time, on time percent, caught up, onTimePercent, rollup, SLA met, figures, line screen, 93% on time, misleading figure]
files: [v6/app/view-model.ts, v6/core/rollup.ts, v6/ui/screens/work.tsx]
cost: The owner's own screenshot read "93% were answered on time" next to "median first reply of 130 minutes"; it stood for weeks.
status: live
---

`OversightRollupBuilder`'s `onTimePercent` (ported into `core/rollup.ts`) is `caughtUp / active`: the share of chats active today that have an answer at all. The line screen showed it under the label **Answered on time**, which is a different figure — measured replies within the target, `responseStats(...).slaPercent`. Both are true and they move independently, so the headline could say 93% answered on time above a median first reply of 130 minutes, and the screen was wrong rather than merely confusing. They are now two figures: **Caught up** ("of the chats active today, those with an answer") and **Answered on time** ("N replies measured, target 90%"), which reads "—" when nothing has been measured instead of 0% — a zero there claimed every reply missed the target.

The general rule for this app: a figure's label has to name the thing that was computed, and a percentage with no denominator yet is "—", never 0. When porting a v5 metric, check what its own code divides before reusing the v5 caption — v5 called this one "Caught up", and that was right.
