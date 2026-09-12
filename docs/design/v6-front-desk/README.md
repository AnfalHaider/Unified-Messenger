# v6 Front Desk renders

The approved design for Unified Messenger v6: 36 screens across every phase of the plan, each 1440 × 900,
in light, dark and match-Windows. The published page is the artifact "Unified Messenger Front Desk". These
files are its source.

Open `index.html` through any static server (not Vite: Vite injects its client into the first `<header` it
finds in the inline templates). Every board is plain HTML built by the `boards-*.js` files on top of `lib.js`
(tokens, shell, the line) and `charts.js` (sparkline, line chart, heat map).

## The design, in brief

- **The line** is the one loud element: everyone waiting, placed by minutes waited, one lane per location,
  with the reply target drawn through it.
- **Act, don't just read.** Every waiting customer opens a chat, is marked handled, or is snoozed.
- **The account page docks beside the line** instead of replacing the window.
- **Problems with the app are kept apart from customers**, in Needs you and on the Accounts grid.
- **Colour means lateness and nothing else.** Everything else is ink. Status marks pass the dataviz validator
  on both grounds; text steps are held to WCAG separately.
- **Type:** Bricolage Grotesque for headings, Instrument Sans (with its width axis for timers) for everything
  else. Both are bundled in the app from npm, never fetched.

The product is for any business with customers on WhatsApp, Instagram and Google, not one trade. Customers,
reviews and figures in the renders are invented; account and location names are the owner's own.

`v6/ui/tokens.css` is lifted from `app.css` and `more.css` here. Change a colour in both.
