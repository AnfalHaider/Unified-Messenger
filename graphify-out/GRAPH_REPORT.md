# Graph Report - UnifiedMessenger  (2026-08-04)

## Corpus Check
- 359 files · ~178,333 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 4572 nodes · 9253 edges · 244 communities (218 shown, 26 thin omitted)
- Extraction: 98% EXTRACTED · 2% INFERRED · 0% AMBIGUOUS · INFERRED: 185 edges (avg confidence: 0.75)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- WhatsApp DOM Scraper
- Services and Models Namespace
- GitHub Auto-Update
- Activity Patterns Chart
- Shell Window Chrome
- Main Window WebView Host
- Instance Store Persistence
- Account Icon Picker
- Settings Page
- Taskbar Badge Service
- Dashboard Snapshot Model
- Command Palette
- Session Lifecycle and Adapters
- Personal Overview Panel
- Command Center Panel
- Shell Layout Metrics
- Messenger Instance Model
- Oversight Chat Snapshot
- Response Time Tracking
- Instance Registry Service
- Community 20
- Community 21
- Community 22
- Community 23
- Community 24
- Community 25
- Community 26
- Community 27
- Community 28
- Community 29
- Community 30
- Community 31
- Community 32
- Community 33
- Community 34
- Community 35
- Community 36
- Community 37
- Community 38
- Community 39
- Community 40
- Community 41
- Community 42
- Community 43
- Community 44
- Community 45
- Community 46
- Community 47
- Community 48
- Community 49
- Community 50
- Community 51
- Community 52
- Community 53
- Community 54
- Community 55
- Community 56
- Community 57
- Community 58
- Community 59
- Community 60
- Community 61
- Community 62
- Community 63
- Community 64
- Community 65
- Community 66
- Community 67
- Community 68
- Community 69
- Community 70
- Community 71
- Community 72
- Community 73
- Community 74
- Community 75
- Community 76
- Community 77
- Community 78
- Community 79
- Community 80
- Community 81
- Community 82
- Community 83
- Community 84
- Community 85
- Community 86
- Community 87
- Community 88
- Community 89
- Community 90
- Community 91
- Community 92
- Community 93
- Community 94
- Community 95
- Community 96
- Community 97
- Community 98
- Community 99
- Community 100
- Community 101
- Community 102
- Community 103
- Community 104
- Community 105
- Community 106
- Community 107
- Community 108
- Community 109
- Community 110
- Community 111
- Community 112
- Community 113
- Community 114
- Community 115
- Community 116
- Community 117
- Community 118
- Community 119
- Community 120
- Community 121
- Community 122
- Community 123
- Community 124
- Community 125
- Community 126
- Community 127
- Community 128
- Community 129
- Community 130
- Community 131
- Community 132
- Community 133
- Community 134
- Community 135
- Community 136
- Community 137
- Community 138
- Community 139
- Community 140
- Community 141
- Community 142
- Community 143
- Community 144
- Community 145
- Community 146
- Community 147
- Community 148
- Community 149
- Community 150
- Community 151
- Community 152
- Community 153
- Community 154
- Community 155
- Community 156
- Community 157
- Community 158
- Community 159
- Community 160
- Community 161
- Community 162
- Community 163
- Community 164
- Community 165
- Community 166
- Community 167
- Community 168
- Community 169
- Community 170
- Community 171
- Community 172
- Community 173
- Community 174
- Community 175
- Community 176
- Community 177
- Community 178
- Community 179
- Community 180
- Community 181
- Community 182
- Community 183
- Community 184
- Community 185
- Community 186
- Community 187
- Community 188
- Community 189
- Community 190
- Community 191
- Community 192
- Community 193
- Community 194
- Community 195
- Community 196
- Community 197
- Community 198
- Community 199
- Community 200
- Community 201
- Community 202
- Community 203
- Community 204
- Community 205
- Community 206
- Community 207
- Community 208
- Community 209
- Community 210
- Community 211
- Community 212
- Community 213
- Community 214
- Community 215
- Community 216
- Community 217
- Community 218
- Community 219
- Community 220
- Community 221
- Community 222
- Community 223
- Community 224
- Community 225
- Community 226
- Community 227
- Community 228
- Community 229
- Community 230
- Community 231
- Community 232
- Community 233
- Community 234
- Community 235
- Community 236
- Community 237
- Community 238
- Community 239
- Community 240
- Community 241
- Community 242

## God Nodes (most connected - your core abstractions)
1. `MessengerInstance` - 216 edges
2. `UnifiedMessenger.Services` - 210 edges
3. `UnifiedMessenger.Models` - 189 edges
4. `SettingsPage` - 110 edges
5. `CommandCenterPanel` - 87 edges
6. `MessageAnalyticsService` - 85 edges
7. `Page` - 84 edges
8. `MainWindow` - 83 edges
9. `WorkspaceSidebar` - 69 edges
10. `InstanceSessionManager` - 61 edges

## Surprising Connections (you probably didn't know these)
- `Window` --references--> `MainWindowViewModel`  [INFERRED]
  MainWindow.xaml → ViewModels/MainWindowViewModel.cs
- `Page` --references--> `SettingsViewModel`  [INFERRED]
  Pages/SettingsPage.xaml → ViewModels/SettingsViewModel.cs
- `App` --references--> `MainWindow`  [EXTRACTED]
  App.xaml.cs → MainWindow.ChromeEvents.partial.cs
- `App` --references--> `ApplicationServices`  [EXTRACTED]
  App.xaml.cs → Services/ApplicationServices.cs
- `ActivityPatternsPanel` --references--> `ActivityDimension`  [EXTRACTED]
  Controls/ActivityPatternsPanel.xaml.cs → Services/Analytics/MessageAnalyticsService.cs

## Import Cycles
- None detected.

## Communities (244 total, 26 thin omitted)

### Community 0 - "WhatsApp DOM Scraper"
Cohesion: 0.07
Nodes (61): attachMainObserver(), attachSidebarObserver(), beginDomWorkTick(), collectVisibleHistoryMessages(), countFromDomBadges(), detectMessageKind(), detectOutgoingDeliveryStatus(), disconnectDomObservers() (+53 more)

### Community 2 - "GitHub Auto-Update"
Cohesion: 0.08
Nodes (27): GitHubReleaseInfo, UpdateCheckResult, UpdateCheckStatus, CancellationToken, Func, Task, IGitHubUpdateService, CancellationToken (+19 more)

### Community 3 - "Activity Patterns Chart"
Cohesion: 0.06
Nodes (38): AccountSelector, AxisHost, ChartArea, ChartHost, DayButton, HeatmapButton, HeatmapHost, HourButton (+30 more)

### Community 4 - "Shell Window Chrome"
Cohesion: 0.05
Nodes (47): AiToggleButton, AppTitleBar, BackToDashboardButton, ContentFrame, InstanceLoadingPanel, InstanceLoadingRing, InstanceLoadingText, InstanceWebViewHost (+39 more)

### Community 5 - "Main Window WebView Host"
Cohesion: 0.05
Nodes (24): CoreWebView2InitializedEventArgs, CoreWebView2SourceChangedEventArgs, PointerRoutedEventArgs, MainWindow, bool, Button, ColumnDefinition, CoreWebView2 (+16 more)

