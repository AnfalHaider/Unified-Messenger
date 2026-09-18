---
id: v6-a-field-must-adopt-the-value-that-arrives-late
date: 2026-09-18
agent: claude
title: A text field seeded from pushed state stays empty forever unless it adopts the stored value when that value arrives or changes
triggers: [note field, textarea, useState initial value, stale state, customer panel, setNote, push state, empty after restart, controlled input]
files: [v6/ui/screens/work.tsx, v6/app/view-model.ts]
cost: Found by the customer-panel test: the note was saved correctly and the panel still showed an empty box after a restart.
status: live
---

The screens are drawn from a state object the main process pushes every few seconds, so a panel can mount before the part of the state it needs has arrived. A field written as `useState(card?.note ?? '')` takes whatever was there at mount — usually nothing — and an effect keyed on the chat (`[accountId, chatKey]`) does not re-run when the note itself arrives a second later, so the box stays empty while `customers.json` holds the real text. The fix is to adopt the stored value whenever it *changes*, comparing against the last stored value seen in a ref, not against what the owner has typed:

```tsx
const stored = card?.note ?? '';
const lastStored = useRef(stored);
useEffect(() => { if (stored !== lastStored.current) { lastStored.current = stored; setNote(stored); } }, [stored]);
```

That adopts the first arrival and any later change without overwriting typing in progress, because typing does not change `stored`. The same shape is needed for any editable field fed by pushed state. Two smaller findings from the same panel: a button's accessible name comes from its text, not its `title`, so a Playwright query by a `title`-only name finds nothing (give it `aria-label`); and a count that only starts at the first read ("N times on the line") must not be shown as "0 times" for someone who is visibly waiting — say when they were first seen instead.
