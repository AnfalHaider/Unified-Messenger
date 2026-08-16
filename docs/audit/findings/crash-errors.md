# Findings — Crash, shutdown, and error surface

Audited directly by the orchestrator after the delegated agent for this domain failed twice without
producing output. Coverage is therefore **partial and targeted**, not exhaustive — see
"What was not covered" at the end. Everything asserted here was read at HEAD of `audit/product-hardening`.

## Summary counts

| Pattern | Sites | Note |
|---|---|---|
| Literal empty `catch {}` / `catch (Exception) {}` | **0** | Genuinely good hygiene; better than expected for 80k lines |
| `async void` total | 67 | 65 are legitimate XAML event handlers |
| `async void` that is **not** an event handler | **2** | `CommandCenterPanel.xaml.cs:2371`, `MainWindow.xaml.cs:276` |
| `.Result` / `.Wait()` / `.GetAwaiter().GetResult()` | 6 raw hits | 4 are false positives (`dialog.Result` is a property, not `Task.Result`) |
| Genuine blocking-await sites | **2** | `ApplicationLifecycleService.cs:30`, `InstanceRegistryService.cs:227` |

The blanket "80k lines will be full of swallowed exceptions" assumption did **not** hold. The error
handling in this codebase is consistently `catch (Exception ex) { Debug.WriteLine(...); AppLogger.LogError(...); }`
— logged, never silent-empty. The defects below are structural, not sloppy.

## Global exception handling verdict

Only **one** of the three hooks a WinUI 3 process needs is wired. See `F-ORCH-03` in
`orchestrator.md` for the full finding — summarised here so this file stands alone:

- `Microsoft.UI.Xaml.Application.UnhandledException` — **wired** (`App.xaml.cs:17`), logs and deliberately
  leaves `Handled = false` so the process terminates. No user-facing message is shown before it dies.
- `AppDomain.CurrentDomain.UnhandledException` — **not wired**.
- `TaskScheduler.UnobservedTaskException` — **not wired**.

---

### F-CRASH-01 — A single failing store aborts the shutdown flush, silently discarding six other stores' unsaved state

- **Severity:** S1
- **Confidence:** confirmed (control flow read directly; the data-loss consequence follows necessarily from
  the structure — I have **not** yet forced a flush failure to watch data vanish, so the *mechanism* is
  confirmed and the *observed loss* is not)
- **Where:** `UnifiedMessenger/Services/ApplicationLifecycleService.cs:97-116`
- **User-visible symptom:** The owner closes the app at the end of the day. Next morning, work is missing
  and inconsistently so — chats they marked handled are waiting again, snoozes have expired early,
  response-time history has a hole, KPI sparklines have a gap. Because the losses are partial and vary by
  which store failed first, it reads as "the app is flaky" rather than a reproducible bug, so it will
  never be reported precisely. Worse, the awaiting-override loss actively re-surfaces work the owner
  already dealt with, which destroys trust in the one number the product exists to provide.
- **Repro:**
  1. Make the first flush in the chain fail — e.g. hold `messageanalytics.json` open with a writer lock,
     or fill the disk, or corrupt the target directory's permissions.
  2. Close the app.
  3. Observe: `Lifecycle.Flush` is logged once, and `ResponseTimeTracker`, `ContactHistoryStore`,
     `AwaitingOverrideStore`, and `KpiTrendStore` never flush at all.
  (Not yet executed — see confidence note.)
- **Root cause:** Seven sequential `await`s share **one** `try` block:
  ```csharp
  try {
      await services.MessageAnalytics.FlushAsync(ct);       // if THIS throws...
      await services.TriagePersistence.FlushAsync(ct);      // ...none of these
      await OversightChatSnapshotService.Instance.FlushAsync(ct);
      await ResponseTimeTracker.Instance.FlushAsync(ct);
      await ContactHistoryStore.Instance.FlushAsync(ct);
      await AwaitingOverrideStore.Instance.FlushAsync(ct);  // ...ever run
      await KpiTrendStore.Instance.FlushAsync(ct);
  } catch (Exception ex) { Debug.WriteLine(...); AppLogger.LogError("Lifecycle.Flush", ex); }
  ```
  The first throw unwinds past every remaining flush. The catch logs one line and returns normally, so
  shutdown proceeds and the app exits reporting success. Nothing tells the user anything was lost.
