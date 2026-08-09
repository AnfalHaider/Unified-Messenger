# Findings — Orchestrator (direct verification)

These were found and verified by the orchestrator directly, not by a subagent. Every claim here was
checked against the source at HEAD of `audit/product-hardening`.

## Correction to the audit brief's §7 premise

The brief states: "`PlatformDefinition.All` offers telegram and messenger in the account picker."
**That is not accurate at HEAD.** Verified:

`UnifiedMessenger/Services/PlatformModuleSettingsHelper.cs:54` —
```csharp
private static readonly HashSet<string> HiddenFromPicker =
    new(StringComparer.OrdinalIgnoreCase) { "telegram", "metabusinesssuite", "instagram" };
```

`PlatformDefinition.All` contains **nine** platforms (AGENTS.md documents only six — see F-ORCH-04):
`whatsapp`, `whatsappbusiness`, `googlebusiness`, `telegram`, `messenger`, `discord`,
`metabusinesssuite`, `instagram`, `generic`.

Three are hidden from the picker. So the picker actually offers **six**:

| Platform id | In picker | Adapter | Produces oversight data? |
|---|---|---|---|
| `whatsapp` | yes | WhatsApp adapter | yes — full conversation metrics |
| `whatsappbusiness` | yes | WhatsApp adapter | yes — full conversation metrics |
| `googlebusiness` | yes | NullPlatformAdapter | yes — review metrics via `GoogleReviewSnapshotService` (separate surface) |
| `messenger` | **yes** | NullPlatformAdapter | **no** |
| `discord` | **yes** | NullPlatformAdapter | **no** |
| `generic` | yes | NullPlatformAdapter | no — but this is the honest, understood purpose of "Custom URL" |

**Telegram is already hidden.** The §7 remediation therefore concerns `messenger` and `discord`,
not `telegram`. This is recorded so no one "fixes" a problem that was already solved.

---

### F-ORCH-01 — `PlatformDefinition.Description` is never read, so every capability disclaimer shown in the code is invisible to the user

- **Severity:** S1
- **Confidence:** confirmed
- **Where:** `UnifiedMessenger/Models/PlatformDefinition.cs:9` (property declared)
- **Where:** `UnifiedMessenger/Dialogs/AddInstanceDialog.xaml.cs:37` (`DisplayMemberPath` = `DisplayName` only)
- **User-visible symptom:** In "Add account", the user sees a flat list of platform names — WhatsApp,
  WhatsApp Business, Google Business, Messenger, Discord, Custom URL — with nothing distinguishing the
  two that produce oversight metrics from the three that produce none. A paying customer adds Messenger,
  waits for it to appear on the dashboard, and it never does. There is no error, no empty state, no
  explanation. They conclude the product is broken.
- **Repro:**
  1. Launch the app, open Add account.
  2. Observe the Platform dropdown: names only, no descriptions.
  3. Add a Messenger account. It appears in the sidebar and loads the web client.
  4. Go to the Dashboard. The account contributes nothing — no card, no awaiting count, no on-time %.
  5. Nothing anywhere tells the user this is expected.
- **Root cause:** The `Description` field carries accurate, deliberately-written disclaimers
  ("Discord — embedded. No oversight metrics.", "Meta Messenger — embedded. (Unread/awaiting adapter
  is planned.)") but has **zero consumers**. `grep -rn "\.Description"` across the whole app returns no
  read site. The combo box is bound with `DisplayMemberPath = nameof(PlatformDefinition.DisplayName)`,
  so only the name is rendered. The author's intent to be honest was written down and then never wired up.
- **Proposed fix:** Render the description in the picker via an `ItemTemplate` (replacing
  `DisplayMemberPath`) with the name on line 1 and the description as secondary caption text, plus a
  visible "No oversight metrics" badge for channels where `ContributesConversationMetrics` is false.
  Additionally surface it on the account's own page so it is discoverable after the add flow. Tradeoff:
  the picker becomes taller; acceptable, and preferable to the alternative under §7 of removing the
  channels entirely, since embed-only browsing is a real feature the user may want.
- **Blast radius:** `AddInstanceDialog.xaml`/`.xaml.cs` only for the picker change. The badge on the
  account page touches the shell content host.
