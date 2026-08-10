# Findings — ChatEntryParser (per-field extraction from scraped JSON)

`ChatEntryParser.ParseConversations` is where every oversight number begins: both ingestion paths (the
store bridge and the IndexedDB fallback) and the startup backfill funnel through it. It parses JSON
produced by a web page that can change shape without notice.

## Producer parity: CLEAN

AGENTS.md warns that a `ChatEntry` field emitted by one JS producer and not the other would make metrics
differ depending on which path ran — a wrong-number bug with a plausible cause. **Checked, and it holds.**

Both `whatsapp-adapter.js:1359-1368` (IndexedDB scan) and `whatsapp-store-bridge.js:680-688` (in-memory
scan) emit the identical set, in the same order:

| Field | adapter.js | store-bridge.js | parser reads |
|---|---|---|---|
| `conversationKey` | ✅ | ✅ | ✅ |
| `customerName` | ✅ | ✅ | ✅ |
| `unreadCount` | ✅ | ✅ | ✅ |
| `lastActivityTimestampUtc` | ✅ | ✅ | ✅ |
| `lastMessagePreview` | ✅ | ✅ | ✅ |
| `awaiting` | ✅ | ✅ | ✅ |
| `lastMessageFromMe` | ✅ | ✅ | ✅ |
| `contactPhone` | ✅ | ✅ | ✅ |

Both also emit `lastInboundBody`, `lastInboundTimestampUtc` and `inboundCount`, which the parser ignores —
consistent between producers, so no divergence there either. **No parity defect found.**

Both producers additionally wrap each chat in its own `try/catch` with the comment *"Skip a malformed chat
rather than failing the whole scan."* That intent is correct — and it is exactly what the C# side failed
to honour, which is F-PARSE-01.

---

### F-PARSE-01 — One wrong-typed field discarded every conversation in the scan, zeroing an account's metrics

- **Severity:** S2
- **Confidence:** confirmed (reproduced by unit test against the real parser, before and after)
- **Where:** `UnifiedMessenger/Services/Oversight/ChatEntryParser.cs:45,52,53,54,59` (pre-fix `GetString()`)
- **Where:** `UnifiedMessenger/Services/Oversight/ChatEntryParser.cs:51` (pre-fix `TryGetInt32`)
- **Status:** **FIXED** in `v4.99.7`.
- **User-visible symptom:** An account's oversight data vanishes completely — not degraded, gone. The
  dashboard shows "no activity" and the account drops out of the caught-up average, so the owner has no
  idea customers may be waiting at that branch. The trigger is a *single* chat whose scraped shape
  changed; the other 849 chats are collateral. Because the guards found in the previous increment mean no
  false number is shown, this is S2 rather than S1 — the app goes quiet rather than lying.
- **Repro:**
  1. Feed `ParseConversations` an array of 5 conversations where one has
     `"lastActivityTimestampUtc": 1754812800000` (an epoch number instead of an ISO string).
  2. Pre-fix: `JsonElement.GetString()` throws `InvalidOperationException`, which escapes
     `TryParseConversation` **and** `ParseConversations`, so the caller receives an exception and **all 5**
     conversations are lost.
  3. Post-fix: that one row is skipped, the other 4 parse. Pinned by
     `ChatEntryParserResilienceTests.OneBadRowAmongManyGoodOnesCostsOnlyItself` and
     `…ANumericTimestampSkipsOnlyThatRow_AndDoesNotDiscardTheScan`.
- **Root cause:** Two `System.Text.Json` APIs that are less forgiving than they look, used without
  `ValueKind` guards.
  - `GetString()` throws `InvalidOperationException` when the element is a number, boolean, object or
    array. It returns null only for `JsonValueKind.Null`. So `TryGetProperty(...) ? x.GetString() : null`
    is safe against a *missing* field and unsafe against a *retyped* one — and a scraper emitting an epoch
    number instead of an ISO string is a realistic change, not a contrived one.
  - **`TryGetInt32` also throws** when the element is not a Number. The `Try` prefix covers only whether
    the value fits in an `int`, not whether it is a number at all. I did not know this going in; **my own
    test caught it** — `UnreadCountThatIsNotAnIntegerDefaultsToZero` failed on the first run against the
    partially-fixed parser, revealing that a string-typed `unreadCount` still cost the whole row.
  Neither `TryParseConversation` nor `ParseConversations` had a `try/catch`, so a throw from any of the six
  reads propagated out and destroyed the partially-built list.