- **Proposed fix:** Give each flush its own `try`/`catch` so one failure cannot cancel the others, and
  aggregate the failures. Persistence is exactly the place where best-effort-per-item beats fail-fast.
  Prefer a small loop over a list of named flush delegates so a future store cannot be added without
  inheriting the isolation. Surface a single plain-language warning on next launch if any store failed to
  persist — the user needs to know their overrides may be stale. **Constraint check:** local warning only;
  no crash upload, no telemetry (constraint 2).
- **Blast radius:** `ApplicationLifecycleService` only. `FlushPersistentStateAsync` has two callers
  (`ShutdownAsync:71` and `FlushPersistentStateFireAndForget:19`), both of which benefit identically.
  Low-risk, high-value — this is the single best fix-to-effort ratio found so far.
- **Evidence:** `ApplicationLifecycleService.cs:101-115`, quoted above verbatim.

---

### F-CRASH-02 — Window close blocks the UI thread on async shutdown work that must run on that same thread, risking an intermittent hang on exit

- **Severity:** S2
- **Confidence:** likely (every link in the chain is confirmed by reading; I have **not** reproduced a
  hang, and the code contains a deliberate mitigation that makes it intermittent rather than certain —
  see below. I am explicitly not claiming `confirmed`.)
- **Where:** `UnifiedMessenger/MainWindow.xaml.cs:405-411` (`OnMainWindowClosed`, a UI-thread handler)
- **Where:** `UnifiedMessenger/Services/ApplicationLifecycleService.cs:30` (`ShutdownAsync().GetAwaiter().GetResult()`)
- **Where:** `UnifiedMessenger/Services/Session/InstanceSessionManager.cs:263` (`CloseAllSessionsAsync` → `UiThreadRunner.RunAsync`)
- **Where:** `UnifiedMessenger/Services/UiThreadRunner.cs:44-63`
- **User-visible symptom:** Sometimes the window closes and the process stays alive, or the window hangs
  mid-close. The owner opens Task Manager and force-kills it. Because the app is a tray-resident monitor
  meant to run all day, a process that will not die is more than an annoyance — the single-instance mutex
  (`UnifiedMessenger_AppMutex`) means the **next launch is silently swallowed**, and the app simply
  appears not to start. That symptom is nearly impossible for a non-technical owner to diagnose.
- **Repro:** `static-analysis-only` — not reproduced. Reproducing likely needs a session mid-navigation
  when close is pressed. Deferred to Wave 5 verification.
- **Root cause:** `OnMainWindowClosed` runs on the UI thread and calls
  `ShutdownAsync().GetAwaiter().GetResult()` — a synchronous block on the UI thread. `ShutdownAsync` then
  calls `CloseAllSessionsAsync`, which routes through `UiThreadRunner.RunAsync` because WebView2 objects
  are UI-thread-affine. Continuations that resume on the dispatcher cannot run while the dispatcher is
  blocked in `GetResult()`.
  **The mitigation that makes this intermittent rather than certain, and why it is not a fix:**
  `UiThreadRunner.RunAsync` checks `dispatcher.HasThreadAccess` and, when already on the UI thread, runs
  the work **inline** (`UiThreadRunner.cs:47-50`) instead of enqueuing — so the outer hop does not
  deadlock. But the inline path is `await action().ConfigureAwait(true)`, and `UiThreadRunner`'s own class
  comment states the problem exactly: *"WinRT awaitables often resume on thread-pool threads even when
  ConfigureAwait(true) is used."* Resumption is therefore **not deterministic** — some WebView2 disposal
  awaits resume on the pool (fine) and some attempt the blocked dispatcher (hang). That is precisely the
  profile of a bug that passes every test run and hangs on a customer's machine.
  Note the ordering is fortunate and worth preserving: `FlushPersistentStateAsync` runs at line 71,
  **before** `CloseAllSessionsAsync` at line 78, so a hang here happens *after* state is persisted. That
  is why this is S2 and not S1.
- **Proposed fix:** Do not block the UI thread. Move shutdown off the closing handler — either cancel the
  close, run `ShutdownAsync` properly awaited, and close on completion; or perform session teardown with a
  bounded timeout on a background thread and let the process exit regardless. A hard upper bound matters
  more than clean teardown here: WebView2 processes are reaped by the OS anyway, so a timeout that
  guarantees exit is strictly better for the user than a clean close that might hang forever.
  `WorkerShutdownTimeout` (2s) already exists for the worker queues; sessions have no equivalent bound.
