> ⚠️ **Superseded by [MASTER-PLAN.md](MASTER-PLAN.md)** (2026-06-16). Folded into the master plan (§3, Workstreams A/B); kept for history and evidence. The master plan is the living source of truth.

# Unified Messenger — Multi-Instance Research, Limitations & Improvements

**Focus:** the core purpose — *run as many WhatsApp instances on one system as required* — and every limitation (platform, functional, performance, UI/UX) that bears on it.
**Version:** v4.6.0 · **Date:** 2026-06-16 · **Constraint of record:** the product **does not use Meta/WhatsApp APIs** (WhatsApp Web via WebView2 only).
**Evidence tags:** 🟢 live app · 🔵 code · 📄 in-repo doc · 🌐 external research · ⚪ inference.

---

## 0. The trigger: the "Use here" dialog

The screenshot shows WhatsApp Web's **"WhatsApp is open in another window. Click 'Use here'…"**. This is **not an app bug** — it is WhatsApp's account-level session arbitration. Understanding it is the key to the whole product.

- 🔵 The app gives every instance an **isolated WebView2 profile** (`MessengerInstance.ProfileName` → `CoreWebView2ControllerOptions.ProfileName`), all under one shared user-data folder (`InstanceSessionManager` header comment). Profiles isolate cookies/IndexedDB/localStorage.
- 🔵 The app creates **exactly one WebView per profile**: `EnsureSessionCoreAsync` returns early if `_sessions.ContainsKey(instance.Id)`, and `IsProfileOwnedByOther(profileName, id)` forbids two instances sharing a profile. **So the app never double-opens the same session itself.**
- 🌐 Per-profile isolation is the industry-standard way to run multiple WhatsApp Web accounts ("each profile has its own cookies and login sessions … without interfering"). **Different numbers in different instances do not trigger "Use here."**
- ⚪ Therefore "Use here" appears when the **same number's session is contested from outside the app** — the user's phone-linked WhatsApp Web in a real browser, WhatsApp Desktop, another linked device, or the same number added to two instances and then re-claimed.

**Conclusion:** the conflict is a **WhatsApp platform behavior**, surfaced raw because the app does nothing to mediate it (see L-1 and I-1).

---

## 1. The hard ceiling on the core purpose

> "Open as many instances … as required."

This works **per distinct phone number**, but two ceilings apply:

