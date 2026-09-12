# v6 screens

The 39 screens of Unified Messenger v6, as generated sources. The published canvas is the artifact
"Unified Messenger v6 Screens"; these files are what produce it, so **never hand-edit the generated
`.dc.html` files — change a generator and run it again.**

```
node build.mjs          # writes 39 .dc.html artboards + canvas.json next to this folder's output dir
node publish.mjs        # inspects the published page's document record
node publish.mjs write  # rebuilds the canvas page with the new artboards, for republishing
```

| File | Holds |
|---|---|
| `lib.mjs` | Tokens, icons, title bar, rail, and the shared pieces: figure band, clock, meter, chips, buttons. |
| `screens-a.mjs` | Command Center, the live account page, account detail. |
| `screens-b.mjs` | Reviews, Analytics, the weekly report, assistant, notifications, command palette. |
| `screens-c.mjs` | The eight Settings screens. |
| `screens-d.mjs` | Sign-in and first-run screens, and the owner console. |
| `screens-e.mjs` | Dialogs: add and edit account, invite and remove member, suspend, delete. |
| `screens-f.mjs` | States (suspended, offline, empty, all caught up, reader broken, update) and system screens. |

## The design, in one paragraph

What matters in this product is time running out on a waiting customer, so the screens are built like a
departures board rather than a dashboard. The waiting list is the only loud thing: a condensed clock
numeral, a bar filling toward the reply target with a tick where the target sits, and a hairline row.
Colour means one thing only — on time, due soon, past target — so it is never decoration. The shell and
rail are near-black indigo against a white work surface, which gives the app a silhouette; the brand
indigo appears twice, on the mark and the primary button. Figures are typeset in a band rather than
boxed into cards, because four identical cards say nothing about which number matters.
