# Product-hardening audit — final report

**Merged to `main` and released as `v4.99.27`** (41 commits from `audit/product-hardening`).
**Two sessions, 30 numbered increments,** `v4.99.1` → `v4.99.27`. Build clean at 0 warnings throughout.
**337 tests in the audit regression suite, all green** (164 from session 1, 173 added in session 2).

> **If you are upgrading an existing install, `v4.99.27` must be installed manually once.** The
> auto-update fix cannot deliver itself — see §3. This is the single most important operational fact in
> this report.

The brief asked for a product that could be put on sale tomorrow: no crashes, no dead ends, **no wrong
numbers**, no half-built promises — judged against *"a stranger paid for this and is using it
unsupervised."* This is what was found, what was fixed, and what is still not known.

---

## 1. The verdict

**The product is materially closer to sellable than it was, and it is not there yet.**

What changed: **27 findings at S1 or S2 were closed** across the two sessions (session 1: 8 S1 and 12 S2;
session 2: 1 S1 and 6 S2), including several that made the product actively lie to its owner and one where
a headline feature had never worked at all. The
numbers on the main screen can now be trusted in the cases that were checked, and the checks are pinned by
tests that fail loudly rather than by prose.

What has not changed: the areas listed in §5 remain unverified, and two of them — a long soak and a real
screen reader — are the kind that only reveal themselves in use. Nothing found in session 2 suggests the
product is fragile; several things found suggest that **where nobody has looked, defects are still likely**,
because that pattern held on every single pass. Every domain opened in session 2 produced at least one real
finding, including the two opened specifically because they were expected to be clean.

**The single most important structural observation:** almost every serious defect in this audit was a
*reassuring* number or message that was reassuring **because data was missing**. That is the failure mode
this product is most prone to, and it is worth treating as a design rule rather than a list of bugs.

---

## 2. What the audit found, by session

### Session 1 (`v4.99.1` → `v4.99.20`, increments 1–22)

| | Closed | Open at handoff |
|---|---|---|
| S1 | 8 | 0 |
| S2 | 12 | 3 |
| S3 | 5 | several, all recorded |

The five most serious:

1. **The rounding lie — 7 sites, 4 files.** `Math.Round` turned 996/1000 into **100%**, so a card showed a
   green *"100% caught up"* beside *"4 awaiting"*, and the KPI tile read *"SLA met 100%"* beside a reply
   set containing a breach. Fixed by `MetricMath.HonestPercent`, which reserves 100 for "nothing
   outstanding" and 0 for "nothing done".
2. **The hero blamed the wrong branch.** `oldest 75d · Depilex DHA-2 WhatsApp` read as one sentence, but
   that account's own card said `Longest wait: 50d`. Two bugs underneath: the name came from "most
   awaiting" while the duration came from "oldest anywhere", measured over **different windows**.
3. **The ARM64 installer shipped a binary 16 versions stale**, stamped as current. Now blocked at compile
   time by a payload guard that caught a live instance on its first run.
4. **Updating deleted the log and the settings-recovery file** — and auto-update is on by default.
5. **WhatsApp's own notice account counted as a waiting customer**, 26 days old and impossible to clear.

### Session 2 (`v4.99.21` → `v4.99.27`, increments 23–29)

Seven passes, each taken from the handoff's own priority list. **Every one produced a finding.**

| Increment | Pass | Findings |
|---|---|---|
| 23 | DST boundaries | **F-METRICS-10 (S2)** date windows an hour wrong on both transition days · **F-METRICS-11 (S4)** projection skew, WONTFIX with a measured bound |
| 24 | Offline behaviour | **F-OFFLINE-01 (S1)** auto-update had never worked · **F-OFFLINE-04 (S2)** accounts never retried after a dropped connection · **-02/-03 (S3)** raw error codes and a hang risk |
| 25 | Dialogs opened live | **F-DIALOG-01 (S2)** icon picker announced 25 identical "button"s · **F-DIALOG-02 (S3)** unnamed customer rows |
| 26 | Durability | **F-DURA-01 closed** — the settings reset is finally told to the user · **F-DURA-03 (S3)** racing startup prompts, one silently swallowed |
| 27 | State matrix | **F-STATE-01 (S2)** "all caught up" while a branch was unmeasured · **F-STATE-02** caught-up-on-a-range read as caught-up-on-everything |
| 28 | Semantic colours | **F-A11Y-04 (S2)** every status colour failed contrast in one theme |
| 29 | Tab order | **F-A11Y-05 (S3)** account rows never announced that they could be opened |

