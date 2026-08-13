# Findings — Per-view state matrix

§6.4 asks for 6 pages, ~20 controls and ~15 dialogs each exercised across ~14 states. **That full matrix
was not covered.** What follows is what *was* exercised, what it showed, and — stated plainly — what was
not touched, so this is not mistaken for a completed sweep.

## Approach

Rather than sample the matrix evenly, I targeted the cells most likely to hide defects **and impossible to
reach with the owner's own data**: many accounts, very long names, and non-Latin / right-to-left text. The
owner's 11 accounts are all short Latin names, so none of that had ever been rendered.

The registry was replaced with a synthetic 20-account set (owner data backed up and hash-verified first,
restored and re-verified after — all files matched):

| Stress case | Value used |
|---|---|
| Very long name | `Depilex Gulberg III Main Boulevard Lahore Flagship Salon and Spa Reception Desk Line Two` (87 chars) |
| Right-to-left | `ڈیپیلیکس ایف الیون اسلام آباد` (Urdu — the owner's own market) |
| CJK | `深圳市宝安区美容院前台` |
| Accents + apostrophe | `Café Ñandú — Señor Müller's Salon` |
| Emoji incl. ZWJ sequence | `Depilex 💇‍♀️ F-11 ✨ VIP` |
| Minimal | `A` |
| Volume | padded to **20 accounts** |

## Result: this cell of the matrix is CLEAN

| Check | Result |
|---|---|
| App survives 20 accounts incl. RTL/CJK/emoji | **yes** — alive, 255 MB, no crash, no error in `app.log` |
| All stress names render | **yes** — every one present in the UI tree |
| Accessible names correct for non-Latin | **yes** — e.g. `深圳市宝安区美容院前台, WhatsApp` |
| Layout overflow past the window | **0 elements** extend beyond window bounds |
| Clipping of the 87-character name | none observed; no element exceeded the window rect |
| Sidebar reachability at 20 accounts | **all reachable** — 6 laid out initially, 12 after scrolling to the end, 11 newly materialised |
| Virtualisation | working — unrendered rows report infinite bounding rects and materialise on scroll |

**AGENTS.md lists "Sidebar-rail density at very large counts" as an open Phase 3 item.** At 20 accounts it
holds up: the rail scrolls, virtualises correctly, and every account — including the 87-character and RTL
names — is reachable. That item can be considered addressed at this scale. It was **not** tested at 50+.

A methodological note worth keeping: the first measurement pass reported "3 of 20 accounts" and looked like
a serious finding. It was an artefact — unrendered rows return `∞` bounding rectangles, which the naive
cast silently dropped. Handling infinity explicitly turned a false S2 into a clean result. **Virtualised UI
must be measured by scrolling, not by a single snapshot.**

## What was NOT covered — the majority of the matrix

States exercised somewhere in this audit:
- **first-run / empty** (v4.99.14, on a genuinely clean install)
- **no-data-yet** (empty states observed: "No accounts connected yet", "No professional accounts yet",
  "No Google Business account connected", "Reviews — Not scanned yet")
- **one account** and **many accounts (20)**
- **long names, non-Latin, RTL, CJK, emoji**
- **stale / degraded session** (v4.99.3, v4.99.8)

States **not exercised at all**:
- **offline** — no network-loss test was run. For an app whose entire input is web clients, this is the
  most conspicuous gap in the list.
- **error** states beyond corrupt-file and failed-read
- **quiet hours active**
- **AI on vs AI off** — `EnableLocalAi` was left at its stored value throughout; the AI-off degradation
  path (heuristic fallback) was never observed
- **all-caught-up** — the owner's data never reached zero awaiting, and no synthetic all-caught-up state
  was constructed, so the "No customers are waiting" hero branch is **unverified in the running app**
- **everything-on-fire** beyond the owner's real 343-awaiting backlog
- **date-range interactions** across the window selector

Surfaces **not enumerated for state at all**:
- **~15 dialogs.** Only `AddInstanceDialog` was opened and inspected (v4.99.2). The other ~14 —
  delete, rename, edit-metadata, set-location, workspace-management, change-icon, weekly-report,
  account-detail, auto-update, pin-to-taskbar, confirm-permanent-delete — were **never opened**. Dialogs
  are where dead ends and missing empty states typically hide, and this is the single largest untested
  area of the product.
- **Reviews and Reports pages** were navigated through during the leak stress but never inspected
  state-by-state.
- **~20 controls** were not individually exercised.

## Honest summary

One high-value cell of the matrix was covered thoroughly and came back clean. The bulk — dialogs, offline,
AI-off, all-caught-up, quiet hours — remains untested. Treating this domain as "done" would be wrong.