- **Evidence:**
  ```
  $ grep -rn "\.Description" UnifiedMessenger/ --include=*.cs --include=*.xaml | grep -i "platform\|Definition"
  (no output — zero read sites)

  $ grep -n "PlatformBox" UnifiedMessenger/Dialogs/AddInstanceDialog.xaml.cs
  35: PlatformBox.ItemsSource = PlatformModuleSettingsHelper.GetSelectablePlatforms(...)
  37: PlatformBox.DisplayMemberPath = nameof(PlatformDefinition.DisplayName);
  ```

---

### F-ORCH-02 — The Google Business picker description contradicts shipped behaviour, calling a shipped feature "planned"

- **Severity:** S3 (would be S2 if the description were rendered; it is currently invisible — see F-ORCH-01)
- **Confidence:** confirmed
- **Where:** `UnifiedMessenger/Models/PlatformDefinition.cs:97`
- **User-visible symptom:** None today, because the string is never rendered. It becomes user-visible the
  moment F-ORCH-01 is fixed — at which point the picker would tell the user that review-metric scraping is
  "planned" for a feature that has shipped since v4.42.0. Fixing F-ORCH-01 without fixing this would
  actively mislead.
- **Repro:** `static-analysis-only`
- **Root cause:** Stale string. The `Description` reads
  `"Google Business reviews — embedded. (Review metrics scraping is planned.)"` while the code comment
  **six lines below it in the same object initializer** states the opposite and is correct:
  `"Google DOES contribute review metrics (GoogleReviewSnapshotService: rating, lifetime total,
  unanswered, reply rate)"`. AGENTS.md's verified-facts section confirms `GoogleReviewSnapshotService.ProfileRating`
  ships. Because nothing consumed the string, the drift was never caught.
- **Proposed fix:** Rewrite to describe what ships: reviews and Q&A oversight — rating, lifetime total,
  unanswered reviews, reply rate — and state plainly that Google has no message channel. Must be fixed
  in the same increment as F-ORCH-01, never after it.
- **Blast radius:** One string. No behaviour change.
- **Evidence:** `PlatformDefinition.cs:97` vs the comment at `PlatformDefinition.cs:103-106`.

---

### F-ORCH-03 — Only the XAML unhandled-exception hook is wired; a background-thread or unobserved-task crash kills the app with no message and no saved state

- **Severity:** S2
- **Confidence:** confirmed (wiring verified by reading; the resulting user experience is `likely` — not yet reproduced)
- **Where:** `UnifiedMessenger/App.xaml.cs:17` (`UnhandledException += OnUnhandledException;` — the only hook)
- **Where:** `UnifiedMessenger/App.xaml.cs:77-91` (the handler)
- **User-visible symptom:** The window vanishes mid-session with no dialog, no explanation, and no
  indication that anything can be recovered. The user reopens the app and has to guess whether their data
  survived. For an app left running unattended all day as a monitoring dashboard, a silent disappearance
  is indistinguishable from "I closed it by accident" — so the user will not even report it.
- **Repro:** `static-analysis-only` for the wiring. Reproducing the vanish requires forcing a
  threadpool-thread throw; deferred to Wave 5 verification.
- **Root cause:** Three separate hooks are needed to cover a WinUI 3 process:
  `Microsoft.UI.Xaml.Application.UnhandledException` (wired), `AppDomain.CurrentDomain.UnhandledException`
  (**not wired**), and `TaskScheduler.UnobservedTaskException` (**not wired**). Grep across `App.xaml.cs`
  and `ApplicationLifecycleService.cs` finds only the first. The handler that does exist logs and then
  deliberately leaves `Handled = false` with the comment *"Leave Handled=false so the process can terminate
  on non-recoverable faults"* — a defensible engineering choice, but it ships **no user-facing message at
  all** before the process dies, which is not defensible in a product a stranger paid for.
- **Proposed fix:** Wire all three hooks to a shared last-gasp handler that (a) flushes the log, (b) best-effort
  persists any dirty state, and (c) shows a plain-language crash notice naming the log file path so the user
  knows where to look and that their data is safe. Keep `Handled = false` for genuinely non-recoverable
  faults — the goal is dignity on the way down, not papering over faults. **Constraint check:** the notice
  must be local-only; per constraint 2 there is no crash upload, ever, and the fix must not add one.
- **Blast radius:** `App.xaml.cs`, `AppLogger`. Touches startup ordering — the hooks must be installed
  before any other initialization can throw.
