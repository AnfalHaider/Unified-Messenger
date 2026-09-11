---
id: tooling-grep-counts-include-bin-copies
date: 2026-08-28
agent: claude
title: Grep counts over UnifiedMessenger/ include bin/ build copies and inflate XAML counts exactly 4x
triggers: [grep count, rg count, Opacity, XAML sites, audit sizing, bin, obj, build output, inflated count]
files: [docs/audit-2026-08/00-remaining-work.md, docs/design-system/scales.md]
cost: An audit brief sized the Opacity-contrast defect at 352 sites (really 88) and a remediation roadmap was written around it, then falsified.
evidence: docs/audit-2026-08/00-remaining-work.md row A0-3 — `grep -o 'Opacity="0\.[0-9]+"'` over UnifiedMessenger/ including build output returns 352; tracked XAML holds 88; every sub-figure was exactly 4x.
status: live
---
A recursive grep over `UnifiedMessenger/` also walks `bin/` and `obj/`, where each built configuration carries a copy of every `.xaml`. The result is a clean integer multiple of the real count, so it looks plausible. The 352 was believed and prioritised; the real 88 was a quarter the size, and 51 of them sat in one file (`SettingsPage.xaml`). When sizing a defect by counting sites, exclude `bin/` and `obj/`, or count only `git ls-files`. Treat any count that divides evenly by a small integer as suspect. `docs/design-system/scales.md` states its counts exclude `obj/` and `bin/`; do the same.