**Session 2 severity: 1 × S1, 6 × S2, 5 × S3, 1 × S4 (accepted).**

---

## 3. The three worst things found in session 2

### Auto-update had never worked, and failed invisibly — F-OFFLINE-01 (S1)

`TryVerifyDownloadedInstaller` required a trusted Authenticode signature. **Nothing in the repository signs
anything** — no `SignTool` in either `.iss`, no signing step in CI, and `Get-AuthenticodeSignature` on the
built installer reports `NotSigned`. Proven rather than reasoned: supplying a *correct* SHA-256 to the old
code still answered *"Downloaded installer is not Authenticode-signed."*

With the shipped defaults (`EnableAutoUpdate = true`, `PromptBeforeAutoUpdate = false`) that meant: check
GitHub at every launch, download the **entire installer**, reject it, delete it, and throw inside a
discarded task where nothing logged and nothing showed. Forever. A headline feature was dead and silent
while consuming bandwidth on every start.

Now admitted on **either** a verified digest **or** a trusted signature, never neither. *The tradeoff is
recorded rather than buried:* the digest proves the file arrived intact, not who built it. Authenticode
remains the stronger control and should be restored to mandatory once a certificate exists.

> **Correction, added after shipping `v4.99.27`.** The fix **cannot deliver itself.** The broken verifier
> lives in the *client*, so every installation older than `v4.99.22` still rejects the release that fixes
> it. Confirmed on the owner's own machine, still on `v4.99.13`: publishing `v4.99.27` changed nothing,
> and *Check for updates* produced "Downloaded installer is not Authenticode-signed" — a string that no
> longer exists on any code path in the fixed build, which is what proves the dialog came from the old
> client and not from a regression.
>
> **`v4.99.27` therefore has to be installed manually, once, on every existing install.** After that,
> updates work. The fixed verifier was then checked against the real published artifact — accepted with
> its `.sha256`, rejected without one — and a full manual install was performed with all user data intact
> byte-for-byte. Detail in `findings/offline.md`.

### "You're all caught up" while a branch was not being measured — F-STATE-01 (S2)

The hero and the shift briefing decided this from `totalAwaiting == 0` alone. **An account whose read fails
contributes zero awaiting** — there is nothing to count — so a branch dropping out of the rollup pushed the
total *down*, toward the reassuring answer. The card directly underneath already said "couldn't read"; the
headline above it never looked.

Only reachable because the owner's real data has never hit zero awaiting, which is exactly why the
state-matrix cell had never been exercised.

### Every status colour failed contrast in one theme — F-A11Y-04 (S2)

Success, warning and danger were a single value shared between light and dark. Success measured **2.28:1**
and warning **2.15:1** on the light card — worse than the brand regression that prompted the original
contrast audit — and danger **2.84:1** on the dark card. A shared value was never possible: the luminance
ranges required for white and for the dark card do not overlap, which is now a test.

*This one nearly shipped a worse bug than it fixed:* moving the colours into theme dictionaries broke
`UmSemanticBrushes.Get`, whose fallback was a **silent `Colors.Gray`**. Caught before publishing.

---

## 4. What was verified clean — roughly half the value

Recording these is what stops the next person re-auditing them.

| Area | Verdict |
|---|---|
| Sparkline / response-time / analytics day bucketing across DST | Clean — calendar-date arithmetic, DST-immune, now tested against synthetic 23- and 25-hour days |
| App responsiveness with every web client failing | Clean — `Responding=True` throughout, UI fully enumerable |
| Ollama / local AI when offline | Unaffected by design (localhost), states already have plain-English copy |
| AI on vs off | Clean — the heuristic is used for AI-off *and* AI-pending, so there is no blank strip |
| Quiet hours | Clean — suppression sits *before* `Evaluate`, so the alert edge survives and fires when quiet hours end |
| Rename / Delete / Workspace-management dialogs | Clean — 0 unnamed controls; the delete dialog's copy is genuinely good |
| Tab order | Clean — 51 stops, 0 unnamed, cycle closes, order follows the screen |
| WCAG 1.4.1 (colour never the only signal) | Clean — every status surface carries words or a glyph |
| Memory over **3.6 hours** | **Clean — no leak.** Both app and WebView2 asymptote and hold; handles flat across the whole run (§6) |
| Oversight data leaving the machine | **Clean, and checked rather than assumed.** Every `.cs` file touched since `v4.99.21` was grepped for outbound primitives (`HttpClient`, `WebClient`, sockets, `Post/PutAsync`, `WebRequest`). The only hits are in `GitHubUpdateService` — the pre-existing updater, which *fetches* release metadata and an installer and sends no derived data. Session 2's changes there were error wording, a download timeout, partial-file cleanup and logging |