- **Evidence:**
  ```
  $ grep -rn "UnhandledException\|UnobservedTaskException\|FirstChanceException" \
      UnifiedMessenger/App.xaml.cs UnifiedMessenger/Services/ApplicationLifecycleService.cs
  UnifiedMessenger/App.xaml.cs:17:  UnhandledException += OnUnhandledException;
  UnifiedMessenger/App.xaml.cs:77:  private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs eventArgs)
  UnifiedMessenger/App.xaml.cs:83:      AppLogger.LogError("App.UnhandledException", new Exception(...));
  UnifiedMessenger/App.xaml.cs:88:      AppLogger.LogError("App.UnhandledException", eventArgs.Exception);
  ```
  No `AppDomain` or `TaskScheduler` hook exists anywhere in either file.

---

### F-ORCH-04 — AGENTS.md's roadmap and platform registry are stale by 46 minor versions, and will mislead the next maintainer

- **Severity:** S3
- **Confidence:** confirmed
- **Where:** `AGENTS.md:164` (platform list), `AGENTS.md:294` ("current as of v4.53.0")
- **User-visible symptom:** None for the end user. Included because §9 of the audit requires docs
  accurate to what ships, and because this file is the primary onboarding artifact for anyone maintaining
  the product after purchase — a buyer's technical due diligence would read it and get a wrong picture.
- **Repro:** `static-analysis-only`
- **Root cause:** HEAD is `v4.99.0` (`669e747`); AGENTS.md's roadmap header says "current as of v4.53.0"
  and its shipped-features list stops at v4.53.0. Its platform registry line names six platforms
  (`whatsapp`, `whatsappbusiness`, `googlebusiness`, `telegram`, `messenger`, `generic`) when the code has
  **nine** — it omits `discord`, `metabusinesssuite`, and `instagram` entirely. It also does not mention
  `HiddenFromPicker`, which is the single most important fact about which channels a user can actually add.
- **Proposed fix:** Orchestrator-owned file — I will update it in Wave 6 with the true platform list, the
  `HiddenFromPicker` rule, and a roadmap synced to what actually shipped through v4.99.0.
- **Blast radius:** Documentation only.
- **Evidence:** `git log --oneline -1` → `669e747 v4.99.0: ...` vs `AGENTS.md:294` → "Phase roadmap (current as of v4.53.0)".

---

---

### F-ORCH-05 — The AI layer sends customer names and full message bodies to Ollama; AGENTS.md documents the opposite, and no guard keeps the endpoint on-box

- **Severity:** S2
- **Confidence:** confirmed (code paths read directly; not yet demonstrated with a captured request)
- **Where:** `UnifiedMessenger/Services/Ai/TranscriptBuilder.cs:9-30` (composes name + up to 800 chars of body)
- **Where:** `UnifiedMessenger/Services/Ai/AiInferenceQueue.cs:194` (sole caller)
- **Where:** `UnifiedMessenger/Models/AppSettings.cs:177` (`OllamaEndpoint`, unvalidated string)
- **Where:** `UnifiedMessenger/Services/Ai/OllamaInferenceClient.cs:86,129` (`new OllamaApiClient(new Uri(endpoint))`)
- **Where:** `AGENTS.md:175` (the incorrect "aggregate counts only" claim)
- **User-visible symptom:** None in the default configuration — and that matters, so state it plainly:
  **out of the box there is no leak.** The symptom appears if `OllamaEndpoint` is ever repointed at a
  remote host, at which point every triaged customer's name and message text is POSTed off the machine
  while Settings continues to display "Nothing leaves your PC." The user has no way to tell.
- **Repro:** `static-analysis-only` for the leak. The two facts underneath it are confirmed by reading:
  `TranscriptBuilder` embeds `item.CustomerName` and `item.MessageFullText`; no loopback guard exists.
- **Root cause:** Two separate issues that compound.
  1. **Documentation is wrong.** AGENTS.md's AI-layer section states prompts send "only aggregate counts —
     never customer names or message text". That is true of `OversightInsightService` but there is a
     second, entirely undocumented AI path (`AiInferenceQueue` → `TranscriptBuilder`) that sends both.
     Anyone auditing privacy from AGENTS.md — including a buyer's technical reviewer — would reach a
     wrong conclusion. The in-app Settings string at `SettingsPage.xaml:468` is, by contrast, **accurate**.
  2. **No host validation.** `AppSettings.Validate` (`AppSettings.cs:237-247`) and
     `OllamaOptions.NormalizeEndpoint` only check for empty, trim, and append a trailing slash. A grep for
     `IsLoopback` / `127.0.0.1` / `localhost` across every `.cs` finds no guard on this value anywhere.
  **Verified mitigation — do not report this as UI-editable:** the Settings text box is
  `IsReadOnly="True"` (`SettingsPage.xaml:476-479`) and `SettingsPage.Ai.partial.cs:49` only *writes* to
  it; there is no change handler and no write-back. The endpoint cannot be changed from the UI. The
  exposure is limited to hand-editing `settings.json` on disk, or any process with file access doing so.
