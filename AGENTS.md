# Unified Messenger — AGENTS.md

## What this project is

A **Windows oversight app** for a business with several WhatsApp, WhatsApp Business, Instagram and Google
accounts. It watches the accounts the business is already signed in to and shows who is waiting for a reply,
for how long, and at which branch — plus Google **reviews**, which is a reviews channel and never a
conversation channel (Google Business Messages was shut down in 2024).

**It reads; the owner replies.** The app never sends a message, never posts a reply to a review, and never
clicks anything inside a chat.

The app is **v6**: Electron + TypeScript + React, in [`v6/`](v6/README.md). Everything else in this
repository is documentation. The previous app (WinUI 3 / .NET, `UnifiedMessenger/`) was retired on
2026-09-20 and lives only in git history — see `docs/revamp/roadmap.md` §7.6 and the lessons that still name
its files, which remain useful reading.

**Hard constraints (never violate):**
- **No paid APIs. No recurring cost to run the app.** Free official APIs are allowed. The only cloud service
  is the app's own free Firebase project: Google sign-in, workspace membership, and business configuration
  (accounts, locations, settings). *(Owner decision 2026-09-11.)*
- **Zero *oversight* data leaves the machine.** Never transmit metrics, message content, customer
  identities, or AI prompts off-box — no telemetry, no analytics, no crash upload, ever. Business
  configuration synced to Firebase is not oversight data, and nothing else goes there. The update check
  (7.3) is a plain GET to GitHub carrying no identifier. A page the owner deliberately opens in the app is
  their own traffic, not app-originated exfiltration — but oversight data must never reach such a page.
- **The app never sends.** Automation is read-only.
- **All AI is on-device via Ollama.** No cloud LLM.
- **Access is by membership.** A workspace decides who may use its configuration; the product owner can
  suspend a member or a workspace. Everyone with access sees the same oversight data on their own machine.
- **No unofficial protocol libraries** (Baileys, whatsmeow, WPPConnect) — ban risk. Read them for DOM and
  protocol knowledge; never vendor their code. GPL/AGPL sources are reference-only; MIT/Apache ones may be
  adapted with attribution in `THIRD-PARTY-NOTICES.md`.
- **Colour means lateness**, nothing else. The product's word is **account**, never "instance"; the product
  is for a **business**, never a salon.

---

## Agent operating rules (read first)

**Read-first order:** this file → [`v6/README.md`](v6/README.md) → on-demand lessons via
`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/mem.ps1 mem "<topic>"` →
[`docs/revamp/roadmap.md`](docs/revamp/roadmap.md) when the task needs the plan or the history of a step.
Do **not** paste lessons into always-loaded rules.

**Branch model:** two branches, `dev` and `main`. Commit on `dev`; when a piece of work is finished, merge it
into `main` and push `main`. `main` is the only branch on GitHub. **Creating or pushing a `v*` tag still needs
explicit owner permission** — a tag publishes a release, and installed copies offer it as an update.

**Gate before asking to push** (from `v6/`):

```
npm run typecheck && npm test && npm run smoke
```

`npm run rules:test` as well when the change touches `cloud/` — it needs Java 21+ and starts the Firestore
emulator (`scripts/rules-run.mjs` finds a JDK).

**Agents present:** Cursor and Claude. One writer per log file under `docs/memory/<agent>/log.md`. Lessons in
`docs/memory/lessons/` are immutable — correct one by adding a new file with `supersedes: [old-id]`, never by
editing it. Tool-specific notes stay in that tool's file (`CLAUDE.md`, `.cursorrules`); shared rules stay
here.

**Memory commands:** `index` · `report` · `mem <topic>` · `check`, all via
`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/mem.ps1 <command>`. Keep the
`-ExecutionPolicy Bypass`: every policy scope here is Undefined (= Restricted), so a plain shell refuses to
load the script. It is process-scoped and changes no setting.

**The agent shell is sandboxed.** Anything it starts (`npm start`, a Setup.exe) reads and writes a private
copy of `%APPDATA%` and `%LOCALAPPDATA%`. To run, install or read the owner's real app, go through
`Invoke-CimMethod -ClassName Win32_Process -MethodName Create`. Never write under the owner's data folder
from an agent shell: it forks the store invisibly, and both paths then read identical.

---

## The app in one page

Everything below has a longer form in [`v6/README.md`](v6/README.md).

