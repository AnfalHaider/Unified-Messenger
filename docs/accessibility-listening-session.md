# Accessibility listening session — the script

## ✅ Run 2026-09-05, build v4.99.98 — all five passes clean

The owner ran every pass with Narrator. **Nothing wrong was heard in any of them.** This is the first time
any of the accessibility work in this product has been confirmed by ear rather than by construction and
test.

| Pass | Surface | Result |
|---|---|---|
| 1 | Dashboard tab order | Clean — the v4.99.84 band fix holds |
| 2 | Needs-a-reply queue | Clean — rows name who and which account |
| 3 | Open account + read strip | Clean — the strip is reachable and reads as one sentence |
| 4 | Reviews queue | Clean — **the rows fixed blind in v4.99.97 announce correctly** |
| 5 | Settings + Add account dialog | Clean — including focus containment and Escape |

> ⚠️ **A retraction.** Earlier versions of this script said, under Pass 4/5: *"Known already: Escape did not
> close this dialog when I tried it."* **It does.** That claim came from an unreliable observation during a
> screenshot-driven check and was carried forward as fact through several revisions of this file. It is
> removed rather than softened — a stale "known issue" sends the next reader hunting for a bug that is not
> there, and costs more than saying nothing would have.

**What this result does and does not mean.** Five specific failure modes were listened for across five
surfaces and none occurred. It is not a certificate of accessibility: it says nothing about high contrast,
magnification, non-Narrator readers, or any surface not walked here. What it does establish is that the
named surfaces are *usable by ear*, which nothing before this had ever demonstrated.

**Two defects were fixed in the hour before the session**, by scanning rather than by listening — the
review rows announcing only "button" (v4.99.97) and the hub status line overlapping its own header
(v4.99.98). Both would have consumed session time; neither needed ears to find. Scan first, then listen.

---

## The script

**For build v4.99.98.** Rewritten after the mockup pass (v4.99.85–96) added six surfaces that had never
been listened to: the read strip, the coverage chips, the public-activity card, the hub status line, the
palette subtitles and the signed-out card.

**Why this needs you.** Everything in the accessibility work is right by construction and by test; nobody
has ever *listened* to it. And focus rings are one or two pixels, so they cannot be read reliably from a
screenshot — which means tab order cannot be certified from outside either.

Narrator solves both at once: it **speaks the name of every control as focus lands on it**. Tabbing with
it on turns an invisible problem into an audible one. One pass, both gaps.

Budget about 25 minutes. The passes are independent — stopping after two is a real result.

---

## Before you start

**Two defects were found and fixed by scanning for them, so you would not spend your ears on them:**

- Every **review row** announced only "button" — no way to tell a one-star complaint from a five-star
  thank-you. It now says *"1 star from Sana Tariq at Depilex DHA-2, 2 days ago. Waited 40 minutes past my
  appointment. Activate to open and reply."*
- The account drill-down's button said **"Open WhatsApp"** for every account, including Instagram.

That is the class of thing a scan can catch. What it cannot catch is order, silence, and whether a
sentence makes sense out loud — which is what the passes below are for.

---

## Setup (once)

1. **Start Narrator:** `Ctrl` + `Win` + `Enter`. The same keys stop it.
2. **Turn Scan Mode OFF:** `Caps Lock` + `Space` toggles it. Narrator usually starts with it **on**, and
   while it is on the arrow keys read the screen instead of Tab moving focus — which is not what we are
   testing. If pressing Tab reads a whole paragraph rather than naming one control, Scan Mode is on.
3. Open Unified Messenger and go to the **Dashboard**.

If Narrator talks too fast, `Ctrl` + `Alt` + `-` slows it. `Ctrl` alone silences the current sentence.

---

## What "wrong" sounds like

Five specific things. Everything else is a bonus.

| You hear | What it means |
|---|---|
| **"button"** with no name before it | An unlabelled control. A blind user has no idea what it does. |
| **"instance"** anywhere | Our internal word leaking. The product's word is *account*. A test bans it in visible strings, so any survivor is a gap the test cannot see. |
| A control announced that you **cannot see** | One of the invisible-but-focusable controls this app has shipped before — eight icons once reached the repo with an empty glyph, drawing nothing while staying focusable. |
| Focus **jumping backwards or sideways** on screen | Tab order not matching reading order. One collision of this kind was fixed in v4.99.84; this is how we find out whether more remain. |
| A number read with **no idea what it is** — "45", then nothing | A figure whose label is only visual. |

---

## Pass 1 · Dashboard tab order (5 min)

Click **Dashboard** in the left rail, then press `Tab` slowly, about twenty times, listening.

Expected order: the four overview entries (Dashboard, Analytics, Reviews, Reports), then your accounts down
the rail, then **Add account → Notification Hub → Settings**, then the page content on the right.

**Tell me if:** it skips one of the four overview entries · it jumps from the rail into the middle of the
page and back · any account row is silent or says only "button" · the order feels unrelated to what your
eye follows.

**New since the last script — listen for these on the cards:**

- The **coverage chip** on your Instagram accounts should read as a sentence, not two words: *"No message
  text. Every waiting customer is listed with how long they have waited…"*
- The **signed-out card** (Depilex Men DHA-2) should say why it has no figures, and its button should name
  the account — not just "button".
- The **public activity card** should announce all three counts and the caveat in one go.

## Pass 2 · The needs-a-reply queue (5 min)

Click **Needs reply**. Tab through the filter chips and into the first few customer rows.

**Listen for:** each row naming *who* and *which account* — something like "Open chat with &lt;name&gt; in
&lt;account&gt;", not "button" · the **Reply** and **Done** buttons being distinguishable from each other ·
the coverage notice reading as one sentence.

**New:** if everything is caught up, the empty state should mention signed-out accounts rather than a bare
"All caught up".

## Pass 3 · An open account, and the read strip (4 min)

Open any account from the rail — Instagram is the interesting one.

**Listen for:** the **read strip** at the bottom. It should read as one sentence: *"Reading. 18
conversations, 5 waiting, last read 28 minutes ago. Message text is never copied out of this client."*

**Tell me if:** it is silent · it reads as disconnected fragments · Narrator never reaches it while tabbing.

## Pass 4 · Reviews (4 min)

Click **Reviews** and tab into the queue.

**This is the pass most worth doing**, because the rows were fixed blind this morning and have never been
heard. Each should announce its star rating **first**, then who, where, when, and the review text.

**Tell me if:** any row still says just "button" · the rating is missing · the text is cut off mid-sentence.

## Pass 5 · Settings and one dialog (5 min)

`Ctrl` + `,` opens Settings. Tab from the section list into the content.

**Listen for:** each toggle naming what it does *and* its state (on/off) · the three health lines under
**Data and privacy** reading as sentences · nothing saying "instance".

Then click **Add account**, tab around it, and press `Escape`.

**Confirmed working 2026-09-05:** focus stays inside the dialog and Escape closes it. An earlier version of
this file claimed Escape did not work; see the retraction at the top.

---

## Reporting back

Rough notes are fine. The most useful shape is:

> Pass 1 — after Reports it jumped to the Re-sync button, then back to the accounts.
> Pass 4 — the review rows all just say "button".

Tell me what you heard and I will turn each one into a fix and a test. If a pass is entirely clean, say
that too — "nothing wrong in pass 3" is a real result, and it is the first time any of this will have been
confirmed by ear rather than by construction.
