# Accessibility listening session — the script

**Why this needs you.** Everything in the accessibility work is right by construction and by test; nobody
has ever *listened* to it. And focus rings are one or two pixels, so they cannot be read reliably from a
screenshot — which means tab order cannot be certified from outside either.

Narrator solves both at once: it **speaks the name of every control as focus lands on it**. Tabbing with
it on turns an invisible problem into an audible one. One pass, both gaps.

Budget about 20 minutes. You do not need to finish it in one sitting — the passes are independent.

---

## Setup (once)

1. **Start Narrator:** `Ctrl` + `Win` + `Enter`. Same keys stop it.
2. **Turn Scan Mode OFF:** `Caps Lock` + `Space` toggles it. Narrator usually starts with it **on**, and
   while it is on, arrow keys read the screen instead of Tab moving focus — which is not what we are
   testing. If pressing Tab reads a whole paragraph rather than naming one control, Scan Mode is on.
3. Open Unified Messenger and go to the **Dashboard**.

If Narrator talks too fast, `Ctrl` + `Alt` + `-` slows it. `Ctrl` alone silences the current sentence.

---

## What "wrong" sounds like

You are listening for five specific things. Everything else is a bonus.

| You hear | What it means |
|---|---|
| **"button"** with no name before it | An unlabelled control. A blind user has no idea what it does. |
| **"instance"** anywhere | Our internal word leaking. The product's word is *account*. A test bans it in visible strings, so any survivor is a gap the test cannot see. |
| A control announced that you **cannot see** | One of the invisible-but-focusable controls this app has shipped before — eight icons once reached the repo with an empty glyph, drawing nothing while staying focusable. |
| Focus **jumping backwards or sideways** on screen | Tab order not matching reading order. I fixed one collision of this kind in v4.99.84; this is how we find out whether more remain. |
| A number read with **no idea what it is** — "45", then nothing | A figure whose label is only visual. |

---

## Pass 1 · Dashboard tab order (5 min)

Click the **Dashboard** entry in the left rail, then press `Tab` slowly, about twenty times, listening.

Expected order, now that the bands are fixed: the four rail entries (Dashboard, Analytics, Reviews,
Reports), then your accounts down the rail, then **Add account → Notification Hub → Settings**, then the
page content on the right.

**Tell me if:** it skips one of the four overview entries · it jumps from the rail into the middle of the
page and back · any account row is silent or says only "button" · the order feels unrelated to what your
eye follows.

## Pass 2 · The needs-a-reply queue (5 min)

Click **Needs reply**. Tab through the filter chips and into the first few customer rows.

**Listen for:** each row naming *who* and *which account* — it should say something like "Open chat with
&lt;name&gt; in &lt;account&gt;", not "button" · the **Reply** and **Done** buttons on each row being
distinguishable from each other · the notice line reading as one sentence: *"1 Messenger account not
shown here — nothing reads that channel yet."*

## Pass 3 · Settings (5 min)

`Ctrl` + `,` opens Settings. Tab from the section list into the content.

**Listen for:** each toggle naming what it does *and* its state (on/off) · the two scraper-health lines
under **Data & privacy** reading as sentences · nothing saying "instance".

## Pass 4 · One dialog (3 min)

Click **Add account**. Tab around it, then press `Escape`.

**Listen for:** focus staying *inside* the dialog rather than escaping to the page behind it · the
Platform and Workspace dropdowns naming their current value.

**Known already:** Escape did not close this dialog when I tried it. If that repeats for you, say so and
I will fix it — a dialog a keyboard user cannot dismiss is a trap.

---

## Reporting back

Rough notes are fine. The most useful shape is:

> Pass 1 — after Reports it jumped to the Re-sync button, then back to the accounts.
> Pass 2 — the Done buttons all just say "button".

Tell me what you heard and I will turn each one into a fix and a test. If a pass is entirely clean, say
that too — "nothing wrong in pass 3" is a real result, and it is the first time any of this will have
been confirmed by ear rather than by construction.