| Folder | What lives there |
|---|---|
| `v6/app/` | The Electron main process: window, one signed-in session per account, timers, sign-in, workspace, assistant, updates. |
| `v6/core/` | Pure logic, no Electron: waiting and reply times, business hours, reports, digest, reviews, config, the assistant's facts, the cloud's shapes. Every file has tests beside it. |
| `v6/channels/` | One folder per channel (whatsapp, instagram, google): the scripts injected into the page, and the reader around them. |
| `v6/ui/` | React screens and `tokens.css`. The main process pushes a whole view model; screens compute no figures. |
| `v6/cloud/` | The Firestore rules and their emulator tests. |
| `v6/site/` | The product's public pages (home, privacy policy), for Firebase Hosting. |
| `v6/help/` | The in-app help pages and their pictures. |
| `docs/revamp/roadmap.md` | Status, what each step did, and what is left. Start here for "why is this like this". |

**Build, test and install** (from `v6/`):

```
npm run typecheck      # tsc --noEmit
npm test               # node --test: core and app logic, ~350 tests
npm run smoke          # Playwright drives the real app (needs npx vite build first; the script does it)
npm run rules:test     # Firestore rules + workspace sync against the emulator (Java 21+)
npm run dist           # dist\UnifiedMessenger6Setup.exe
npm run install-local  # builds and installs on this PC (run from the owner's own terminal)
npm run help:shots     # retakes the help pictures after a screen changes
```

From an agent shell, install through `Win32_Process` (see above), then confirm from `app.log`, read the same
way, that the old copy logged `quitting` and `quit`, a new `startup` follows, and each WhatsApp account logs
`read` with its chat count. WhatsApp takes 60–120 seconds after launch to build its stores
(`reader-not-ready`, stage `no-store`) before the first read.

---

## Gotchas worth carrying (each one cost a real failure)

| Gotcha | Rule |
|---|---|
| A test file that needs a running program | `node --test` runs `*-test.ts` as well as `*.test.ts`. Name a script that needs Ollama or an emulator something else (`scripts/assistant-eval.ts`). |
| A "this is refused" test | It can pass because a *different* rule refused it. Write the refused case as the allowed write plus the one thing that must fail, and break the rule on purpose to see the test go red. |
| A Firestore rule for a query | It cannot test the document id; only the fields the query filters on. Test a stored field, and make sure only the right person can store that value. |
| Two spec files against one emulator | They clear each other's data. Run them with `--test-concurrency=1`. |
| Reading the clipboard after a Copy | Copying is asynchronous: `await expect.poll(...)`, and never assume the clipboard started empty. |
| A Playwright test whose data came from a v5 import | Those settings leave "closing keeps reading in the background" on, so `app.close()` hangs. End it with `app.evaluate(({ app }) => app.exit(0))`. |
| The installer seems to do nothing | A shutdown closes every account page in turn and can take 90 seconds; the installer waits two minutes. A `quitting` line *after* the installed files' timestamp means that race. |
| A build that customers install | `scripts/dist.mjs` flips `EnableCookieEncryption` and `OnlyLoadAppFromAsar`. Cookies written before the fuse still work; that was proved, not assumed. |
| Asking a 4B local model to write a figure | It will get it wrong. The model chooses among the app's own facts by id; the app shows those facts word for word. See `core/assistant-summary.ts`. |
| Truncating text a customer wrote | Cut with the helpers, never a raw `slice`: a cut through an emoji leaves a lone surrogate and `JSON.stringify` then throws. |
| Logging a scraped payload | Never. `app.log` carries counts and timings only, so it can be sent to support as it is. |
| A figure that could be wrong | Say what is missing instead. Empty is not zero: a failed read must never read as "caught up". |
| Google reviews | Reviews and Q&A only, forever. Stars are carried in the glyphs' colour, not the codepoints. The rating and lifetime total come from the Search merchant view, paired in one run of text. |
| Anything that must not reach the real Google, Firebase or Ollama | Every test launch points those addresses at fixtures, a closed port, or a fake in the test. Keep it that way. |

---

## Commit convention

```
v6: short description (step N.N — what slice)

Body: what changed and why, and what is deferred and why.
```

Use Bash `git commit -m "..."` (not PowerShell here-strings). Do **not** add `Co-Authored-By` or other
tool-attribution trailers to commits in this repository.

---

## Where things stand

[`docs/revamp/roadmap.md`](docs/revamp/roadmap.md) is the live status: Phases 1–6 are done, Phase 7 is done
apart from this retirement, and what waits on the owner is listed in its §5. `CHANGELOG.md` carries the
release notes a customer reads.
