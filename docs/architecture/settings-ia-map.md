# Settings information architecture

Unified Messenger settings use a left navigation rail with sections on a single scrollable page. About opens as a sub-page.

## Primary sections

| Section key | Label | Purpose |
|-------------|-------|---------|
| `notifications` | Notifications | Toasts, badges, panel auto-open, sound, grouping |
| `appearance` | Appearance | Theme and notification panel dock |
| `session-performance` | Session & performance | Startup warm mode, WebView concurrency, experimental Path C toggles, SLA/backfill |
| `ai` | AI | Enable local Ollama, endpoint, model picker, test connection, model pull |
| `data-privacy` | Data & privacy | Clear operational telemetry; import/export when enabled |
| `keyboard-shortcuts` | Keyboard shortcuts | Shell shortcut reference |
| `system` | System | Background close, startup, updates |
| `removed-accounts` | Removed accounts | Restore or permanently delete archived sidebar accounts |
| `storage` | Storage | instances.json and WebView profile paths |
| `about` | About | Version and app details sub-page |

## Sub-pages

| Route | Breadcrumb | Back action |
|-------|------------|-------------|
| `AboutPage` | Settings › About | `Frame.GoBack()` |

## AI section (v3.7.0)

Progressive disclosure: master **Enable local AI** toggle reveals endpoint (read-only default), model dropdown, auto-bootstrap toggle, **Test connection**, and **Pull selected model** with determinate progress.

Privacy copy: message text stays on-device via local Ollama only.

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

## MVVM mapping

| UI surface | ViewModel | Presenter / helper |
|------------|-----------|-------------------|
| Personal overview | `PersonalOverviewViewModel` | `PersonalSnapshotPresenter`, `PersonalOverviewSearchPresenter` |
| Settings shell | `SettingsViewModel` | `SettingsNavigationHelper`, `SettingsArchivedAccountsPresenter`, `SettingsImportExportPresenter` |
| Settings › AI | (code-behind partial) | `AiSettingsSectionHelper`, `OllamaModelPullHelper` |