---

## 5. What is still not known

Stated plainly, because the brief asked for a sellable product and these are the gaps between here and
that claim.

### Still open as findings

| ID | Sev | What |
|---|---|---|
| **F-SNAP-02** | S2 | A *degraded* read (store bridge failed, IndexedDB succeeded) is still visible only in the log |
| ~~**F-OFFLINE-05**~~ | ~~S3~~ | **WITHDRAWN — the finding was wrong, not the code.** The capture was taken on `4.99.21.0`; the fix shipped in `v4.99.22`. Re-verified live on `v4.99.27` |
| **F-OFFLINE-06** | S3 | "Open the account once to finish loading" is shown when the real cause is no network |
| **F-ORCH-06** | S3 | Settings and the account context menu speak "instance" as their **accessible names** |
| **F-METRICS-11** | S4 | End-of-day projection skew — deliberate WONTFIX, bound measured at under 2% |

### Untested, and material

- **A real screen reader was never run.** Both sessions read the UIA tree those tools consume — in session
  2, in focus order — which is much closer to the real thing than a static dump. Nobody listened to it.
- ~~A genuine multi-hour soak.~~ **Done — 3.6 hours, no leak (§6).** What remains untested is a soak
  **under account churn**: the run was idle, so a leak that only appears when accounts cycle, re-sync or
  navigate would not have shown. The ~3.1 GB WebView2 footprint is also unaddressed — stable, but large.
- **The updater's own network path** was never exercised against a real outage — the dead-proxy technique
  affects only WebView2.
- **A network drop while pages are already loaded**, which is the commoner real-world case. No
  `NavigationCompleted` fires, so the v4.99.22 retry does not cover it, and the app may keep reporting
  "Connected" while the web client is offline.
- **Five of twelve dialogs were never opened** — SetLocation, EditInstanceMetadata, PinToTaskbar,
  AutoUpdate, and ConfirmPermanentDelete (deliberately not reached, being the second step of permanent
  deletion on a machine holding real accounts).
- **The `SystemFillColor*` brushes were not measured**, and they are used *more* than the app's own tokens
  (36/33/29 references vs 12/12/11).
- **Focus order outside the dashboard shell** — Settings, Analytics, Reviews, Reports and every dialog.
  Shift+Tab and modal focus containment are untested.
- **The all-caught-up hero has never been rendered**, only decided. It needs zero awaiting across every
  account, which cannot be staged without overwriting live data.
- **ARM64 has never been published or installed.** The payload guard correctly blocks building that
  installer until someone does.
- **The uninstall data-erasure option** (v4.99.14) is unverified at runtime — confirming it would have
  destroyed the owner's data. It is the one change in the branch not proven by execution.

---

## 6. The soak — 3.6 hours, and it answers the question

Session 1's leak check was an 11-minute accelerated proxy and explicitly could not see a slow leak. This
one ran **219 minutes (3 h 39 m), 439 samples at 30-second intervals**, on the owner's real data, sampling
`UnifiedMessenger` plus every `msedgewebview2` process. Raw data: `docs/audit/soak.csv`.

It happened to capture the transition that matters. The first ~22 minutes were idle at 6 WebView2
processes and ~455 MB; then the accounts loaded and WebView2 went to **21 processes**, reproducing the
1.7–2.0 GB footprint session 1 observed and could not explain.

Mean per 30-minute bucket, after the accounts finished loading:

```
  0- 29 min   app 335.6 MB    WebView2 2112.2 MB    handles 1652
 30- 59 min   app 353.7 MB    WebView2 2343.3 MB    handles 1668
 60- 89 min   app 372.3 MB    WebView2 2699.2 MB    handles 1662
 90-119 min   app 377.2 MB    WebView2 2903.5 MB    handles 1673
120-149 min   app 373.1 MB    WebView2 3085.2 MB    handles 1667
150-179 min   app 379.7 MB    WebView2 3126.7 MB    handles 1674
180-209 min   app 377.3 MB    WebView2 3114.7 MB    handles 1667
210-239 min   app 380.1 MB    WebView2 3086.7 MB    handles 1682
```

