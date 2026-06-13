# Unified Messenger — system map

Gate 0 / Wave 12 architecture reference (updated v3.2.0).

## Runtime layers

```
┌─────────────────────────────────────────────────────────────┐
│  Shell (MainWindow)                                         │
│  WorkspaceSidebar │ ContentFrame │ NotificationFeedPanel    │
└────────────┬────────────────────┬───────────────────────────┘
             │                    │
    ┌────────▼────────┐   ┌───────▼──────────────────────────┐
    │ DashboardPage   │   │ SettingsPage / About             │
    │  ├─ OCC         │   └──────────────────────────────────┘
    │  └─ Personal    │
    └────────┬────────┘
             │
    ┌────────▼────────────────────────────────────────────────┐
    │ ApplicationServices (composition root)                  │
    │ Registry · SessionManager · Navigation · NotificationHub│
    │ AppSettings · ThreadRegistry · MessageTriage · Analytics│
    │ TriagePersistence · ThreadDisplayOrder · StateSync      │
    │ WebViewScriptGateway · GitHubUpdate · Dialog            │
    └────────┬────────────────────────────────────────────────┘
             │
    ┌────────▼────────┐   ┌──────────────┐   ┌───────────────┐
    │ Presenters      │   │ ViewModels   │   │ WebView2 host │
    │ (pure mapping)  │   │ (MVVM state) │   │ + JS adapters │
    └────────┬────────┘   └──────────────┘   └───────┬───────┘
             │                                         │
    ┌────────▼─────────────────────────────────────────▼───────┐
    │ Services (domain + persistence)                          │
    │ Triage · Threads · Analytics · Dashboard refresh         │
    └────────┬─────────────────────────────────────────────────┘
             │
    ┌────────▼────────┐
    │ Local persistence│
    │ settings.json    │
    │ instances.json   │
    │ analytics.json   │
    │ triage_v2.json   │
    └──────────────────┘
```

## Key flows

| Flow | Entry | Core services | UI surface |
|------|-------|---------------|------------|
| Professional triage | JS `inbound-message-selected` | MessageTriageService → ThreadRegistryService → OperationsCommandCenterService | OCC |
| Personal notifications | JS `notification-preview` | NotificationHub → PersonalDashboardService | Personal overview, feed panel |
| Instance navigation | `INavigationService.OpenInstance` | ShellNavigationService → InstanceSessionManager | MainWindow WebView host |
| Operational refresh | Triage/thread/analytics events | DashboardRefreshCoordinator (450 ms debounce) | DashboardPage |
| Triage persistence | Triage/thread/order mutations | TriagePersistenceService (750 ms debounce) | `triage_v2.json` |
| Settings destructive | SettingsPage actions | OperationalDataService, InstanceRegistryService | Settings |
| Import/export | Settings (experimental) | InstanceRegistryService + backup `.bak` | Settings |

## MVVM map

| Surface | ViewModel | Presenter / helper |
|---------|-----------|-------------------|
| Shell | MainWindowViewModel, WorkspaceSidebarViewModel, NotificationFeedViewModel | NotificationFeedPresenter, WorkspaceSidebarMenuPlanner |
| OCC | OperationsCommandCenterViewModel, BranchWorkspacePillBarViewModel | OccSnapshotPresenter, OccThreadCardPresenter |
| Personal | PersonalOverviewViewModel | PersonalSnapshotPresenter, PersonalOverviewSearchPresenter |
| Settings | SettingsViewModel | SettingsArchivedAccountsPresenter, SettingsImportExportPresenter |
| Charts | WeeklyActivityChartViewModel, SentimentActivityChartViewModel | WeeklyActivityChartHelper, DashboardTriageHelper |

## CI / release

| Job | Purpose |
|-----|---------|
| `verify` | Build + unit tests (442) |
| `package` | Publish win-x64 / win-arm64 + Inno installers + SHA-256 sidecars |
| `ui-smoke` | FlaUI harness against published x64 binary |
| `release` | Tag-driven GitHub release from CI artifacts |

## Persistence paths

- User data root: `%LOCALAPPDATA%\UnifiedMessenger\`
- `settings.json`, `instances.json`, `analytics.json`, `triage_v2.json`, WebView profiles

## Platform scope

WhatsApp and WhatsApp Business Web only (v3.0+ lite baseline).