- **Proposed fix:** Enforce the invariant in code rather than by convention — validate `OllamaEndpoint` at
  the point of use and reject any host that is not loopback, falling back to the default with a logged
  warning and a visible notice in Settings → AI. This is defence in depth that costs nothing and makes the
  "Nothing leaves your PC" claim structurally true instead of merely conventionally true. Separately,
  correct AGENTS.md to document both AI paths. **Constraint check:** the fix adds no egress and no
  telemetry; it only removes a capability.
- **Blast radius:** `OllamaInferenceClient`, `OllamaModelPullHelper`, `OllamaRuntimeService`, and
  `AppSettings.Validate`. Would break any user deliberately running Ollama on another machine on their
  LAN — that is the tradeoff, and constraint 2 says it is the correct one.
- **Evidence:**
  ```
  $ grep -rn "IsLoopback\|127\.0\.0\.1\|localhost" UnifiedMessenger/ --include=*.cs | grep -v obj/
  (only default-value constants and status strings — no validation of the configured endpoint)

  TranscriptBuilder.cs:
      Customer: {customer}      <- item.CustomerName
      Platform: {item.Platform}
      Message: {message}        <- item.MessageFullText, truncated to 800 chars
  ```
  Referred to the privacy auditor for independent confirmation and for generalisation to other
  user-settable URLs/paths.

---

### F-ORCH-06 — Settings speaks the developer's vocabulary, not the owner's: "instances", "WebViews", and raw filenames are on screen

- **Severity:** S3
- **Confidence:** confirmed
- **Where:** `UnifiedMessenger/Pages/SettingsPage.xaml` (18+ distinct strings; representative sites below)
- **User-visible symptom:** A non-technical business owner opens Settings and reads "Sleep unload inactive
  instances", "Max concurrent WebViews (0 = unlimited)", "Export instances.json", "Refresh all WebViews",
  and "Use tighter thread cards in the Operations Command Center work queue." They do not know what an
  instance, a WebView, or a work queue is. The product calls the same thing an "account" everywhere else —
  in the README, in the "Add account" dialog, in the sidebar — so the user cannot even map the words onto
  what they are looking at. The likely outcome is that they leave memory and session settings alone,
  including the ones that would fix a real performance complaint.
- **Repro:**
  1. Launch the app → Settings.
  2. Read the Session, Data, and Appearance sections.
  3. Count the terms that appear nowhere in the product's own onboarding or README.
- **Root cause:** `MessengerInstance` is the internal model name and it leaked into the presentation layer
  wholesale. `WebView` is the implementation of a session and should never have been a user-facing noun.
  `instances.json` is an internal artifact name used as a button label.
- **Proposed fix:** Retitle to the owner's vocabulary — "accounts" for instances, "account sessions" or
  "background accounts" for WebViews, "Export account list" / "Import account list" for the JSON buttons,
  "Command centre" for "Operations Command Center work queue". **Do not blanket-rename**: at least one use
  is correct English and must stay — "Message text is sent to your local Ollama **instance** only"
  (`SettingsPage.xaml:468`) refers to a server instance, not an account. Rename by review, not by sed.
- **Blast radius:** Display strings only, no model or API renames. `SettingsPage.xaml` plus a few helper
  strings. Zero behaviour change, which makes it low-risk and worth doing.
- **Evidence:**
  ```
  Content="Export instances.json"          Content="Import instances.json"
  Content="Refresh all WebViews"           Header="Max concurrent WebViews (0 = unlimited)"
  Text="Sleep unload inactive instances"   Text="Lazy WebView loading"
  Text="Unload the WebView of instances not used for a while to reduce memory usage."
  Text="Use tighter thread cards in the Operations Command Center work queue."
  Text="After connect, reconcile unread WhatsApp threads into triage and analytics."
  ```

---

### F-ORCH-07 — THIRD-PARTY-NOTICES.md omits half the direct package dependencies