### Community 6 - "Instance Store Persistence"
Cohesion: 0.10
Nodes (15): int, List, InstanceStore, bool, CancellationToken, GeneratedRegex, HashSet, IEnumerable (+7 more)

### Community 7 - "Account Icon Picker"
Cohesion: 0.06
Nodes (29): Button, Color, Glyph, SolidColorBrush, string, TextBlock, AvatarChoiceKind, ChangeIconDialog (+21 more)

### Community 8 - "Settings Page"
Cohesion: 0.05
Nodes (24): SettingsPage, bool, CancellationTokenSource, DateTimeOffset, IReadOnlyList, long, SelectionChangedEventArgs, LocalAiModelOption (+16 more)

### Community 9 - "Taskbar Badge Service"
Cohesion: 0.05
Nodes (17): Task, ITaskbarBadgeService, bool, int, Lazy, object, Task, TaskbarBadgeService (+9 more)

### Community 10 - "Dashboard Snapshot Model"
Cohesion: 0.08
Nodes (28): Active, CaughtUp, OccQueueFilter, DateTimeOffset, ThreadData, IReadOnlyList, UnifiedMessengerBranchMetrics, UnifiedMessengerDashboardSnapshot (+20 more)

### Community 11 - "Command Palette"
Cohesion: 0.07
Nodes (26): Subtitle, Overlay, PalettePanel, ResultsList, SearchBox, UserControl, IconGlyph, Title (+18 more)

### Community 12 - "Session Lifecycle and Adapters"
Cohesion: 0.12
Nodes (12): LinkedList, PlatformAdapterFactory, CancellationToken, DateTimeOffset, Dictionary, IEnumerable, Lazy, string (+4 more)

### Community 13 - "Personal Overview Panel"
Cohesion: 0.06
Nodes (44): ConnectionBrush, ConnectionStatusLabel, DetailLine, DisplayName, InstanceDisplayName, PlatformLabel, UnreadBadgeText, UnreadBadgeVisibility (+36 more)

### Community 14 - "Command Center Panel"
Cohesion: 0.08
Nodes (15): CalendarDatePicker, CalendarDatePickerDateChangedEventArgs, AutoSuggestBox, AutoSuggestBoxTextChangedEventArgs, bool, DateTimeOffset, DispatcherTimer, double (+7 more)

### Community 15 - "Shell Layout Metrics"
Cohesion: 0.06
Nodes (24): ColumnWidth, GridLength, NotificationPanelAutoOpenMode, NotificationPanelDock, RowHeight, double, FrameworkElement, MainWindowShellLayout (+16 more)

### Community 16 - "Messenger Instance Model"
Cohesion: 0.13
Nodes (16): MessengerInstance, CancellationToken, ConcurrentDictionary, ConditionalWeakTable, CoreWebView2, DateTimeOffset, Dictionary, int (+8 more)

### Community 17 - "Oversight Chat Snapshot"
Cohesion: 0.08
Nodes (28): ChatEntryDto, ChatEntry, InstanceSnapshotDto, OversightDigest, bool, CancellationToken, CancellationTokenSource, ConcurrentDictionary (+20 more)

### Community 18 - "Response Time Tracking"
Cohesion: 0.08
Nodes (30): DailyPercentPoint, DailyResponsePoint, InstanceResponseDto, ResponseSampleDto, ResponseStats, bool, CancellationToken, CancellationTokenSource (+22 more)

### Community 19 - "Instance Registry Service"
Cohesion: 0.10
Nodes (17): Alerted, Fire, CancellationToken, IEnumerable, IReadOnlyList, Task, IInstanceRegistryService, ImportInstancesResult (+9 more)

### Community 20 - "Community 20"
Cohesion: 0.06
Nodes (29): AlertThresholdBox, ClosePicker, ContentDialog, EditorPanel, EmptyHint, HoursToggle, LocationCombo, OpenPicker (+21 more)

### Community 21 - "Community 21"
Cohesion: 0.09
Nodes (18): DateTimeOffset, IReadOnlyList, DailySentimentPoint, MessageTriageDashboardSnapshot, MessageTriageItem, IEnumerable, IReadOnlyList, IMessageTriageService (+10 more)

### Community 22 - "Community 22"
Cohesion: 0.15
Nodes (10): Anchor, Instance, InstanceId, AdapterHealthMonitor, ApplicationServices, bool, CancellationToken, FrameworkElement (+2 more)

### Community 23 - "Community 23"
Cohesion: 0.15
Nodes (16): BackfillAccumulator, DateTimeOffset, BackfillContext, CancellationToken, DateTimeOffset, Dictionary, int, IReadOnlyList (+8 more)

### Community 24 - "Community 24"
Cohesion: 0.09
Nodes (17): CoreWebView2DownloadStartingEventArgs, CoreWebView2NavigationStartingEventArgs, Action, ConditionalWeakTable, CoreWebView2, CoreWebView2NewWindowRequestedEventArgs, HashSet, IEnumerable (+9 more)

### Community 25 - "Community 25"
Cohesion: 0.07
Nodes (6): InstanceNavigationFailedEventArgs, INavigationService, ToastActivationEventArgs, NotificationNavigationHelper, Lazy, ShellNavigationService

### Community 26 - "Community 26"
Cohesion: 0.10
Nodes (17): EventArgs, BackfillResult, CancellationToken, CancellationTokenSource, ConcurrentDictionary, IReadOnlyList, Lazy, Task (+9 more)

### Community 27 - "Community 27"
Cohesion: 0.08
Nodes (13): UnifiedMessenger.Dialogs, RoutedEventArgs, string, RenameInstanceDialog, ICollection, KeyEventHandler, bool, Control (+5 more)

### Community 28 - "Community 28"
Cohesion: 0.08
Nodes (17): IDisposable, ImageSource, MainWindow, ISystemTrayService, Action, bool, Func, List (+9 more)

### Community 29 - "Community 29"
Cohesion: 0.09
Nodes (13): bool, object, string, ActiveWorkspaceContext, InstanceNavigationRequest, Action, Func, KeyboardShortcutService (+5 more)

### Community 30 - "Community 30"
Cohesion: 0.11
Nodes (9): bool, CancellationTokenSource, ConcurrentDictionary, JsonSerializerOptions, Lazy, object, SemaphoreSlim, string (+1 more)

### Community 31 - "Community 31"
Cohesion: 0.09
Nodes (32): AttentionBanner, AttentionIcon, AttentionText, BriefingBadge, BriefingStrip, BriefingText, CardsHost, DigestBanner (+24 more)

### Community 32 - "Community 32"
Cohesion: 0.14
Nodes (10): Brush, FrameworkElement, Glyph, IReadOnlyList, Label, StackPanel, Text, Expander (+2 more)

### Community 33 - "Community 33"
Cohesion: 0.09
Nodes (21): DayPoint, DayPointDto, bool, CancellationToken, CancellationTokenSource, ConcurrentDictionary, Dictionary, Func (+13 more)

### Community 34 - "Community 34"
Cohesion: 0.10
Nodes (17): Entry, RefreshResult, ChatEntry, JsonElement, List, ChatEntryParser, ChatEntry, ConcurrentDictionary (+9 more)

### Community 35 - "Community 35"
Cohesion: 0.07
Nodes (11): UnifiedMessenger.ViewModels, UnifiedMessenger.Presenters, Brush, IReadOnlyList, Visibility, KpiTileViewModel, WeeklyActivityBarViewModel, bool (+3 more)

