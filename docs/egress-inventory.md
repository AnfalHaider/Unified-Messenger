# Egress inventory

**As of:** 2026-09-03 · **Baseline:** v4.99.78 · Verified against the tree, not asserted.

The second hard constraint in [`AGENTS.md`](../AGENTS.md) is that **zero oversight data leaves the
machine** — no telemetry, no analytics, no crash upload, ever. That is a claim about every byte the app
can put on a socket, and until now it was written down rather than demonstrated. This file is the
demonstration: every outbound path the app can open, what rides on it, and how each was checked.

Re-derive it, don't trust it. Each row names the command that produces it.

---

## 1 · Sockets the app opens itself

Found with `grep -rn "new HttpClient\|HttpClient(" --include=*.cs UnifiedMessenger/`. **Three clients**, and
no other type in the app constructs a socket, `WebRequest`, `TcpClient`, `Socket`, or `UdpClient`. Four
outbound *paths* across those three — rows 3 and 4 share one client, so the manifest fetch added no new
client, no new host and no new header.

| # | Caller | Destination | Verb | Request body | App-derived data on the wire |
|---|---|---|---|---|---|
| 1 | `Services/Ai/OllamaInferenceClient.cs` | `http://127.0.0.1:<port>/api/*` | GET, POST | prompts | **Yes — and it never leaves the machine.** Loopback only; the endpoint is a setting whose default is `http://127.0.0.1:11434/`. |
| 2 | `Services/Ai/OllamaRuntimeService.cs:357` | `https://github.com/ollama/ollama/releases/download/…` | GET | none | None. Constant URL, no query built from anything the app knows. |
| 3 | `Services/Distribution/GitHubUpdateService.cs` (5 call sites) | `https://api.github.com/repos/…` | GET ×5 | none | None. Constant URLs; the only header carrying anything is `User-Agent`, a literal const `"UnifiedMessenger-Updater/1.0"` at line 25. |
| 4 | `GitHubUpdateService.DownloadManifestAssetAsync`, driven by `Services/Distribution/SelectorManifestUpdater.cs` | a `selectors-<platform>.json` release asset on a GitHub host | GET | none | None. See below. |

Every request in rows 2, 3 and 4 is a `GetAsync`. The app's own code contains **no** `PostAsync`, `PutAsync`,
`PatchAsync` or `SendAsync` call at all — the one POST in the product is issued inside OllamaSharp, on the
loopback endpoint of row 1:

```bash
grep -rnE "\.(PostAsync|PutAsync|PatchAsync|SendAsync)\(" --include=*.cs UnifiedMessenger/
```

**On row 4, the selector-manifest fetch (added v4.99.78).** This is the newest outbound path and the one
most worth being suspicious of, because it exists so that a file from outside the build can change what
the scrapers read. What it is, precisely:

- **One GET, no request body, no query.** The URL is a `browser_download_url` taken from the release
  payload row 3 already fetches, so finding it costs no extra request. Headers are the client's defaults
  plus the same constant `User-Agent`. **There is no field in the request built from anything the app has
  read, derived, or stored** — no instance id, no account name, no counts, no version of anything the
  owner's customers touch.
- **Host allowlist, not just HTTPS.** `SelectorManifestUpdater.IsTrustedAssetUrl` requires the host to be
  github.com / api.github.com / *.githubusercontent.com. Row 3's installer download checks only that a
  URL is HTTPS; that is looser than it should be, and this path deliberately does **not** inherit the gap.
  `SelectorManifestUpdaterTests.TheHostCheckIsStricterThanTheInstallerPath` asserts the difference so it
  cannot be "simplified" away.
- **Read cap before parse.** 256 KB, enforced against the stream and not merely against `Content-Length`,
  so a server that lies about its length does not get to decide how much is read.
- **Data, not code.** A manifest carries CSS selector strings, colour values and booleans. No field can
  hold a URL, a script or a path, and the validator rejects a candidate containing markup, a
  `javascript:` scheme or `expression(` rather than trusting their absence.
- **Trust argument.** The same release serves the installer. Anyone able to publish a hostile manifest
  here could publish a hostile **executable** on the identical channel, which is strictly worse — so this
  adds no trust the product does not already extend, and no new host.
- **It rides the owner's existing choice.** It runs inside the update check, which is gated on
  `EnableAutoUpdate`. An owner who has turned updates off gets no new outbound request.

**On the Ollama prompt path.** This is the one place customer text is put into a request, and it is the
one place that is on-box. Two prompt builders exist and they are not equivalent — see `AGENTS.md`:

- `OversightInsightService` sends **aggregate counts only** — no names, no message text.
- `AiInferenceQueue` → `TranscriptBuilder.Build` sends the **customer name** and up to **800 characters of
  message body**. This is permitted because Ollama is localhost, and the Settings copy says so in the
  owner's own words. Any analysis that assumes "aggregates only" for the whole AI layer is wrong.

