---
id: v6-axe-in-electron-needs-legacy-mode
date: 2026-09-19
agent: claude
title: axe-core/playwright fails in Electron unless it runs in legacy mode, and v6's contrast failures were all one token
triggers: [axe, accessibility, a11y, wcag, color-contrast, AxeBuilder, Target.createTarget, Not supported, ink-3, contrast ratio, electron playwright]
files: [v6/tests/smoke.spec.ts, v6/ui/tokens.css]
cost: Found while building 4.12; the first run failed before checking anything.
status: live
---

`new AxeBuilder({ page }).analyze()` opens a second blank page in the browser context to finish the run, and an Electron window from Playwright's `_electron` refuses that with `browserContext.newPage: Protocol error (Target.createTarget): Not supported`. `.setLegacyMode()` runs axe entirely inside the page and works. Use the named export (`import { AxeBuilder } from '@axe-core/playwright'`); the default import has no construct signature under this tsconfig.

The first full run over 26 screens (13 screens × light and dark) found 11 problems, and every contrast one was the tertiary grey `--ink-3` at 4.29–4.49:1 on the grounds it sits on — one token just under the line, not scattered bad colours. Fix the token, and compute the replacement instead of eyeballing it: step the grey along its own hue until it clears ~4.6:1 on the darkest background it is used on (light #6A716B → #5F6660, dark #858D86 → #89918A). Print axe's `fgColor`, `bgColor` and `contrastRatio` in the failure message so the next finding arrives with its numbers. The one non-colour finding: a scrolling results list that only the arrow keys move still needs to be focusable (`tabIndex={0}`, a label) so a keyboard can scroll it.