### Community 36 - "Community 36"
Cohesion: 0.09
Nodes (21): Override, OverrideDto, OverrideKind, bool, CancellationToken, CancellationTokenSource, ConcurrentDictionary, DateTimeOffset (+13 more)

### Community 37 - "Community 37"
Cohesion: 0.12
Nodes (13): IComparable, IReadOnlyCollection, UnifiedMessengerKanbanColumn, ThreadDisplayOrderEntry, Dictionary, Func, IEnumerable, int (+5 more)

### Community 38 - "Community 38"
Cohesion: 0.12
Nodes (9): AdapterHealthState, InstanceConnectionStatus, DateTimeOffset, Dictionary, Lazy, object, InstanceConnectionStatusEntry, InstanceConnectionStatusService (+1 more)

### Community 39 - "Community 39"
Cohesion: 0.09
Nodes (22): ContactInsight, ContactSeenDto, bool, CancellationToken, CancellationTokenSource, ConcurrentDictionary, DateTimeOffset, Dictionary (+14 more)

### Community 40 - "Community 40"
Cohesion: 0.09
Nodes (15): AttentionJumpButton, DefineLocationsButton, DensityToggle, DigestDismiss, GroupByAccountButton, GroupByLocationButton, NeedsReplyButton, ReportButton (+7 more)

### Community 41 - "Community 41"
Cohesion: 0.10
Nodes (17): CategoryBox, ContentDialog, CustomUrlBox, DisplayNameBox, PlatformBox, RestoreBox, ValidationMessage, bool (+9 more)

### Community 42 - "Community 42"
Cohesion: 0.11
Nodes (17): DateTimeOffset, List, UnifiedMessengerBranchRecord, UnifiedMessengerStoreMetadata, bool, CancellationToken, CancellationTokenSource, EventArgs (+9 more)

### Community 43 - "Community 43"
Cohesion: 0.10
Nodes (10): RoutedEventArgs, RoutedEventArgs, Task, IReadOnlyList, SettingsImportExportPresenter, CancellationToken, Task, OperationalDataService (+2 more)

### Community 44 - "Community 44"
Cohesion: 0.12
Nodes (12): bool, Dictionary, FrameworkElement, RoutedEventArgs, Task, PersonalOverviewPanel, DispatcherQueueTimer, HashSet (+4 more)

### Community 45 - "Community 45"
Cohesion: 0.13
Nodes (8): ConcurrentDictionary, DateTimeOffset, IEnumerable, IReadOnlyList, Lazy, long, object, ThreadRegistryService

### Community 46 - "Community 46"
Cohesion: 0.11
Nodes (14): AiInferenceJob, CancellationToken, CancellationTokenSource, Channel, ConcurrentDictionary, DateTimeOffset, Func, IReadOnlyList (+6 more)

### Community 47 - "Community 47"
Cohesion: 0.13
Nodes (13): bool, Dictionary, HashSet, int, IReadOnlyDictionary, IReadOnlyList, List, string (+5 more)

### Community 48 - "Community 48"
Cohesion: 0.11
Nodes (11): ArchivedAccountItem, NumberBox, NumberBoxValueChangedEventArgs, NumberBox, NumberBoxValueChangedEventArgs, IEnumerable, IReadOnlyList, List (+3 more)

### Community 49 - "Community 49"
Cohesion: 0.12
Nodes (12): Day, Hour, IEnumerable, IReadOnlyList, ActivityAccountSeries, ActivityBreakdown, EndOfDayProjection, MessagesPerDayStat (+4 more)

### Community 50 - "Community 50"
Cohesion: 0.12
Nodes (13): JsonDocument, JsonElement, WebMessageParser, CancellationTokenSource, Channel, ConcurrentDictionary, HashSet, int (+5 more)

### Community 51 - "Community 51"
Cohesion: 0.14
Nodes (25): BackgroundToastsToggle, EnableAutoUpdateToggle, EnableDeepBackfillToggle, EnableEditInstanceMetadataToggle, EnableImportExportInstancesToggle, EnableInstanceNotesTagsToggle, EnableLazyWebViewLoadingToggle, EnableLocalAiToggle (+17 more)

### Community 52 - "Community 52"
Cohesion: 0.13
Nodes (10): Dictionary, HashSet, IEnumerable, int, IReadOnlyList, Lazy, List, object (+2 more)

### Community 53 - "Community 53"
Cohesion: 0.08
Nodes (5): UnifiedMessenger.Services.Adapters, UnifiedMessenger.Services.Backfill, JsonSerializerContext, AdapterMessageJsonContext, WebMessageEnvelope

### Community 54 - "Community 54"
Cohesion: 0.13
Nodes (7): Border, Brush, CornerRadius, FrameworkElement, KeyRoutedEventArgs, PointerRoutedEventArgs, SolidColorBrush

### Community 55 - "Community 55"
Cohesion: 0.20
Nodes (6): Direction, GeneratedRegex, IEnumerable, Regex, Text, ConversationNoiseFilter

### Community 56 - "Community 56"
Cohesion: 0.16
Nodes (9): PersonalDashboardEmptyReason, PersonalSnapshotPresenter, IReadOnlyList, PersonalDashboardPresentationHelper, PersonalOverviewActivityItem, PersonalOverviewEmptyState, PersonalOverviewQuickAction, PersonalOverviewTileItem (+1 more)

### Community 57 - "Community 57"
Cohesion: 0.17
Nodes (11): Process, bool, CancellationToken, Func, HttpClient, IProgress, Lazy, SemaphoreSlim (+3 more)

### Community 58 - "Community 58"
Cohesion: 0.09
Nodes (4): UnifiedMessenger, UnifiedMessenger.Controls, UnifiedMessenger.Services.Shell, ShellSelectionState

### Community 59 - "Community 59"
Cohesion: 0.23
Nodes (23): bodyOf(), buildContactMaps(), chatTitle(), cleanText(), collectViaDebugRequire(), collectViaModuleCache(), collectViaWebpackChunk(), debugModuleMap() (+15 more)

### Community 60 - "Community 60"
Cohesion: 0.12
Nodes (11): bool, Brush, DateTimeOffset, DispatcherTimer, FrameworkElement, RoutedEventArgs, Task, TimeSpan (+3 more)

### Community 61 - "Community 61"
Cohesion: 0.11
Nodes (7): UnifiedMessenger.Services.Ai, UnifiedMessenger.Models.Ai, UnifiedMessenger.Models.Ollama, IServiceCollection, int, TranscriptBuilder, ServiceRegistration

### Community 62 - "Community 62"
Cohesion: 0.14
Nodes (10): DateTimeOffset, AdapterHealthStatus, DateTimeOffset, Dictionary, HashSet, Lazy, object, Timer (+2 more)

### Community 63 - "Community 63"
Cohesion: 0.19
Nodes (10): ContentDialog, FrameworkElement, int, StackPanel, Task, XamlRoot, FirstRunOnboardingHelper, OnboardingWizardResult (+2 more)

### Community 64 - "Community 64"
Cohesion: 0.18
Nodes (8): AccessibilitySettings, ApplicationTheme, ElementTheme, AppThemePreference, ResourceDictionary, Uri, ThemeService, UISettings

### Community 65 - "Community 65"
Cohesion: 0.13
Nodes (7): IEnumerable, PlatformCapabilities, PlatformKind, PlatformKindExtensions, HashSet, IEnumerable, PlatformModuleSettingsHelper