- **Blast radius:** `MainWindow.OnMainWindowClosed`, `ApplicationLifecycleService.TryShutdownOnWindowClosed`,
  and session teardown. This is genuinely delicate — shutdown ordering, the tray-quit path
  (`MainWindow.xaml.cs:276 RequestTrayQuit`, one of the two non-event-handler `async void`s), and the
  single-instance mutex all interact. Needs its own increment and careful verification, not a drive-by fix.
- **Evidence:**
  ```
  MainWindow.xaml.cs:405   private void OnMainWindowClosed(object sender, WindowEventArgs args)
  MainWindow.xaml.cs:408       ApplicationLifecycleService.TryShutdownOnWindowClosed(...);
  ApplicationLifecycleService.cs:30    ShutdownAsync().GetAwaiter().GetResult();
  ApplicationLifecycleService.cs:78    await sessionManager.CloseAllSessionsAsync(ct).ConfigureAwait(false);
  InstanceSessionManager.cs:263        UiThreadRunner.RunAsync(() => CloseAllSessionsCoreAsync(ct));
  UiThreadRunner.cs:47-50              if (dispatcher.HasThreadAccess) return await action().ConfigureAwait(true);
  UiThreadRunner.cs:6-7 (class comment) "WinRT awaitables often resume on thread-pool threads
                                         even when ConfigureAwait(true) is used."
  ```

---

### F-CRASH-03 — Hide-to-tray fires the state flush and immediately abandons it

- **Severity:** S3
- **Confidence:** confirmed (mechanism); the practical loss window is `suspected`
- **Where:** `UnifiedMessenger/Services/ApplicationLifecycleService.cs:18-19`
- **Where:** `UnifiedMessenger/MainWindow.xaml.cs:400`
- **User-visible symptom:** Low in isolation. Closing to tray starts a flush that nobody waits for; if the
  machine sleeps, the user signs out, or the process is killed shortly after, the most recent overrides
  and KPI updates are lost. The owner would experience this as the occasional "I marked that handled
  yesterday" — indistinguishable from F-CRASH-01 and compounding it.
- **Repro:** `static-analysis-only`
- **Root cause:** `FlushPersistentStateFireAndForget()` is `_ = FlushPersistentStateAsync();` — the task is
  discarded. Nothing observes completion or failure. Any exception is caught inside, so this is not an
  unobserved-task crash; it is simply an unbounded, unawaited write.
- **Proposed fix:** Low priority and worth being honest about the tradeoff: hide-to-tray must stay
  instant, so awaiting it inline is wrong. Track the task in a field so a subsequent real shutdown can
  await the in-flight flush instead of racing it, and so two rapid hide events cannot interleave writes to
  the same files. Fixing F-CRASH-01 first matters more.
- **Blast radius:** `ApplicationLifecycleService`. Interacts with F-CRASH-01 — fix that one first.
- **Evidence:** `ApplicationLifecycleService.cs:18-19`, `MainWindow.xaml.cs:399-401`.

---

## What was NOT covered

Stated plainly so no one reads this file as a completed domain audit. The delegated agent for this domain
died twice before writing anything; what is above is what one orchestrator pass reached.

- **Not examined:** nullable dereferences reachable from scraped data; unguarded `dict[key]`, `.First()`,
  `.Single()`, `[0]`, `.Max()`/`.Min()` on possibly-empty sequences; `int.Parse`/`DateTime.Parse` on
  external strings. This is the category most likely to hold additional S1s, because scraped JSON changes
  without notice. **This is the biggest known gap in the audit.**
- **Not examined:** `JsonSerializer.Deserialize` / `JsonDocument.Parse` on on-disk state without
  try/catch — i.e. what a corrupt `settings.json` or a half-written store file does on launch. Directly
  relevant to F-CRASH-01, since that finding is about writes and this would be the matching read failure.
- **Not examined:** timer/background callbacks throwing on threadpool threads; event handlers never
  unsubscribed; use-after-dispose on `CoreWebView2` or disposed `CancellationTokenSource`.
- **Not verified by execution:** every finding here is from reading. No crash was reproduced, no hang was
  observed, no malformed input was fed to a parser. F-CRASH-01's mechanism is certain; its consequence is
  inferred. F-CRASH-02 is explicitly `likely`, not `confirmed`.
- **The 65 event-handler `async void` sites were counted, not reviewed.** An unhandled throw in any of
  them terminates the process, and with `AppDomain.UnhandledException` unwired (F-ORCH-03) it would do so
  without a message. Reviewing them is real remaining work.