- **Severity:** S3
- **Confidence:** confirmed
- **Where:** `THIRD-PARTY-NOTICES.md`; `UnifiedMessenger/UnifiedMessenger.csproj:88-97`
- **User-visible symptom:** None for the end user directly. It matters because §9 of the audit requires
  third-party notices accurate to what ships, and because incomplete attribution is precisely what a
  buyer's legal due diligence checks. Shipping software with half its dependencies unattributed is a
  defect in the product's paperwork even when every licence is permissive.
- **Repro:** `static-analysis-only`
- **Root cause:** The notices file was written once and not maintained as packages were added. Five of the
  ten direct `PackageReference` entries have no entry: `Microsoft.Extensions.DependencyInjection`,
  `Microsoft.Extensions.AI.Abstractions`, `Microsoft.Windows.SDK.BuildTools`, `System.Text.Json`, and
  `System.Drawing.Common`.
- **Proposed fix:** Add the five missing entries with their licences (all MIT). Orchestrator-owned file —
  I will do this in Wave 6 alongside the version-sync pass. Worth also noting the transitive set, but
  direct references are the minimum bar.
- **Blast radius:** Documentation only.
- **Evidence:**
  ```
  $ for p in ...; do echo "$(grep -ci "$p" THIRD-PARTY-NOTICES.md)  $p"; done
  1  CommunityToolkit.Mvvm          0  Microsoft.Extensions.DependencyInjection
  2  WebView2                       0  Microsoft.Extensions.AI.Abstractions
  1  WindowsAppSDK                  0  SDK.BuildTools
  1  NotifyIcon                     0  System.Text.Json
  1  OllamaSharp                    0  System.Drawing.Common
  ```

---

## Checks that came back CLEAN (recorded so no one re-audits them)

- **Version sync.** All five files agree at `4.99.0`: `UnifiedMessenger.csproj:29-31`,
  `app.manifest:4`, `installer-shared.iss:8`, `README.md:10`, and `docs/phase-status.md` baseline.
  No finding.
- **README's "business-hours aware" FRT claim is backed by real code.** AGENTS.md lists business-hours-aware
  FRT as an unshipped optional follow-up, which would have made the README claim a broken promise — but
  `UnifiedMessenger/Services/Oversight/BusinessHoursCalculator.cs:13` (`ElapsedBusinessMinutes`) exists and
  is consumed by `OversightRollupBuilder`. The README is correct; AGENTS.md is stale (see F-ORCH-04).
  **No finding.** Recorded because this looked like an S1 and was not one.
- **The Ollama endpoint is not user-editable from the UI.** `IsReadOnly="True"`. This knocks the severity
  of F-ORCH-05 down from S1 to S2. Recorded because it is the kind of mitigation an auditor skips.
- **README does not over-promise channels.** It scopes the product to WhatsApp, WhatsApp Business, and
  Google Business reviews, and never claims Telegram, Messenger, Instagram, or Discord support. No finding.

## Tech debt (no user symptom — not findings)

Recorded here for `DEFERRED.md`; none of these are worth a fix on their own.

- `PlatformModuleSettingsHelper.NormalizePlatformModules(AppSettings)` — **zero callers**, and its entire
  body is `ArgumentNullException.ThrowIfNull(settings);`. It does nothing and is called by nothing.
  (`UnifiedMessenger/Services/PlatformModuleSettingsHelper.cs:59`)
- `PlatformModuleSettingsHelper.GetSelectablePlatforms(AppSettings settings)` — the `settings` parameter
  is never used; the method ignores it entirely.
  (`UnifiedMessenger/Services/PlatformModuleSettingsHelper.cs:56`)

## What I could not determine

- Whether the `messenger` and `discord` picker entries were a deliberate "let the owner keep a web app in
  a tab" feature or an oversight. The `generic` / Custom URL entry already covers that use case, which
  argues they are vestigial — but `WebViewPlatformConfigurator` gives Discord a bespoke desktop-Chrome
  user agent and in-app new-window handling specifically so its login flow works, which is real,
  deliberate effort and argues the opposite. This decision is recorded in `ASSUMPTIONS.md` rather than
  guessed at silently.
- Whether the crash path in F-ORCH-03 actually loses unsaved state. I verified the hooks are missing;
  I have **not** yet reproduced a crash and checked what survives. Severity may rise to S1 if state is lost.