### Community 66 - "Community 66"
Cohesion: 0.10
Nodes (15): UnifiedMessenger.Services.Adapters.Modules, WhatsAppAdapter, WhatsAppBusinessAdapter, IReadOnlyList, JsonElement, NotificationHub, WhatsAppPlatformAdapterBase, CancellationToken (+7 more)

### Community 67 - "Community 67"
Cohesion: 0.22
Nodes (9): InstanceMessageStats, Labels, PeakLabel, Action, DateTime, DateTimeOffset, Func, ActivityDimension (+1 more)

### Community 68 - "Community 68"
Cohesion: 0.14
Nodes (9): CustomerIntent, MessageSentiment, DateTimeOffset, HeuristicTriageProcessor, HeuristicTriageResult, MessageTriageRequest, IEnumerable, string (+1 more)

### Community 69 - "Community 69"
Cohesion: 0.13
Nodes (14): RepliesChart, ResponseChart, bool, FrameworkElement, IReadOnlyList, NavigationEventArgs, SelectionChangedEventArgs, string (+6 more)

### Community 70 - "Community 70"
Cohesion: 0.13
Nodes (7): AppNotificationActivatedEventArgs, AppNotificationBuilder, AppNotificationManager, bool, Dictionary, Lazy, AppNotificationService

### Community 71 - "Community 71"
Cohesion: 0.10
Nodes (13): ChannelReader, ChannelWriter, InboundMessageKind, DateTimeOffset, IReadOnlyList, InboundMessageSelection, TriageInferenceSource, DateTimeOffset (+5 more)

### Community 72 - "Community 72"
Cohesion: 0.13
Nodes (13): List, IEnumerable, IReadOnlyList, PersonalOverviewSearchPresenter, SolidColorBrush, PersonalOverviewSearchSuggestionViewModel, bool, IEnumerable (+5 more)

### Community 73 - "Community 73"
Cohesion: 0.22
Nodes (10): CoreWebView2Profile, CancellationToken, CoreWebView2Environment, GeneratedRegex, Lazy, Regex, SemaphoreSlim, Task (+2 more)

### Community 74 - "Community 74"
Cohesion: 0.16
Nodes (10): Count, Func, IEnumerable, IReadOnlyList, Lazy, long, InstanceResourceTile, ResourceMonitorService (+2 more)

### Community 75 - "Community 75"
Cohesion: 0.11
Nodes (20): ActivityPatternsPanel, ExportButton, MessagesChart, MessagesKpi, Page, RangeBox, RepliesKpi, ResponseKpi (+12 more)

### Community 76 - "Community 76"
Cohesion: 0.13
Nodes (10): SectionLinks, bool, DispatcherTimer, EventArgs, IEnumerable, NavigationEventArgs, RoutedEventArgs, Task (+2 more)

### Community 77 - "Community 77"
Cohesion: 0.18
Nodes (5): CancellationToken, IEnumerable, Task, WebView2, IInstanceSessionManager

### Community 78 - "Community 78"
Cohesion: 0.16
Nodes (8): Task, TimeSpan, WebViewUiAwaiter, DispatcherQueue, Func, Task, UiThreadRunner, TaskCompletionSource

### Community 79 - "Community 79"
Cohesion: 0.16
Nodes (14): BackfillDedupeEntryDto, bool, CancellationToken, ConcurrentDictionary, DateTimeOffset, JsonSerializerOptions, Lazy, List (+6 more)

### Community 80 - "Community 80"
Cohesion: 0.11
Nodes (14): ContentControl, double, IReadOnlyList, Label, string, Value, BarChartView, DependencyProperty (+6 more)

### Community 81 - "Community 81"
Cohesion: 0.11
Nodes (13): ContentDialog, ContentDialog, DescriptionText, ConfirmPermanentDeleteDialog, TextBlock, ContentDialog, PinToTaskbarDialog, ContentDialog (+5 more)

### Community 82 - "Community 82"
Cohesion: 0.14
Nodes (9): OllamaPullProgress, OllamaRuntimeDownloadProgress, PullModelResponse, DateTimeOffset, CancellationToken, IProgress, IReadOnlyList, Task (+1 more)

### Community 83 - "Community 83"
Cohesion: 0.19
Nodes (8): Name, Preview, ConversationKeyResolver, ChatEntry, DateTimeOffset, IReadOnlyList, TimeSpan, OversightThreadEnricher

### Community 84 - "Community 84"
Cohesion: 0.10
Nodes (4): OpenWorkspaceManagerButton, FrameworkElement, NavigationEventArgs, RoutedEventArgs

### Community 85 - "Community 85"
Cohesion: 0.14
Nodes (11): checkOutgoingDom(), emitSent(), findLatestOutgoingSignature(), getChatHint(), isComposeElement(), isSendButton(), matchesSelector(), resolveChatHint() (+3 more)

### Community 86 - "Community 86"
Cohesion: 0.18
Nodes (10): bool, DispatcherQueueTimer, double, int, IReadOnlyList, Point, string, MessageVolumeLineChart (+2 more)

### Community 87 - "Community 87"
Cohesion: 0.18
Nodes (9): List, DateTimeOffset, NotificationAlert, NotificationAlertGroup, Dictionary, IEnumerable, IReadOnlyDictionary, IReadOnlyList (+1 more)

### Community 88 - "Community 88"
Cohesion: 0.10
Nodes (16): ObservableObject, bool, int, ObservableCollection, Visibility, NotificationFeedViewModel, bool, SolidColorBrush (+8 more)

### Community 89 - "Community 89"
Cohesion: 0.15
Nodes (13): ExportCsvButton, Page, RangeBox, ReportBody, SaveMarkdownButton, bool, NavigationEventArgs, RoutedEventArgs (+5 more)

### Community 90 - "Community 90"
Cohesion: 0.10
Nodes (15): IReadOnlyList, string, SettingsNavigationHelper, SettingsSectionNavItemViewModel, bool, ObservableCollection, string, SettingsViewModel (+7 more)

### Community 91 - "Community 91"
Cohesion: 0.17
Nodes (9): DateTimeOffset, IEnumerable, int, IReadOnlyList, string, DashboardPageHelper, DashboardSearchMatch, ProfessionalDashboardDisplay (+1 more)

### Community 92 - "Community 92"
Cohesion: 0.14
Nodes (14): ColorHex, int, IReadOnlyList, Label, Value, AnalyticsPagePresenter, AnalyticsView, DateTime (+6 more)

### Community 93 - "Community 93"
Cohesion: 0.17
Nodes (11): ConcurrentQueue, CancellationTokenSource, Channel, DateTimeOffset, int, Lazy, Task, TimeSpan (+3 more)

### Community 94 - "Community 94"
Cohesion: 0.17
Nodes (3): IEnumerable, SelectionChangedEventArgs, ShellSection

### Community 95 - "Community 95"
Cohesion: 0.12
Nodes (18): DOTNET_ENVIRONMENT, _platformHint, profiles, UnifiedMessenger, UnifiedMessenger (Release), UnifiedMessenger (x64), UnifiedMessenger (x64 Release), $schema (+10 more)

### Community 96 - "Community 96"
Cohesion: 0.30
Nodes (5): DateTimeOffset, DateTimeOffset, IReadOnlyList, JsonElement, WhatsAppIngressHandler