**Verdict: no leak. Both curves are asymptotic, not linear.**

- **The app itself** climbs 335 → 377 MB over the first 90 minutes and then sits between 373 and 380 MB
  for the remaining two hours. Flat.
- **WebView2** climbs ~1 GB over the first two hours as the web clients accumulate content, then
  **plateaus**: the last three buckets are 3126.7, 3114.7, 3086.7 — level, then very slightly *down*.
- **Handles are flat across the entire 3.6 hours** — 1652 to 1682, a 2% band with no trend. This is the
  signal that matters most: a handle leak shows there long before working set moves, and there is none.

A leak would keep climbing. This asymptotes and then holds for the final ninety minutes, which is the
shape of a cache filling up rather than memory being lost.

**Two things this does not establish.** The app was **idle** — no account cycling, no navigation churn,
no re-syncs — so a leak that only appears under those is out of scope. And separately from leaking:
**~3.1 GB of WebView2 against ~380 MB of app is a large absolute footprint**, roughly eight times the
app's own. It is not growing, but it is the number that will determine whether this runs comfortably on a
modest machine, and nothing in this audit has tried to reduce it.

---

## 7. What worked, as process

Worth carrying forward, because the difference in yield between these and ordinary review was large.

- **Write the failing test first.** Every metric defect was proven by a test that failed with a readable
  message *before* the fix — `"4 customers are waiting but the card claims 100% caught up"`. That is what
  separates a real finding from a plausible one.
- **Prove the guard catches the regression.** Reverting each fix and confirming the tests fail caught a
  hole in *my own* coverage: the injected sparkline regression passed everything, because every test had
  placed the short day at *today*, where the gap to the previous day is still 24 hours.
- **Verify against the running app, not the build** — *and against the right build.* Several fixes looked
  right and were confirmed only by driving the live app. One was "disconfirmed" by driving the **wrong**
  build; see below.
- **Record clean results.** Roughly half this audit's value is "checked, and here is why it is fine".
- **Say what was not done.** Every increment's commit message and findings section ends with what was
  skipped and why.

### What did not work

- **Six concurrent heavy subagents** exhausted the session limit and produced zero output files (session 1).
- **A static XAML sweep** reported 32 unnamed controls where the live tree had 9. The live tree is the
  authority.
- **A focus walk driven by injected input** silently wandered into another application and reported its
  controls as the app's tab order. Guard by `ProcessId`.
- **A stale UI capture manufactured an entirely fictional finding.** F-OFFLINE-05 claimed a fix "did not
  take effect", on the strength of a capture taken against `4.99.21.0` — a build compiled before the fix
  existed, which shipped in `v4.99.22`. The version was recorded correctly in the evidence and nobody
  compared it to the version the change landed in. It came complete with a suspect list and a next step,
  and it survived into a merged report and a public release before being withdrawn.
  **A stale capture is more dangerous than no capture: it looks like evidence.**

---

## 8. Recommended order for whoever picks this up

1. **A real screen reader**, and the focus orders outside the dashboard. The tree is clean; the experience
   is unverified. This is now the largest genuinely-unknown area.
2. **A soak under account churn.** The idle 3.6-hour run is clean; cycling, re-syncing and navigating
   accounts is the case still untested. Worth pairing with an attempt to reduce the ~3.1 GB WebView2
   footprint, which is stable but eight times the app's own.
3. **Tell existing users that `v4.99.27` needs a manual install.** Anyone below `v4.99.22` is stuck behind
   the bug the release fixes, and no future release can reach them. The `v4.99.27` release notes are the
   only place this can be said, because it is a property of the build they are *already* running.
4. **Code-sign the installer.** It closes F-OFFLINE-01 properly and restores the stronger control.
5. **F-SNAP-02 and F-ORCH-06**, the two remaining recorded findings with user-visible consequences.
6. **Repository housekeeping** — `main` carries a commit titled "Audit Files" (`954145e`) with ~1,400
   graphify cache files, probably worth dropping. This branch has not been merged, pushed, or PR'd.
