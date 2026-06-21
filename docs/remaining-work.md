# Remaining work — prioritized backlog

**As of:** 2026-06-21 · **Baseline:** v4.28.1 · **Source of truth:** [MASTER-PLAN.md](MASTER-PLAN.md)

Everything in the v4.22–v4.24 UI/UX modernization plan and the reported bugs (delete crash, reorder
hang, opened≠replied, Google-Business sidebar, embed channels, Work-Queue→Needs-reply merge, new
channels + UA) is **shipped through v4.28.1**. What follows is the substantive roadmap that remains —
none of it is a quick win.

---

## P1 — high value, doable now (no live account)

### P1-A · Surface the real business-hours SLA on command-center cards
§8's centerpiece is a business-hours-aware **reply-latency SLA**. The engine exists
(`BusinessHoursCalculator`, `ThreadData.IsSlaBreached`) but is **dormant** inside the retired OCC.
The command center headline is "caught up %" (unread/direction-based), not on-time latency. Surface an
on-time-% and/or breach count on the cards so the primary metric matches the plan's intent. Ties to P1-B.

### P1-B · Decide the OCC's fate
The Work Queue / OCC kanban-triage-branch-filter subsystem is dormant (sidebar button collapsed in
v4.27.0) but still in the tree — maintenance weight + confusion. Either formally archive/remove it
(pages, Ctrl+Shift+Q, command-palette entries) **or** harvest its SLA logic into the command center
(ties to P1-A) and remove the rest. Document the decision.

### P1-C · Finish WCAG 1.4.1 coverage
The shape-distinct status glyph (✓/⚠/⨯, v4.25.0) is only on comfortable-density L0 cards. Compact cards
(which hide the %) and the Needs-reply rows still encode status by color/text without the shape glyph.
Add the non-color cue to those surfaces. Small.

### P1-D · Sticky-awaiting safety valve
v4.26.0's sticky-awaiting prevents opening a chat from falsely clearing "awaiting". Risk: a chat with no
DOM hint and a never-confirmed outbound could stay "awaiting" indefinitely. Add a confidence decay (after
N hours/days) or a periodic full reconciliation so a stuck chat can self-clear. Add tests.

---

## P2 — needs a live, logged-in account (user unblocks)

### P2-A · Issue 1 — unsaved-contact numbers + message gist · **now unblocked**
Awaiting rows show "Unsaved contact" with no preview for `@lid` chats not rendered in the WhatsApp
sidebar DOM. WhatsApp hides `@lid`→phone by design (resolvable only for known contacts via the `contact`
store). **Diagnostic-first:** ship a probe that reveals the `contact`-store schema for `@lid` records,
then build the resolver + a reliable last-message gist (bounded `message`-store read, not a hang-prone
cursor) in `whatsapp-adapter.js`. Verify against live data in the Needs-reply / awaiting UI.

### P2-B · Channel metric scrapers (Google / Telegram / Messenger / Instagram)
Embed slices exist (NullPlatformAdapter). Build per-channel scrapers that feed oversight metrics —
Google (rating / % responded / unanswered), Telegram (unread/awaiting), Messenger + Instagram (passive
read-only). Each must be tuned against a live logged-in account. Add real adapter cases in
`PlatformAdapterInternals.ResolveEnabledAdapter` + per-channel snapshot services.

### P2-C · Outbound staff-reply tone/quality scoring (Tier-2 Ollama)
§6/§8 AI Tier-2: read outbound staff replies and score tone/quality. Needs a message-content pipeline
(metric is counts-only today) + careful on-device prompting. Fully on-device, graceful degradation.

---

## P3 — larger / infrastructure

### P3-A · `IInstanceConnection` abstraction (§10 A-7)
The oversight/triage/notification layer couples directly to `InstanceSessionManager` + WebView2.
Introduce `IInstanceConnection` so the data layer doesn't depend on WebView2 directly. Cross-cutting,
no user-visible change, high regression surface.

### P3-B · Tier-1 lightweight AI (ONNX Runtime / Windows ML)
§6 Tier-1: small CPU model for better sentiment/classification than the Tier-0 heuristic, tiny RAM.
Runtime integration + model packaging + wiring between Tier-0 and Tier-2.

### P3-C · Post-suspend WebView2 RAM instrumentation
§10 RED partially closed (LRU cap 6, idle reaper, memory tiers shipped). Remaining: measure post-suspend
RAM at many-instance scale + CI stress fixtures to confirm the memory strategy holds.

### P3-D · True L1 channel-aware entity view
§9 L1: clicking an account should open a per-entity view with channel-aware tabs before the live WebView
(L2). Currently it jumps straight to L2. Depends on P2-B scrapers.

---

## Recommended order
1. **P2-A (Issue 1)** — top user-facing pain, now unblocked; diagnostic-first.
2. **P1-A + P1-B** — closes the biggest gap to the plan's core promise ("are staff replying *on time*").
3. **P1-C, P1-D** — quick polish.
4. **P2-B / P2-C** — as each channel/account becomes available.
5. **P3** — infra, when the product surface is settled.