### Community 97 - "Community 97"
Cohesion: 0.15
Nodes (8): bool, DispatcherQueue, DispatcherQueueTimer, EventArgs, int, Lazy, object, DashboardRefreshCoordinator

### Community 98 - "Community 98"
Cohesion: 0.18
Nodes (10): AppWindowClosingEventArgs, LifecycleServices, AppWindow, CancellationToken, int, Task, TimeSpan, ApplicationLifecycleService (+2 more)

### Community 99 - "Community 99"
Cohesion: 0.11
Nodes (17): AccountName, AlertId, BodyOpacity, CardOpacity, GroupInitials, GroupTitle, GroupUnreadLabel, TitleOpacity (+9 more)

### Community 100 - "Community 100"
Cohesion: 0.14
Nodes (12): WindowSelector, SelectionChangedEventArgs, ComboBox, End, IReadOnlyList, OversightCommandCenterSnapshot, OversightEntityKind, OversightGrouping (+4 more)

### Community 101 - "Community 101"
Cohesion: 0.18
Nodes (9): DateTimeOffset, IReadOnlyList, WhatsAppConversationMetadata, WhatsAppOutgoingStatusEvent, WhatsAppTelemetryPayload, WhatsAppThreadContextSnapshot, ConcurrentDictionary, Lazy (+1 more)

### Community 102 - "Community 102"
Cohesion: 0.16
Nodes (10): CheckForUpdatesButton, LogoImage, Page, VersionText, NavigationEventArgs, RoutedEventArgs, AboutPage, Button (+2 more)

### Community 103 - "Community 103"
Cohesion: 0.14
Nodes (11): ProfileRating, ConcurrentDictionary, DateTimeOffset, Lazy, string, Task, TimeSpan, GoogleReviewSnapshotService (+3 more)

### Community 104 - "Community 104"
Cohesion: 0.16
Nodes (12): Action, ConcurrentDictionary, Func, HashSet, Lazy, object, SemaphoreSlim, string (+4 more)

### Community 105 - "Community 105"
Cohesion: 0.16
Nodes (6): Dictionary, IEnumerable, Lazy, object, WebView2, InstanceWebViewRegistry

### Community 106 - "Community 106"
Cohesion: 0.25
Nodes (9): IEnumerable, IReadOnlyList, List, string, SidebarMenuEntry, SidebarMenuEntryKind, SidebarMenuPlan, SidebarScope (+1 more)

### Community 107 - "Community 107"
Cohesion: 0.17
Nodes (16): Accent, AccentStates, Disabled, InteractionStates, InteractiveNormal, LabelText, MetricIcon, Normal (+8 more)

### Community 108 - "Community 108"
Cohesion: 0.16
Nodes (15): AddInstanceContent, AddInstanceLabel, FooterPanel, Grid, MenuRoot, MenuStack, NotificationHubBadge, NotificationsContent (+7 more)

### Community 109 - "Community 109"
Cohesion: 0.15
Nodes (7): ContentDialogButtonClickEventArgs, RoutedEventArgs, SelectionChangedEventArgs, EditInstanceMetadataDialog, IReadOnlyList, PlatformDefinition, IReadOnlyList

### Community 110 - "Community 110"
Cohesion: 0.21
Nodes (10): SelectionChangedEventArgs, double, IReadOnlyList, AccountReportLine, BusinessInsight, BusinessReport, InsightSeverity, ReportInputs (+2 more)

### Community 111 - "Community 111"
Cohesion: 0.15
Nodes (9): MenuFlyoutSubItem, MemoryTierPreference, Personal, Professional, IEnumerable, int, IReadOnlyList, string (+1 more)

### Community 112 - "Community 112"
Cohesion: 0.20
Nodes (8): OccViewMode, DateTimeOffset, Func, IEnumerable, int, IReadOnlyDictionary, IReadOnlyList, OccDateRangeFilterHelper

### Community 113 - "Community 113"
Cohesion: 0.16
Nodes (8): List, OllamaCatalogDocument, OllamaCatalogModel, OllamaConnectionState, IReadOnlyList, JsonSerializerOptions, string, AiSettingsSectionHelper

### Community 114 - "Community 114"
Cohesion: 0.25
Nodes (4): RegistryKey, string, StartupRegistrationAction, StartupTaskService

### Community 115 - "Community 115"
Cohesion: 0.15
Nodes (11): DateTimeOffset, ChannelSessionStateEvent, ChannelSnapshotEvent, IChannelEvent, Action, bool, ConcurrentDictionary, IDisposable (+3 more)

### Community 116 - "Community 116"
Cohesion: 0.21
Nodes (11): DateTimeOffset, IEnumerable, int, IReadOnlyDictionary, IReadOnlyList, IReadOnlySet, Lazy, PersonalActivityItem (+3 more)

### Community 117 - "Community 117"
Cohesion: 0.29
Nodes (13): attachScopedObserver(), broadcastResolved(), ensureObserver(), findConversationRoot(), inspectActiveThread(), looksLikeIconLigature(), normalize(), queryAll() (+5 more)

### Community 118 - "Community 118"
Cohesion: 0.17
Nodes (11): Canvas, Color, DependencyProperty, double, FrameworkElement, IReadOnlyList, Point, StackPanel (+3 more)

### Community 119 - "Community 119"
Cohesion: 0.15
Nodes (11): Border, Brush, CornerRadius, DependencyObject, DependencyProperty, DependencyPropertyChangedEventArgs, FontIcon, IReadOnlyList (+3 more)

### Community 120 - "Community 120"
Cohesion: 0.20
Nodes (9): Action, ChatEntry, FrameworkElement, Brush, ChatEntry, FrameworkElement, SolidColorBrush, StackPanel (+1 more)

### Community 121 - "Community 121"
Cohesion: 0.27
Nodes (6): bool, DependencyObject, DependencyProperty, DependencyPropertyChangedEventArgs, Visibility, MetricCardView

### Community 122 - "Community 122"
Cohesion: 0.23
Nodes (9): Action, bool, CancellationToken, JsonSerializerOptions, Lazy, SemaphoreSlim, string, Task (+1 more)

### Community 123 - "Community 123"
Cohesion: 0.14
Nodes (15): CommandCenterPanel, DashboardScrollViewer, Page, PersonalButton, PersonalButtonLabel, PersonalFlyout, PersonalOverviewPanel, WelcomeSubtitle (+7 more)

### Community 124 - "Community 124"
Cohesion: 0.12
Nodes (16): AboutSection, AccountsSection, AiAdvancedPanel, AiSection, AppearanceSection, DataPrivacySection, ImportExportPanel, KeyboardShortcutsSection (+8 more)

### Community 125 - "Community 125"
Cohesion: 0.13
Nodes (13): bool, int, string, MainWindowViewModel, CurrentSection, InstanceLoadingMessage, IsInstanceLoading, NotificationPanelVisible (+5 more)

### Community 126 - "Community 126"
Cohesion: 0.18
Nodes (9): AbsolutePath, RelativePath, CancellationToken, HashSet, Lazy, List, string, Task (+1 more)

### Community 127 - "Community 127"
Cohesion: 0.16
Nodes (11): Canvas, Func, IReadOnlyList, string, TextBlock, AreaLineChartView, ActionPresenter, UserControl (+3 more)

