---
id: v6-a-native-view-does-not-follow-the-css
date: 2026-09-23
agent: claude
title: The docked page is a native view, so a layout change has to move two things, and only its own bounds prove it
triggers: [dock, WebContentsView, page-slot, layout, LINE, CUSTOMER, collapse panels, full width, getBounds, tokens.css]
files: [v6/app/main.ts, v6/ui/tokens.css, v6/ui/screens/work.tsx, v6/tests/smoke.spec.ts]
cost: None — the CSS already carried a warning about it, which is the only reason it was not a bug.
status: live
---

The account's page on the dock is not in the DOM. It is a `WebContentsView` that `app/main.ts` lays over an
empty `.page-slot` div, positioned from the constants `RAIL`, `LINE`, `DOCK_BAR` and `CUSTOMER`. So **any
change to the dock's layout has to be made twice** — in `ui/tokens.css` and in `layout()` — and the two must
agree to the pixel, or the page sits over a panel that is still drawn underneath it.

`tokens.css` already says this in a comment above `.split`. Believe it.

The testing consequence matters more than the coding one: **a test that asserts on the DOM proves nothing
here.** A class on a div can flip while the native view stays exactly where it was, and the screenshot would
show a page covering the customer panel. The test reads the view's own bounds instead:

```ts
const v = BrowserWindow.getAllWindows()[0].contentView.children.find((c) => c.getBounds);
```

— then checks that widening moves `x` left and makes `width` larger, and that closing it returns `x` to
exactly where it started. Confirmed by pinning `x` back to `RAIL + LINE` on purpose and watching that one
assertion go red while everything about the DOM still passed.
