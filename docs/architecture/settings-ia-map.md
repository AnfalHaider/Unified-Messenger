# Settings information architecture

Unified Messenger settings use a left navigation rail with ten sections. Sub-pages (Local AI, About) show a breadcrumb back to Settings.

## Primary sections

| Section key | Label | Purpose |
|-------------|-------|---------|
| `notifications` | Notifications | Toasts, badges, panel auto-open, sound, grouping |
| `appearance` | Appearance | Theme and notification panel dock |
| `session-performance` | Session & performance | Startup warm mode, WebView concurrency, experimental Path C toggles |
| `professional-metrics` | Professional metrics | SLA threshold, urgency threshold, startup backfill, heuristic insights |
| `data-privacy` | Data & privacy | Clear operational telemetry; import/export when enabled |
| `system` | System | Background close, startup, updates |
| `removed-accounts` | Removed accounts | Restore or permanently delete archived sidebar accounts |
| `storage` | Storage | instances.json and WebView profile paths |
| `local-ai` | Local AI | Link to Local AI sub-page |
| `about` | About | Version and app details sub-page |

## Sub-pages

| Route | Breadcrumb | Back action |
|-------|------------|-------------|
| `LocalAISettingsPage` | Settings › Local AI | `Frame.GoBack()` |
| `AboutPage` | Settings › About | `Frame.GoBack()` |

## Experimental features

Path C session options live inside an **Experimental** expander under Session & performance (collapsed by default):

- Lazy WebView loading
- Per-instance sleep unload
- Edit instance metadata
- Import / export instances
- Instance notes and tags

## Import / export UX

1. **Export** — pre-export summary dialog lists active/archived counts and registry path before the save picker.
2. **Import** — user selects file first; confirmation summarizes counts and optional backup; backup writes `instances.json.bak` beside the live registry when enabled.

## Removed accounts

Archived rows show display name, platform, profile folder, restore, and permanent delete. Permanent delete calls `RemovePermanentlyAsync` after a destructive confirmation.

## MVVM mapping (Wave 9)

| UI surface | ViewModel | Presenter / helper |
|------------|-----------|-------------------|
| Personal overview | `PersonalOverviewViewModel` | `PersonalSnapshotPresenter`, `PersonalOverviewSearchPresenter` |
| Settings shell | `SettingsViewModel` | `SettingsNavigationHelper`, `SettingsArchivedAccountsPresenter`, `SettingsImportExportPresenter` |
| Local AI | `LocalAISettingsViewModel` | `LocalAiSettingsPageHelper` |