### Community 128 - "Community 128"
Cohesion: 0.15
Nodes (6): AlertsList, ItemClickEventArgs, ListView, IEnumerable, IReadOnlyList, INotificationHubService

### Community 129 - "Community 129"
Cohesion: 0.18
Nodes (6): ClearAllButton, MarkAllReadButton, RoutedEventArgs, Task, NotificationFeedPanel, Button

### Community 130 - "Community 130"
Cohesion: 0.13
Nodes (11): UserControl, DependencyProperty, LoadingOverlayView, CardBorder, CardContent, UserControl, DependencyProperty, SurfaceCard (+3 more)

### Community 132 - "Community 132"
Cohesion: 0.18
Nodes (9): CoreWebView2NavigationCompletedEventArgs, ConditionalWeakTable, CoreWebView2, Func, Task, TypedEventHandler, NavigationHookState, PlatformNavigationHooks (+1 more)

### Community 133 - "Community 133"
Cohesion: 0.19
Nodes (8): InstanceMessageStatsDto, Dictionary, int, IReadOnlyDictionary, List, AnalyticsStore, InstanceMessageStats, InstanceMessageStatsDto

### Community 134 - "Community 134"
Cohesion: 0.16
Nodes (8): DateTimeOffset, int, List, AppSettings, StartupWarmMode, ToastSoundPreference, WhatsAppBackfillMode, QuietHours

### Community 135 - "Community 135"
Cohesion: 0.20
Nodes (6): DeleteInstanceChoice, string, DeleteInstanceDialogHelper, CancellationToken, Task, InstanceDeletionService

### Community 136 - "Community 136"
Cohesion: 0.21
Nodes (8): bool, CancellationToken, Func, HttpClient, Lazy, string, Task, OllamaInferenceClient

### Community 137 - "Community 137"
Cohesion: 0.20
Nodes (5): CardBorder, KeyRoutedEventArgs, PointerRoutedEventArgs, TappedRoutedEventArgs, Border

### Community 138 - "Community 138"
Cohesion: 0.19
Nodes (8): ContentDialog, DescriptionText, PermanentDeleteButton, ContentDialogButtonClickEventArgs, RoutedEventArgs, DeleteInstanceDialog, Button, TextBlock

### Community 139 - "Community 139"
Cohesion: 0.21
Nodes (6): NavAddressBox, KeyRoutedEventArgs, TextBox, Result, BrowserAddressNormalizer, Result

### Community 140 - "Community 140"
Cohesion: 0.29
Nodes (7): CancellationToken, IReadOnlyList, Lazy, Task, IWebViewScriptGateway, WebViewScriptBuilder, WebViewScriptGateway

### Community 141 - "Community 141"
Cohesion: 0.24
Nodes (8): CancellationToken, ConditionalWeakTable, CoreWebView2, Dictionary, object, string, Task, WebViewChromeStyleInjector

### Community 142 - "Community 142"
Cohesion: 0.28
Nodes (3): AnalyticsStore, CancellationToken, Task

### Community 143 - "Community 143"
Cohesion: 0.19
Nodes (7): IEnumerable, IReadOnlyDictionary, IReadOnlyList, SolidColorBrush, NotificationFeedAlertRow, SolidColorBrush, NotificationFeedItem

### Community 144 - "Community 144"
Cohesion: 0.17
Nodes (9): AddPersonalAccountButton, OpenBusiestInboxButton, OperationsUrgentLink, PersonalLayoutEditButton, PersonalLayoutMoveDownButton, PersonalLayoutMoveUpButton, RoutedEventArgs, Button (+1 more)

### Community 145 - "Community 145"
Cohesion: 0.15
Nodes (6): CoreWebView2MemoryUsageTargetLevel, CoreWebView2WebMessageReceivedEventArgs, CoreWebView2, TypedEventHandler, WebView2, SessionEntry

### Community 146 - "Community 146"
Cohesion: 0.35
Nodes (7): Brush, FrameworkElement, IReadOnlyList, SolidColorBrush, StackPanel, WeeklyReportDialog, BusinessReportResult

### Community 147 - "Community 147"
Cohesion: 0.15
Nodes (12): net8.0-windows10.0.19041.0, CommunityToolkit.Mvvm (8.4.0), H.NotifyIcon.WinUI (2.2.0), Microsoft.Extensions.AI.Abstractions (10.0.0), Microsoft.Extensions.DependencyInjection (8.0.0), Microsoft.Web.WebView2 (1.0.3967.48), Microsoft.Windows.SDK.BuildTools (10.0.28000.1839), Microsoft.WindowsAppSDK (2.1.3) (+4 more)

### Community 149 - "Community 149"
Cohesion: 0.23
Nodes (7): Func, XamlRoot, CancellationToken, Func, Task, XamlRoot, WinUiDialogService

### Community 150 - "Community 150"
Cohesion: 0.18
Nodes (7): ClearEnabled, IEnumerable, MarkAllReadEnabled, IEnumerable, IReadOnlyList, NotificationFeedPresentation, NotificationFeedPresenter

### Community 151 - "Community 151"
Cohesion: 0.23
Nodes (11): AreaPath, AxisEndLabel, AxisStartLabel, ChartPlotGrid, EmptyHintText, LinePath, SummaryTextBlock, UserControl (+3 more)

### Community 152 - "Community 152"
Cohesion: 0.17
Nodes (9): ChartPresenter, SummaryTextBlock, UserControl, DependencyObject, DependencyProperty, DependencyPropertyChangedEventArgs, AccessibleChartHost, ContentPresenter (+1 more)

### Community 153 - "Community 153"
Cohesion: 0.24
Nodes (5): CancellationToken, DateTimeOffset, IEnumerable, Task, IMessageAnalyticsService

### Community 154 - "Community 154"
Cohesion: 0.32
Nodes (4): string, EditInstanceMetadataDialogHelper, EditInstanceMetadataDialogSubmission, EditInstanceMetadataFormState

### Community 155 - "Community 155"
Cohesion: 0.25
Nodes (3): AwaitingChatActions, UnifiedMessenger.Controls.Charts, UnifiedMessenger.Controls.Shared

### Community 156 - "Community 156"
Cohesion: 0.35
Nodes (4): DateTimeOffset, TimeSpan, SessionState, SessionStateProjection

### Community 157 - "Community 157"
Cohesion: 0.22
Nodes (6): RegistryNavigationArgs, ApplicationServiceProvider, ApplicationServices, Lazy, OversightService, PageServices

### Community 159 - "Community 159"
Cohesion: 0.24
Nodes (10): BranchKeyBox, ContentDialog, CustomUrlBox, DisplayNameBox, NotesBox, PlatformBox, ValidationMessage, ComboBox (+2 more)

### Community 160 - "Community 160"
Cohesion: 0.18
Nodes (7): CommandPaletteOverlay, ApplicationServices, IReadOnlyList, Task, ShellCommandPaletteCoordinator, IReadOnlyList, CommandPalette

### Community 161 - "Community 161"
Cohesion: 0.18
Nodes (11): AiConnectionStatusText, AiModelPullStatusText, AiRuntimeDownloadStatusText, InstancesPathText, NoAccountsText, NoArchivedAccountsText, ProfilesPathText, StoreBridgeHealthText (+3 more)