- **Fix applied:** All reads go through `ReadString` / `ReadInt` helpers that check `ValueKind` first and
  degrade to `""` / `0`. `ParseConversations` additionally wraps each row in its own `try/catch` as belt
  and braces, matching what both JS producers already do, and logs a count of skipped rows so a shape
  change is visible rather than silent. The root element's `ValueKind` is now checked too, so a
  non-object root returns an empty list instead of throwing.
- **Blast radius:** `ChatEntryParser` only — but that is every oversight number, via all three callers
  (store bridge, IndexedDB fallback, startup backfill). Behaviour on well-formed input is unchanged, which
  the 52 passing tests across the parser-adjacent suites confirm.
- **Evidence:** `ChatEntryParserResilienceTests`, 16 tests, green. The `TryGetInt32` defect was found by a
  failing test rather than by reading, which is the strongest form of evidence available here.

---

### F-PARSE-02 — A missing `awaiting` field silently downgrades the product's headline metric to a worse definition

- **Severity:** S2 (latent — not firing today)
- **Confidence:** confirmed (behaviour), `likely` (that it would go unnoticed — now mitigated)
- **Where:** `UnifiedMessenger/Services/Oversight/ChatEntryParser.cs:55-57` (pre-fix)
- **Status:** **MITIGATED** in `v4.99.7` — the fallback is retained deliberately, but it now announces itself.
- **User-visible symptom:** None today; both producers emit `awaiting`. If either stopped, the awaiting
  count — the number this product exists to report — would silently switch to a different and
  **documented-as-inferior** definition, with no visible change. The codebase states the problem plainly
  at `OversightSnapshotReader.cs:294-298`: unread is *per-device read state*, so reading a chat on a phone
  clears it without anyone having replied, and it lags per linked device, meaning two installs watching
  the same accounts would disagree. Awaiting is meant to be direction-based (last message not from us),
  which syncs identically everywhere. The owner would see plausible-looking numbers that quietly changed
  meaning.
- **Repro:** `static-analysis-only` for the trigger; the fallback behaviour itself is pinned by
  `MissingAwaitingFallsBackToUnread_WhichIsTheDocumentedDegradedBehaviour`.
- **Root cause:** `conversation.TryGetProperty("awaiting", out var a) ? a.ValueKind == JsonValueKind.True : unread > 0`
  — a silent semantic fallback with no signal. Reasonable as resilience, dangerous as silence.
- **Fix applied:** The fallback is **kept**, because removing it would be worse: without it a producer
  change would zero the awaiting count entirely rather than degrade it. Instead `ParseConversations` now
  counts rows that lacked an explicit boolean `awaiting` and logs a warning naming the consequence
  ("per-device and less accurate… the scraper's output shape has probably changed"). Also tightened: an
  explicit `null` no longer counts as "present", and `HasExplicitAwaiting` requires a real `True`/`False`.
- **Blast radius:** `ChatEntryParser`. No behaviour change on today's input.
- **Evidence:** `ExplicitAwaitingFalseBeatsANonZeroUnreadCount` pins the important direction — an explicit
  `awaiting: false` must win over `unreadCount: 7`, or a chat we already replied to would be resurrected
  into the backlog.

---

## What I could not determine

- **The log is the only place these signals surface.** Both fixes make shape changes *diagnosable*, not
  *visible*. A non-technical owner will not read `app.log`, so a degraded scraper still presents as a quiet
  account on screen. This is the same gap as F-SNAP-02 and is not closed by this increment.
- **No live schema-break was simulated.** The malformed inputs were constructed in unit tests. I did not
  modify an injected scraper to emit a changed shape and observe the app end-to-end. That remains the
  definitive test and was not run.
- **`lastInboundBody` / `lastInboundTimestampUtc` / `inboundCount` are emitted by both producers and read
  by neither** (this parser ignores them). Whether some other consumer reads them from the raw JSON, or
  whether they are simply dead payload, was **not** traced.
- **`WhatsAppBackfillProvider`'s use of this parser was not audited.** It is the third caller; I verified
  it funnels through `ChatEntryParser` as AGENTS.md claims, but did not review what it does with the
  results.
