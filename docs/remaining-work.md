# Remaining work — prioritized backlog

**As of:** 2026-06-21 · **Baseline:** v4.28.1 · **Source of truth:** [MASTER-PLAN.md](MASTER-PLAN.md)

Everything in the v4.22–v4.24 UI/UX modernization plan and the reported bugs (delete crash, reorder
hang, opened≠replied, Google-Business sidebar, embed channels, Work-Queue→Needs-reply merge, new
channels + UA) is **shipped through v4.28.1**. What follows is the substantive roadmap that remains —
none of it is a quick win.

---

## P1 — high value, doable now (no live account) — ✅ ALL DONE

### P1-A · Surface the real business-hours SLA on command-center cards — ✅ done (v4.31.0)
The §8 SLA was computed in `OversightRollupBuilder` (`ThreadData.IsSlaBreached` + per-location
`BusinessHoursCalculator`) but then **discarded** in favour of the unread-based caught-up %. Added
`OversightEntityHealth.SlaBreachedCount` (open in-window threads past their business-hours reply SLA),
computed independently of the caught-up override, and surfaced it as a **"N late"** card sub-metric next
to urgent/dropped. 0 when there's no thread data, so it degrades gracefully.

### P1-B · Decide the OCC's fate — ✅ decided
**Decision: keep the OCC UI dormant (already retired in v4.27.0), harvest its SLA logic into the command
center.** The valuable SLA engine (`BusinessHoursCalculator`, `ThreadData.IsSlaBreached`,
`OperationalThresholds`) lives in shared Models/Services — *not* inside the OCC UI — so P1-A surfaced it
without touching the kanban. The OCC pages stay dormant + reversible (Ctrl+Shift+Q / palette) rather than
deleted, to avoid churn; a later cleanup can remove them once the command center fully replaces them.

### P1-C · Finish WCAG 1.4.1 coverage — ✅ done (v4.30.0)
Status glyph added to compact cards + a warning glyph on Needs-reply rows. Status is never colour-alone.

### P1-D · Sticky-awaiting safety valve — ✅ done (v4.30.0)
Stickiness only inherits while the chat's last activity is within 7 days (`StickyAwaitingMaxAge`); past
that an unconfirmed-clear is allowed through so it can't get permanently stuck. Regression test added.

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