### Community 162 - "Community 162"
Cohesion: 0.18
Nodes (11): BackupDataButton, ClearAnalyticsButton, ClearNotificationsButton, DownloadAiRuntimeButton, ExportInstancesButton, ImportInstancesButton, PullLocalAiModelButton, RefreshAllWebViewsButton (+3 more)

### Community 163 - "Community 163"
Cohesion: 0.22
Nodes (5): long, object, string, AppLogger, StartupDiagnostics

### Community 164 - "Community 164"
Cohesion: 0.25
Nodes (4): ConcurrentDictionary, DateTimeOffset, TimeSpan, BackfillDedupeRegistry

### Community 165 - "Community 165"
Cohesion: 0.22
Nodes (4): DateTimeOffset, IEnumerable, IReadOnlyList, IThreadRegistryService

### Community 166 - "Community 166"
Cohesion: 0.35
Nodes (4): DateTimeOffset, int, Lazy, OccDateRangeFilterState

### Community 167 - "Community 167"
Cohesion: 0.33
Nodes (5): DependencyObject, FrameworkElement, PointerRoutedEventArgs, ScrollViewer, ScrollInputHelper

### Community 168 - "Community 168"
Cohesion: 0.42
Nodes (4): Brush, FrameworkElement, SolidColorBrush, ThemeBrushResolver

### Community 170 - "Community 170"
Cohesion: 0.24
Nodes (6): Export-SvgToPng(), New-InlineWordmarkTheme(), Resize-Bitmap(), Resolve-InkscapePath(), Save-Icon(), Save-Png()

### Community 171 - "Community 171"
Cohesion: 0.24
Nodes (5): Application, Task, App, LaunchActivatedEventArgs, UnhandledExceptionEventArgs

### Community 172 - "Community 172"
Cohesion: 0.24
Nodes (6): Brush, DependencyObject, DependencyProperty, DependencyPropertyChangedEventArgs, IReadOnlyList, MiniSparkline

### Community 173 - "Community 173"
Cohesion: 0.33
Nodes (4): WorkspaceCategory, string, AddInstanceDialogHelper, AddInstanceDialogSubmission

### Community 174 - "Community 174"
Cohesion: 0.20
Nodes (8): Page, NoAccountsState, Page, ReviewHealthPanel, NavigationEventArgs, ReviewsPage, StackPanel, ReviewHealthPanel

### Community 175 - "Community 175"
Cohesion: 0.33
Nodes (5): DailyActivityPoint, IReadOnlyList, WeeklyActivityChartHelper, WeeklyActivityChartSummary, IReadOnlyList

### Community 176 - "Community 176"
Cohesion: 0.38
Nodes (5): IReadOnlyList, MessageVolumeLineChartHelper, MessageVolumeLineChartSummary, X, Y

### Community 177 - "Community 177"
Cohesion: 0.25
Nodes (4): AutoSuggestBoxSuggestionChosenEventArgs, AutoSuggestBox, AutoSuggestBoxQuerySubmittedEventArgs, AutoSuggestBoxTextChangedEventArgs

### Community 178 - "Community 178"
Cohesion: 0.31
Nodes (4): LinksHost, UserControl, DashboardSectionLinks, Grid

### Community 179 - "Community 179"
Cohesion: 0.22
Nodes (6): UserControl, DependencyObject, DependencyProperty, DependencyPropertyChangedEventArgs, Visibility, SectionHeaderView

### Community 180 - "Community 180"
Cohesion: 0.28
Nodes (5): AddInstanceButton, NotificationsButton, SettingsButton, RoutedEventArgs, Button

### Community 181 - "Community 181"
Cohesion: 0.22
Nodes (4): Button, int, string, WorkspaceSidebarViewModel

### Community 182 - "Community 182"
Cohesion: 0.33
Nodes (4): ContentDialogButtonClickEventArgs, string, RenameInstanceDialogHelper, RenameInstanceDialogSubmission

### Community 183 - "Community 183"
Cohesion: 0.28
Nodes (4): ContentDialogButtonClickEventArgs, RoutedEventArgs, Task, StorageFile

### Community 185 - "Community 185"
Cohesion: 0.31
Nodes (3): DashboardCardEmptyReason, int, DashboardCardEmptyStateHelper

### Community 186 - "Community 186"
Cohesion: 0.28
Nodes (4): Mutex, Program, SingleInstanceGuard, STAThread

### Community 187 - "Community 187"
Cohesion: 0.25
Nodes (4): IEnumerable, IReadOnlyList, SettingsArchivedAccountsPresenter, ArchivedAccountRowViewModel

### Community 188 - "Community 188"
Cohesion: 0.25
Nodes (5): int, string, TimeSpan, Uri, OllamaOptions

### Community 189 - "Community 189"
Cohesion: 0.31
Nodes (6): CancellationToken, int, string, Task, TimeSpan, ConversationFocusHelper

### Community 190 - "Community 190"
Cohesion: 0.39
Nodes (5): CancellationToken, CoreWebView2Environment, Task, WebView2, IWebViewProfileManager

### Community 191 - "Community 191"
Cohesion: 0.31
Nodes (3): DllImport, WindowsAppRuntimeBootstrapHelper, WindowsAppRuntimeNative

### Community 192 - "Community 192"
Cohesion: 0.36
Nodes (4): Task, IInstanceConnection, InstanceConnection, WebViewInstanceConnection

### Community 193 - "Community 193"
Cohesion: 0.46
Nodes (7): anySelectorMatches(), bodyContainsAuthPrompt(), evaluateConnection(), isVisible(), publishStatus(), resolveProfile(), urlHintsLoggedIn()

### Community 194 - "Community 194"
Cohesion: 0.36
Nodes (3): string, WhatsAppDeliveryStatusLabel, ThreadDataExtensions

### Community 195 - "Community 195"
Cohesion: 0.25
Nodes (8): DashboardUrgencyThresholdBox, MaxConcurrentWebViewsBox, QuietHoursEndBox, QuietHoursStartBox, SlaThresholdBox, WhatsAppBackfillMaxChatsBox, WhatsAppBackfillRecentDaysBox, NumberBox

### Community 196 - "Community 196"
Cohesion: 0.25
Nodes (8): LocalAiModelBox, PanelAutoOpenBox, PanelDockBox, StartupWarmModeBox, ThemePreferenceBox, ToastSoundBox, WhatsAppBackfillModeBox, ComboBox

### Community 199 - "Community 199"
Cohesion: 0.29
Nodes (3): string, Version, AboutPageHelper

### Community 200 - "Community 200"
Cohesion: 0.48
Nodes (4): Action, CancellationToken, Task, IAppSettingsService

### Community 201 - "Community 201"
Cohesion: 0.29
Nodes (4): Action, CancellationTokenSource, string, SecondInstanceActivator

### Community 202 - "Community 202"
Cohesion: 0.33
Nodes (4): DllImport, IntPtr, uint, NativeDialogService

### Community 203 - "Community 203"
Cohesion: 0.33
Nodes (3): AppWindowChangedEventArgs, AppWindow, WindowActivatedEventArgs

### Community 204 - "Community 204"
Cohesion: 0.40
Nodes (4): InstanceTilesList, RecentActivityList, ItemClickEventArgs, ListView

### Community 205 - "Community 205"
Cohesion: 0.33
Nodes (3): Control, int, AccessibilityTabOrderHelper

### Community 206 - "Community 206"
Cohesion: 0.33
Nodes (5): CardsHost, UpdatedText, UserControl, StackPanel, TextBlock

