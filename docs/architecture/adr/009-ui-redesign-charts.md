# ADR-009: Chart suite + data-shaping foundation for the UI redesign

## Status

Accepted (v4.96.0) — increment 1 of the design-team UI vision.

## Context

The design team's mockups need a chart suite (bar / area / donut / KPI cards with vs-last-week deltas)
the app doesn't have. WinUI 3 ships no chart controls, and external chart libraries are banned by the
zero-dependency rule. Recon also found that the *data* the charts need mostly doesn't exist yet — the
stores expose whole-window aggregates, not the per-day series and prior-period deltas a chart binds to.

## Decision

**Build the data layer first, tested, before any pixel.** The charts are the visible part, but the
load-bearing work is shaping numbers the stores don't already produce. This increment ships that layer
and the reusable controls that consume it; no page changes yet, so everything downstream is unblocked and
the risky (visual) part is separated from the verifiable (numeric) part.

### Data shaping — `ChartSeriesBuilder` (pure, 17 tests)

Deterministic, store-free static methods so they unit-test directly:
- `ComputeDelta` returns a `MetricDelta` whose **sentiment is three-state** (Favourable / Adverse /
  Neutral), not a bool. This is the mockup's "volume down is neutral, response-time down is good" rule:
  colour follows sentiment, never arrow direction. A bool `IsFavourable` couldn't tell an adverse move
  from a no-judgement one, so the model carries `DeltaSentiment`.
- `BuildShareSlices` — donut percentages that sum to **exactly** 100 (largest-remainder rounding), zero
  rows dropped.
- `BuildSlaBreakdown` — the three-way met/missed/**no-SLA** split, joining response stats with
  `OversightEntityHealth.SupportsResponseTiming`/`MeasuredCount`. An untimeable channel is no-SLA, never
  a false "met" or "missed".
- `RankTopPerformers` — inverts the worst-first oversight ordering, but **only ranks accounts with real
  measured data**. `OnTimePercent` defaults to 100 for an unsynced account; a naive inversion would crown
  the accounts we know nothing about. A test asserts an unmeasured account is not #1.
- `BuildZeroPaddedDailySeries` — date-index-aligned so it can be sliced into last-7 vs previous-7.

### New store accessors

- `KpiTrendStore.GetCaughtUpByDay` / `GetAwaitingByDay` — **date-keyed** maps. The existing `Series()`
  omits empty days, so it can't be sliced for week-over-week; these preserve the dates.
- `ResponseTimeTracker.GetDailyWithinThreshold` — the "replies within 15 minutes" **series**, which
  `GetStats` could only give as a single window number.

### Controls — `Controls/Charts/`, code-behind (the repo's chart pattern, per `MiniSparkline`)

`DeltaBadge` (sentiment-coloured chip), `DonutChartView` (`ArcSegment` ring + centre + legend, standard
empty state when all-zero), `KpiStatCard` (icon chip + hero value + delta + sparkline). All theme-aware
via the existing semantic brushes, all with automation summaries.

### Tokens

Additive type ramp filling the gap between body (12) and the lone metric size (22): Subtitle 15 /
MetricSm 18 / Metric 24 / **Hero 34**, with `UmSubtitleTextStyle` / `UmMetricValueStyle` /
`UmHeroValueStyle`.

## Deferred (with reasons, not omissions)

- **`BarChartView` / `AreaLineChartView`** — these re-home renderers that already exist and work
  (`ActivityPatternsPanel`'s stacked bars, `MessageVolumeLineChartHelper`'s line/area). Building them now
  creates a third unused copy; they land in the increment that *replaces* those renderers, so the old and
  new never coexist.
- **Full colour-system + elevation theme refactor** — wrapping every `UmBrand*`/`UmStatus*` in
  `ThemeDictionaries` and adding shadow tokens is an app-wide change with real regression surface. It gets
  its own careful increment rather than being rushed alongside new controls. The status colours used here
  are already mid-tone values that read on both themes.
- **Visual verification of the controls** — code-behind controls can't be unit-tested headlessly (they
  need the XAML runtime, which hangs in CI — the documented rule). They build clean; their first on-screen
  appearance is the dashboard-recomposition increment, which is where visual iteration is efficient.