### C-1 — WhatsApp's linked-device cap (platform, immovable without API) 🌐 🔴
- WhatsApp allows **1 primary phone + 4 linked/companion devices per account** (raised to **10 with Meta Verified**; **unlimited only via the WhatsApp Business API**, which is excluded by decision).
- **Implication:** a **single number** can run in **at most 4 app instances simultaneously** (plus the phone). The 5th instance of the *same* number forces a device to be unlinked, and contesting an active session yields the "Use here" prompt.
- **For different numbers there is no WhatsApp-imposed limit** — each number independently supports its own companion session. *This is the app's legitimate sweet spot.*
- Sources: [WhatsApp Help — linked devices](https://faq.whatsapp.com/647349420360876), [aisensy — 4/10/unlimited](https://m.aisensy.com/blog/whatsapp-business-more-than-4-devices/), [nexloo](https://nexloo.com/en/blog/can-i-have-more-than-4-linked-devices-on-whatsapp-a-look-at-the-new-updates/).

### C-2 — System resource ceiling (the real practical limit) 🔵 ⚪
- Each instance is a full Chromium WebView2 (~**100–250 MB** RAM typical for an active WhatsApp Web tab). N instances scale roughly linearly. 20 instances ⇒ several GB.
- 🔵 The app already has mitigations: `MaxConcurrentWebViews` (0 = unlimited, clamp 0–32), `EnforceSessionCapAsync` (LRU eviction), `StartupWarmMode` (`VisibleOnly` default), `EnableLazyWebViewLoading`, `EnablePerInstanceSleepUnload`. These are good but **undocumented to the user** and **not surfaced as guidance** (see I-4).
- 📄 The swarm sabotage doc lists **WebView2 memory leakage post-suspend = UNKNOWN/AMBER** — never measured. At high instance counts this is the dominant risk.

**Net:** "as many as required" is realistic for **dozens of distinct numbers** *only if* the app manages memory aggressively (hibernate inactive instances) and sets expectations. For a *single* number it is hard-capped at 4 by WhatsApp.

---

## 2. Documentation corpus — what exists (📄 inventory)

| Area | Docs | Value |
|---|---|---|
| Architecture | `docs/architecture/system-map.md`, ADRs (`001-mvvm`, `002-refresh-coordinator`, `003-ci-release`, `004-composition-root`, `occ-grid-layout`), `settings-ia-map.md` | Solid; current to v3.2 |
| Validation | `docs/validation/{completion-criteria,remaining-issues-log,v2-reaudit-checklist,wave11-checklist}.md` | Tracks resolved/deferred issues |
| Design system | `docs/design-system/{README,contrast-audit}.md` | Tokens, contrast |
| UX research corpus | `.crawl4ai/occ-research/**` (Front, Intercom, Zendesk, Freshdesk SLA, WinUI a11y, WebView2, Ollama) | Large scraped reference set |
| Swarm logs | internal adversarial-review logs (Stage1 agents, **Stage4 sabotage results**, exit handshake) | **Honest perf/risk catalogue** |
| Prior audit | `docs/ui-ux-research-and-recommendations.md` (this session) | P0–P3 UX plan + impl status |

**Gap:** none of the docs explain the **multi-instance limits (C-1/C-2)** or the **"Use here" behavior** to users or maintainers. That is this document's primary contribution.

---

## 3. All limitations

### Platform / account (immovable without API)
- **L-1 "Use here" not mediated** 🔵🔴 — the app surfaces WhatsApp's raw conflict dialog; the user must manually click "Use here" every time a session is contested. No auto-claim, no friendly explanation. *(Most-felt limitation given the screenshot.)*
- **C-1 4-device cap** 🌐🔴 — single number ≤ 4 instances.
- **L-2 No same-number duplicate guard** 🔵🟠 — adding the same number to two instances is allowed (two profiles → two linked devices), silently consuming device slots and inviting contention. No detection/warning.
- **L-3 Ban-risk from WhatsApp-Web automation** 🌐🟠 — DOM scraping is fingerprintable; bulk/automation risks number bans (covered in the UI/UX audit). No in-app responsible-use guidance.

### Functional / performance (fixable)
- **L-4 Uncoalesced telemetry ingress** 📄🔴 — Stage4: "sync JSON parse per message; no C# channel coalescer" → RED under burst. At many instances, message bursts multiply.
- **L-5 Unthrottled DOM MutationObservers** 📄🔴 — Stage4 rated survivability **2/10** at 5000 mutations/2s. Each instance runs observers; N instances compound UI-thread pressure.
- **L-6 WebView2 post-suspend memory** 📄🟠 — never measured; the scaling-critical unknown for "many instances."
- **L-7 Layout passes under rapid filter** 📄🟠 — OCC likely >2 measure passes under rapid date-range/Live-Historical toggling.
- **L-8 No per-instance health/resource visibility** 🔵🟠 — `ResourceMonitorService`/`InstanceConnectionStatusService` exist, but the user can't see per-instance RAM or "asleep/awake" state to manage many instances.

### UI/UX (fixable — see also the P0–P3 audit)
- **L-9 SLA metric integrity** — ✅ fixed in v4.5.0 (backfilled excluded; at-risk window).
- **L-10 Sidebar at scale** 🔵🟠 — the account list (`WorkspaceSidebar`) is a flat list; with 20+ instances it needs grouping/search/pinning (only branch grouping exists in OCC, not the sidebar nav).
- **L-11 No bulk instance management** 🔵🟠 — add/rename/delete are one-at-a-time; no multi-select, no "sleep all but visible," no import templates for many numbers.
- **L-12 Connection-state ambiguity** 🟢🟡 — "Connected · syncing" is shown, but a contested ("Use here") or logged-out instance isn't clearly differentiated in the sidebar.
- **L-13 First-run/empty states & density** — partially addressed (P1 pass): KPI-first layout, empty-state CTA, view-mode label, non-color SLA cues shipped v4.6.0.

---

## 4. Improvements (prioritized)

Effort S/M/L/XL · Impact ★–★★★.

### I-1 · Mediate the "Use here" conflict 🔴 ★★★ · M
Inject adapter JS to **detect the conflict dialog and auto-click "Use here"** (claim the session in the app), with a short debounce to avoid a claim war if the user is also active elsewhere. Surface a one-line app banner ("Reconnected this window") instead of the raw WhatsApp modal. *Directly removes the screenshot's friction.* 🔵 hook point: `whatsapp-adapter.js` + `WhatsAppIngressHandler`.

### I-2 · Same-number duplicate guard 🟠 ★★ · S
On Add/Import instance, detect a number/profile already linked in another instance and **warn** ("This number is already in instance X; WhatsApp allows 4 linked devices") before consuming a slot. 🔵 `InstanceRegistryService.CreateProfileName` / add flow.

### I-3 · Aggressive hibernation for scale 🔴 ★★★ · M
Make `EnablePerInstanceSleepUnload` the **default** with a sensible idle timer, and unload background instances beyond `MaxConcurrentWebViews`. This is what makes "many instances" actually viable on real RAM. 🔵 `InstanceSessionManager.EnforceSessionCapAsync` + settings default.

### I-4 · Per-instance resource & state HUD 🟠 ★★ · M
Surface, per instance: awake/asleep, RAM, connection (Connected / Syncing / **Contested** / Logged-out). Lets a power user run many and see what to sleep. 🔵 `ResourceMonitorService` + sidebar badges.

### I-5 · Coalesce telemetry ingress 🔴 ★★ · M
Add a channel coalescer (latest-wins per conversation, debounced) between `HandleWebMessage` and triage, per Stage4 L-4. Critical as instance count rises. 🔵 `WebMessageIngressService` / `MessageTriageService`.

### I-6 · Throttle DOM MutationObservers 🔴 ★★ · M
Batch/throttle observers in `adapter-core.js`/`whatsapp-adapter.js` (rAF or time-bucketed) to survive DOM floods (Stage4 L-5). Multiplied across instances, this protects the whole app.

### I-7 · Measure & cap WebView2 memory 🟠 ★★ · M
Instrument post-suspend memory (Stage4 L-6); enforce a soft cap that triggers hibernation. Turns the scaling unknown into a managed budget.

### I-8 · Sidebar for scale 🟠 ★★ · M
Group accounts (Personal/Professional/branch), add **search/filter** and **pinning/reorder** in the nav sidebar; collapse asleep instances. 🔵 `WorkspaceSidebar` + `WorkspaceSidebarMenuPlanner`.

### I-9 · Bulk instance management 🟡 ★ · L
Multi-select; "sleep all background"; CSV/template import of many numbers; bulk rename/branch assignment. 🔵 `InstanceRegistryService` import/export already exists (experimental) — extend.

### I-10 · Responsible-use + limits disclosure 🟠 ★★ · S
One in-app note: WhatsApp Web, **4 linked devices/number**, no bulk automation, resource expectations. Sets correct expectations and reduces ban risk. (P0-2 in the UX audit.)

---

## 5. Honest verdict on the core purpose

- **Many *different* numbers, one instance each:** ✅ supported by design (per-profile isolation); the practical limit is **RAM**, which the app can manage far better (I-3, I-7). With hibernation, dozens are realistic.
- **One number across many instances:** ❌ hard-capped at **4 linked devices** by WhatsApp (10 with Meta Verified) — *not solvable in-app without the Business API, which is excluded.* The right response is **honest disclosure (I-10) + duplicate guard (I-2) + auto-"Use here" (I-1)**, not a promise the platform won't keep.
- **The "Use here" papercut** is the highest-ROI fix (I-1): small JS change, removes a constant manual step that the core workflow hits repeatedly.

---

## 6. Suggested sequencing
1. **I-1 auto-"Use here"** + **I-10 disclosure** (immediate friction + honesty).
2. **I-3 hibernation default** + **I-7 memory cap** (makes scale real).
3. **I-5/I-6 ingress + observer throttling** (stability under many instances / bursts).
4. **I-2 duplicate guard**, **I-4 per-instance HUD**, **I-8 sidebar at scale**.
5. **I-9 bulk management**.

## 7. Sources
WhatsApp limits: [WhatsApp Help — linked devices](https://faq.whatsapp.com/647349420360876) · [aisensy 4/10/unlimited](https://m.aisensy.com/blog/whatsapp-business-more-than-4-devices/) · [nexloo](https://nexloo.com/en/blog/can-i-have-more-than-4-linked-devices-on-whatsapp-a-look-at-the-new-updates/) · [chati device-limit fix](https://chati.ai/blog/whatsapp-device-limit-reached-how-to-fix-multi-device-errors).
Multi-account method (profiles): [technastic](https://technastic.com/multiple-whatsapp-web-account-chrome/) · [WADesk](https://wadesk.io/en/tutorial/how-to-open-multiple-whatsapp-web-accounts).
In-repo: `docs/architecture/system-map.md`, `docs/validation/remaining-issues-log.md`, `docs/ui-ux-research-and-recommendations.md`.