### Community 207 - "Community 207"
Cohesion: 0.33
Nodes (4): ContentDialog, DescriptionText, AutoUpdateDialog, TextBlock

### Community 208 - "Community 208"
Cohesion: 0.33
Nodes (5): ContentDialog, DisplayNameBox, ValidationMessage, TextBlock, TextBox

### Community 209 - "Community 209"
Cohesion: 0.33
Nodes (3): ICommand, Action, TrayActionCommand

### Community 210 - "Community 210"
Cohesion: 0.33
Nodes (5): AccountsList, ArchivedAccountsList, SectionNavList, ItemClickEventArgs, ListView

### Community 211 - "Community 211"
Cohesion: 0.40
Nodes (3): HashSet, string, AdapterMessageTypes

### Community 212 - "Community 212"
Cohesion: 0.40
Nodes (4): DeltaDirection, DeltaSentiment, MetricDelta, MetricPolarity

### Community 214 - "Community 214"
Cohesion: 0.47
Nodes (3): CancellationToken, Task, IDialogService

### Community 216 - "Community 216"
Cohesion: 0.47
Nodes (3): ScrollViewer, ScrollOffsetPreservationHelper, VisualStateGroup

### Community 217 - "Community 217"
Cohesion: 0.33
Nodes (4): Button, FontIcon, Button, FontIcon

### Community 219 - "Community 219"
Cohesion: 0.50
Nodes (3): NotificationFeedTemplateSelector, DataTemplate, DataTemplateSelector

### Community 220 - "Community 220"
Cohesion: 0.40
Nodes (4): LastReceivedUtc, LastSentUtc, ReplyCount, TotalReplyMinutes

### Community 221 - "Community 221"
Cohesion: 0.60
Nodes (3): CancellationToken, Task, IAiInferenceClient

### Community 222 - "Community 222"
Cohesion: 0.50
Nodes (3): GeneratedRegex, Regex, BranchNameResolver

### Community 223 - "Community 223"
Cohesion: 0.40
Nodes (3): DateTimeOffset, TimeSpan, WeeklyReportReminder

### Community 224 - "Community 224"
Cohesion: 0.40
Nodes (4): bool, IReadOnlyList, string, MessageVolumeLineChartViewModel

### Community 225 - "Community 225"
Cohesion: 0.50
Nodes (4): MutedIndicatorVisibility, MutedIndicatorIcon, RecentActivityEmptyIcon, FontIcon

### Community 228 - "Community 228"
Cohesion: 0.67
Nodes (3): string, ClientSentimentLabel, UnifiedMessengerIntentCategory

### Community 232 - "Community 232"
Cohesion: 0.67
Nodes (3): AiModelPullProgressBar, AiRuntimeDownloadProgressBar, ProgressBar

## Knowledge Gaps
- **187 isolated node(s):** `ItemsRepeater`, `ComboBox`, `ItemsRepeater`, `AutoSuggestBox`, `FontIcon` (+182 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **26 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `UnifiedMessenger.Services` connect `Services and Models Namespace` to `GitHub Auto-Update`, `Activity Patterns Chart`, `Community 131`, `Taskbar Badge Service`, `Community 139`, `Community 140`, `Oversight Chat Snapshot`, `Community 149`, `Community 154`, `Community 27`, `Community 155`, `Community 29`, `Community 157`, `Community 158`, `Community 28`, `Community 33`, `Community 34`, `Community 35`, `Community 163`, `Community 36`, `Community 166`, `Community 39`, `Community 167`, `Community 168`, `Community 173`, `Community 175`, `Community 176`, `Community 49`, `Community 53`, `Community 182`, `Community 55`, `Community 56`, `Community 58`, `Community 186`, `Community 61`, `Community 190`, `Community 191`, `Community 63`, `Community 192`, `Community 198`, `Community 199`, `Community 71`, `Community 201`, `Community 202`, `Community 74`, `Community 73`, `Community 205`, `Community 78`, `Community 80`, `Community 209`, `Community 212`, `Community 214`, `Community 216`, `Community 218`, `Community 91`, `Community 93`, `Community 97`, `Community 226`, `Community 228`, `Community 103`, `Community 105`, `Community 234`, `Community 106`, `Community 110`, `Community 114`, `Community 115`, `Community 116`, `Community 126`?**
  _High betweenness centrality (0.343) - this node is a cross-community bridge._
- **Why does `MessengerInstance` connect `Messenger Instance Model` to `Community 128`, `Activity Patterns Chart`, `Community 131`, `Main Window WebView Host`, `Instance Store Persistence`, `Account Icon Picker`, `Community 132`, `Community 135`, `Dashboard Snapshot Model`, `Session Lifecycle and Adapters`, `Command Center Panel`, `Community 143`, `Community 142`, `Community 146`, `Instance Registry Service`, `Response Time Tracking`, `Community 21`, `Community 150`, `Community 23`, `Community 22`, `Community 153`, `Community 26`, `Community 154`, `Community 20`, `Community 30`, `Community 32`, `Community 34`, `Community 38`, `Community 41`, `Community 43`, `Community 44`, `Community 173`, `Community 48`, `Community 49`, `Community 50`, `Community 52`, `Community 54`, `Community 187`, `Community 60`, `Community 189`, `Community 65`, `Community 66`, `Community 67`, `Community 70`, `Community 72`, `Community 74`, `Community 76`, `Community 77`, `Community 213`, `Community 87`, `Community 91`, `Community 92`, `Community 94`, `Community 96`, `Community 100`, `Community 230`, `Community 106`, `Community 110`, `Community 111`, `Community 116`, `Community 120`?**
  _High betweenness centrality (0.172) - this node is a cross-community bridge._
- **Why does `ApplicationServices` connect `Community 157` to `Community 128`, `Community 129`, `GitHub Auto-Update`, `Activity Patterns Chart`, `Main Window WebView Host`, `Account Icon Picker`, `Settings Page`, `Taskbar Badge Service`, `Dashboard Snapshot Model`, `Community 135`, `Community 140`, `Command Center Panel`, `Instance Registry Service`, `Community 149`, `Community 21`, `Community 153`, `Community 25`, `Community 28`, `Community 29`, `Community 165`, `Community 38`, `Community 42`, `Community 171`, `Community 44`, `Community 174`, `Community 47`, `Community 46`, `Community 178`, `Community 57`, `Community 60`, `Community 61`, `Community 190`, `Community 62`, `Community 69`, `Community 200`, `Community 74`, `Community 76`, `Community 77`, `Community 213`, `Community 214`, `Community 89`, `Community 221`, `Community 93`, `Community 97`, `Community 105`, `Community 116`, `Community 120`?**
  _High betweenness centrality (0.165) - this node is a cross-community bridge._
- **What connects `ItemsRepeater`, `ComboBox`, `ItemsRepeater` to the rest of the system?**
  _187 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `WhatsApp DOM Scraper` be split into smaller, more focused modules?**
  _Cohesion score 0.06829488919041157 - nodes in this community are weakly interconnected._
- **Should `Services and Models Namespace` be split into smaller, more focused modules?**
  _Cohesion score 0.06246799795186892 - nodes in this community are weakly interconnected._
- **Should `GitHub Auto-Update` be split into smaller, more focused modules?**
  _Cohesion score 0.07597895967270601 - nodes in this community are weakly interconnected._