---

## 2 · Sockets WebView2 opens

Each account is a WebView2 with its own profile (`MessengerInstance.ProfileName`, default
`"{platform}-{id}"`), inside one shared `CoreWebView2Environment`. What those pages fetch is the owner's
own session with their own messaging provider — the traffic a browser tab would make anyway. It is not
app-originated egress and the constraint has never covered it.

What matters is the reverse direction: **can anything the app derives be pushed into a page?** Every
injection point was enumerated:

```bash
grep -rn "ExecuteScriptAsync(" --include=*.cs UnifiedMessenger/     # 39 sites
```

All 39 pass either a `const` scraper script or a script built by `WebViewScriptBuilder`. Only **two** carry
any value at all:

| Path | What is injected | Assessment |
|---|---|---|
| `InstanceSessionManager.BroadcastAdapterSettingsCoreAsync` | `window.__umIncludeMutedBadges = true\|false` | One boolean preference. Not customer data, not a metric. |
| `ConversationFocusHelper` → `__umFocusConversation` | platform, conversation key, customer name, contact phone | Returned to the page it was scraped from, to scroll that account's own chat list to that chat. Nothing crosses accounts. |

Both are JSON-serialized by `WebViewScriptBuilder.BuildFunctionCall`
(`Services/Session/IWebViewScriptGateway.cs`), which serializes the function name as well as every
argument — so a conversation key containing a quote cannot break out of the call. No `ExecuteScriptAsync`
call site anywhere builds its script with string interpolation:

```bash
grep -rnE 'ExecuteScriptAsync\(\s*\$"' --include=*.cs UnifiedMessenger/   # no matches
```

**Navigation is allowlisted.** `WebViewNavigationGuard` cancels navigation to any host outside the
allowlist, which is per-WebView and captured in the handler's own closure — see the note in that file
about why it must not be a table lookup. On a real launch it logs what it attached with:
`[WebView.Nav] Navigation guard attached: allowAllHosts=False hosts=19`.

---

## 3 · Things that are not egress but look like it

| Thing | Why it is not |
|---|---|
| `app.log` | A local file under `%LOCALAPPDATA%\UnifiedMessenger`, rotated at 256 KB. Nothing uploads it. The owner may choose to send it, which is why scraped payloads, customer names and message text must never be written to it — payload **lengths** are logged instead (`PlatformAdapters.HandleWebMessage`, `WebMessageIngressService`). |
| Local backup / export | `LocalBackupService`, and the CSV/Markdown/PNG exports on Reports and Analytics, write to a path the owner picks in a file dialog. |
| A browse tab | The owner's own request, in that account's own WebView2 profile. Oversight data must never reach one — see §2, which is where that is enforced. |
| Windows toasts and the taskbar badge | Local shell APIs. No network. |

---

## 4 · What has *not* been demonstrated

Stated plainly, because an inventory that overclaims is worse than none.

- **This is a static read of the tree, not a packet capture.** No proxy or firewall log was used to
  confirm that the running app opens only these sockets, and nothing here should be read as saying one was.

  > **Correction, 2026-08-28.** This bullet previously asserted that the machine had no network, and quoted
  > an `[Update] Check could not reach GitHub: No such host is known. (api.github.com:443)` line as
  > evidence. **That line was never observed in any `app.log` read while writing this file**, and the claim
  > is false — `git push`, `api.github.com` and the GitHub Actions API all work from here. It was carried in
  > from a stale premise rather than measured, which is precisely the failure this document exists to avoid,
  > and it is recorded rather than quietly deleted because a corrected document is worth more than one that
  > was never wrong on paper.

- **WebView2 makes its own requests the app does not control** — component updates, certificate revocation
  checks, and whatever the loaded page does. These are Edge's, not the app's, and carry nothing the app
  derived; but they are not enumerable from this repository.
- **Sign-in state varies by account.** At least one WhatsApp account is signed in and scanning — its
  reply-time store holds real samples and pending waits. Others sit on login screens. So a real scrape
  *has* been observed for one account, and not end to end across all of them.

## How to re-check this after a change

```bash
grep -rn "new HttpClient\|HttpClient(" --include=*.cs UnifiedMessenger/
grep -rnE "\.(PostAsync|PutAsync|PatchAsync|SendAsync)\(" --include=*.cs UnifiedMessenger/
grep -rnE 'ExecuteScriptAsync\(\s*\$"' --include=*.cs UnifiedMessenger/
grep -rhoE "https?://[a-zA-Z0-9._/-]+" --include=*.cs UnifiedMessenger/ | sort -u
```

A new hit in the first two, or any hit in the third, is a change to the app's egress surface and needs a
line in this file.
