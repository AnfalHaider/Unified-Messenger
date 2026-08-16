# Audit assumptions

Decisions made autonomously during the `audit/product-hardening` run, where the brief was ambiguous and
blocking for an answer would have stalled the work. Each records the reasoning and **the cost of being
wrong**, so a human can overturn any of them cheaply.

---

## A-01 — `messenger` and `discord` stay in the picker, with honest capability labelling, rather than being removed

**Context.** §7 of the brief ("channel completeness") says a product being sold may not offer a channel
that silently produces no data, and gives two options: implement the scraper, or remove the channel from
the picker. It named `telegram` and `messenger`. On inspection, `telegram` is already hidden
(`HiddenFromPicker`), and the actually-affected picker entries are `messenger` and `discord`.

**Decision.** Neither channel is removed, and no metric scraper is built for them. Instead the picker is
made to state plainly what each channel does and does not do (fixing F-ORCH-01, which is the *real*
defect — the honest disclaimers already exist in code and are simply never rendered).

**Reasoning.**
1. §7's premise is that the channel "silently produces no data". The silence is the defect, not the
   absence of metrics. An embed-only channel that *says* it is embed-only is not a broken promise — it is
   a feature with a stated scope, exactly like the `generic` / Custom URL entry the product already ships
   and documents.
2. Building a Messenger metric scraper would run straight into a hard constraint. `PlatformDefinition.cs`
   records that Meta web clients **mark a thread read and fire a read receipt to the customer the moment
   the thread is opened**, and models this as `RequiresThreadOpenToRead`. Constraint 3 says the app never
   auto-sends and automation is read-only. A per-conversation Messenger scraper would therefore transmit
   read receipts to the owner's customers on the owner's behalf — an outbound side effect. That is
   **WONTFIX-BY-CONSTRAINT** for anything below badge-level aggregates.
3. Discord is not a customer-conversation channel for a multi-location business at all; oversight metrics
   for it were never the point. It also carries deliberate engineering (`WebViewPlatformConfigurator`
   gives it a desktop Chrome user agent and in-app new-window handling so login works), which indicates
   the embed is intentional rather than vestigial.

**Cost of being wrong.** Low and cheaply reversed. If the owner considers these entries clutter, deleting
two entries from `PlatformDefinition.All` plus their `HiddenFromPicker` treatment is a few lines and no
data migration — existing accounts already resolve through `NormalizePlatformId`. If instead the owner
genuinely wanted Messenger metrics, that is a larger conversation gated on the read-receipt constraint
above, not on effort, and it is raised in the final report's Open Questions.

**Status:** applied. Recorded as an ADR requirement — see `docs/architecture/adr/`.

---

## A-02 — "Wrong number" severity is judged by what a reasonable owner would *act on*, not by numeric magnitude

**Context.** The brief makes a wrong metric an S1 because it is silent and gets trusted. Applied
literally, every rounding difference becomes an S1.

**Decision.** A metric defect is S1 when it could change a decision the owner makes — staffing a
location, chasing a manager, believing they are caught up when they are not. A defect that is visibly
cosmetic (a sparkline's last pixel, a tooltip's decimal place) is S3 even though it is technically a
wrong number.

**Reasoning.** The bar in the brief is "a stranger paid for this and is using it unsupervised."
Unsupervised use means acting on the numbers. Severity should track consequence, or the S1 list becomes
noise and the genuinely dangerous defects lose their priority.

**Cost of being wrong.** Moderate. If this is too lenient, a real defect could be triaged to S3 and
deferred. Mitigated by recording every downgraded item explicitly in `BACKLOG.md` with the reasoning
visible, rather than silently reclassifying.

---

## A-03 — Documentation accuracy is in scope; commerce plumbing is not

**Context.** §0 says "sellable" means production polish, not commerce plumbing — no licensing, trials,
activation, payment, or telemetry.

**Decision.** README, CHANGELOG, AGENTS.md, THIRD-PARTY-NOTICES.md and in-app About/help text are treated
as shipping surface and audited for accuracy. Anything resembling licensing, activation, or usage
tracking is out of scope and will not be built even where its absence is conspicuous.

**Reasoning.** §9's definition of done explicitly requires README, CHANGELOG, version files, and
third-party notices to be accurate, so docs are in. Telemetry is doubly excluded — by §0 and by
constraint 2.

**Cost of being wrong.** Negligible.
