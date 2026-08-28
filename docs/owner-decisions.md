# Four decisions only the owner can make

**As of:** 2026-08-27 · **Baseline:** v4.99.53

Each of these is a product judgement, not an engineering one. They are written up here because they have
been deferred across several work-streams without ever being put to the owner as a question with its
consequences attached. Each has a recommendation; none has been acted on.

---

## 1 · The SLA target says every location is failing

**This is the highest-value item on the entire backlog and it costs no engineering time.**

### What the owner sees

`SLA met 0%` on the dashboard. Every account and every location reads as failing. It is the most alarming
number on the screen.

### Why

The target is **15 minutes** — the global default in `settings.json`, and each of the three workspace
profiles (DHA-2, F-11, Men-DHA-2) explicitly overrides it to 15 as well, so nothing is falling back to a
value nobody chose. The measured median first reply is **hours**. A target that nothing ever meets stops
being a target and becomes noise: the number is technically correct, permanently red, and therefore
ignored — which is worse than not showing it, because it also teaches the owner to ignore the tiles next
to it.

Nothing about the measurement is wrong. `ResponseTimeTracker` measures forward from real message
timestamps, `BusinessHoursCalculator` can exclude closed hours, and the threshold is honoured per location.
The number is right and the bar is in the wrong place.

### The options

| | What it means | Consequence |
|---|---|---|
| **A. Move the target to something the business could actually hit** — say 2 hours, or 4 | The tile starts distinguishing a good day from a bad one | The percentage becomes informative immediately. It will not be 100%, which is the point. Per-location overrides already exist, so a busy branch and a quiet one can differ. |
| **B. Keep 15 minutes as an aspiration and show distance from it instead** | Replace "SLA met 0%" with "median first reply 3h 20m · target 15m" | Honest, never absurd, and still shows the gap. Loses the single-number comparability across locations. |
| **C. Leave it** | | The most prominent figure on the dashboard stays 0% forever and trains the owner to disregard the whole band. |

**Recommendation: A, with the target set from a week of real data** — pick the current median, round down,
and let the number have somewhere to go. B is a good second if the 15-minute figure is a commitment made to
customers rather than an internal hope.

**What is needed from the owner:** one number, or a decision to switch the tile to option B. Both are
settings-level changes.

---

## 2 · Should "median reply time" for Google reviews be measured from installation?

### The situation

Google publishes no reply dates anywhere the scrape can reach (D6). The tile currently says so rather than
estimating, which is the right default — an invented number here would be indistinguishable from a real one.

The app *could* measure reply time for reviews it watches from installation onward, the same way
`ResponseTimeTracker` does for WhatsApp: note when a review first appears unanswered, note when it stops
being unanswered, take the difference.

### The trade-off

It would be a real measurement, but it covers only reviews that arrive after install, and it would have to
say so on the tile every time — "since 12 March", indefinitely. That caveat never goes away, and a metric
that needs a footnote forever is a metric people misread eventually.

| | Consequence |
|---|---|
| **A. Build it, labelled "since <install date>"** | Real data within a few weeks. Permanent caveat. Roughly the same shape as `ResponseTimeTracker`, so the pattern already exists. |
| **B. Drop the tile** | The Reviews page stops implying a figure exists. Nothing is lost that was ever shown. |
| **C. Leave it saying "not available"** | Honest, and it occupies space to say nothing. |

**Recommendation: B or A, not C.** If reply speed on reviews is something the owner would act on, A is worth
the caveat; if it is not, the tile should go rather than sit there explaining its own absence.

---

## 3 · Does the backlog cutoff stay at 7 days?

### What it does

`awaitingBacklogAfterDays: 7` splits "waiting" into a **live** queue and a **backlog**. It is what turned
466 waiting conversations into a workable 58-item queue. It was chosen, not derived — there is no
measurement behind the number 7.

### The trade-off

Shorter (3–5 days) makes the live queue smaller and more urgent, and moves more conversations into a
backlog that may never be looked at. Longer (14 days) keeps more in view and risks the queue becoming the
466-item list it was designed to replace.

**Recommendation: leave it at 7 unless the live queue is routinely being cleared**, in which case lengthen
it — a queue that empties every day is one that could afford to show more. This is the one decision here
that genuinely benefits from waiting until there is usage data to look at.

---

## 4 · Should the "Audit Files" commit be dropped from history?

### The situation

Commit `954145e` added ~183,000 lines of regenerable `graphify-out` cache. `size-pack` is **112 MiB** for
what is otherwise a modest desktop app.

Those files were **untracked going forward at v4.99.53** — `git rm -r --cached`, no history touched,
nothing removed from disk — so nothing new accumulates and every clone from here on is unaffected in
content. What is still open is only whether to reclaim the space already in history.

### The trade-off

| | Consequence |
|---|---|
| **A. Leave it** | Every clone downloads 112 MiB. Harmless on a fast connection; slightly rude on a slow one. Zero risk. |
| **B. Rewrite history** (`git filter-repo`) and force-push | `size-pack` drops to a few MiB. **Every commit SHA after `954145e` changes.** Anyone with a clone must re-clone. Existing release tags and their SHAs move. Any link to a commit from an issue, a changelog or a release note breaks. |

**Recommendation: A.** The cost is a one-off download on a repository with one developer; the risk of B is
breaking every existing reference. If the repository ever gains contributors or CI-clone-time becomes a
real cost, revisit — but do it deliberately and on a quiet day, not as a side effect of housekeeping.

**This one must not be done without an explicit instruction.** Rewriting published history is not a
housekeeping decision.
