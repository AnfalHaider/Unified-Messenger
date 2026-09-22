---
id: v6-a-gate-changes-what-every-test-starts-from
date: 2026-09-22
agent: claude
title: Adding a sign-in gate broke four tests that had never mentioned sign-in, and each break was a real product question
triggers: [gate, admission, sign-in required, invitation only, UM_CLOUD_CONFIG, Playwright, no rail, Continue with Google, lock screen order]
files: [v6/core/admission.ts, v6/app/main.ts, v6/ui/App.tsx, v6/ui/screens/workspace.tsx, v6/tests/smoke.spec.ts]
cost: Four Playwright tests red, and three of them were pointing at gaps in the feature rather than at themselves.
status: live
---

A gate in front of the whole app is not a screen; it changes the first thing every other screen assumes. Four
tests broke, and only one was merely a test:

- **Every test suddenly had a cloud config.** The shared launcher set `UM_CLOUD_CONFIG` for all tests so none
  could reach real Google, which made every one of them *gateable*. The fix was to make it opt-in: a build
  that cannot ask anyone gates nobody, so a test of the line or the reports simply has none.
- **Only the product owner can start a workspace.** With invitation-only access, an app belonging to no
  workspace admits nobody else — so the person who creates the first workspace must be the product owner. The
  tests now seed `owners/{uid}` because that is what production does, not to make them pass.
- **An invited person could not reach Join.** It lived in Settings, and the gate covers Settings: a door
  opening onto a wall. Join now sits on the gate itself, with who they are signed in as and what they were
  invited to.
- **Removal has to outrank the gate.** Removing a PC signs it out, so the gate would have replaced "this PC
  was removed from Sample Business, here is what was wiped" with a bare sign-in screen, losing the reason at
  the moment it mattered most.

Two habits that paid: **break the guard on purpose** (deleting the read guard made the gate test go red on the
exact assertion that mattered), and **rebuild the screens before a Playwright run** — `npx playwright test`
alone runs the last `vite build`, so a UI change that has not been built shows the old screen and sends you
hunting in the wrong file.
