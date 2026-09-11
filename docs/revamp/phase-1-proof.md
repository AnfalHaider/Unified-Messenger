# v6 revamp · Phase 1 proof build results

Run 2026-09-11 → 2026-09-12. The proof build is throwaway code kept **outside** this repository in
`D:\Projects\um-v6-proof` (plain Electron 44.3.0, about 200 lines); its log lives in `D:\um-v6-proof-data`
and records counts only. Plan and roadmap: the "Revamp Blueprint" artifact.

| Step | What it proves | Result | Evidence |
|---|---|---|---|
| 1 | A minimal Electron app hosts accounts | Done | `main.js`; plain Electron, not the Forge template (Forge adds nothing this phase tests) |
| 2 | A WhatsApp login survives a restart | **Passed** | Owner linked by QR, restarted; first read after restart returned chats with no new scan |
| 3 | Today's `whatsapp-store-bridge.js` runs unchanged inside Electron | **Passed** | 13/13 reads returned chats, 12 of them while the account was hidden |
| 4 | 8 accounts awake for an hour | Skipped by owner | Not enough accounts to sign in. Two awake accounts used about 1.9 GB (avg 1,916 MB, max 2,134 MB) |
| 5 | Today's `instagram-adapter.js` reads the inbox | **Passed** | Every read after login returned chats |
| 6 | Google sign-in into Firebase | **Passed** | Loopback + PKCE in the system browser, then Firebase `accounts:signInWithIdp` over REST; user created in project `unified-messenger-5549a` |
| 7 | The assistant answers owner questions | Reduced by owner to one model | `gemma3:4b`, 10 questions on synthetic data: 7/10. Every figure and name given was correct; misses were an omitted location, a thin review summary, and drafting a message instead of saying it cannot send. First answer 52 s (model load), then about 1 s |
| 8 | Playwright drives the app | **Passed** | `playwright-core` `_electron.launch`, clicked a button, read the status |

## Findings to carry into Phase 2

- **WhatsApp accepts Electron once the `Electron/…` token is stripped from the user agent.** A signed-out
  self-test showed the normal QR page, not "update your browser".
- **Reads are scheduled by the app, not by timers inside pages.** Hidden accounts kept returning chats.
- **Write app data outside `%APPDATA%` while agents run the app.** The proof sets Electron's `userData` to
  `D:\um-v6-proof-data`; an agent shell inside an MSIX container would otherwise fork the store.
- **Two Ollama installs exist on the owner's PC with separate model folders.** The v5 bundled runtime
  (`%LOCALAPPDATA%\UnifiedMessenger\ollama`) holds `llama3.2:3b`; the standalone install holds `gemma3:4b`.
  Whichever starts first owns port 11434, so "model not found" depends on start order. v6 must use one known
  Ollama with one model folder.
- **Chrome downloads to the OneDrive Desktop on this PC.** Any credential file downloaded through the browser
  is uploaded to OneDrive before it can be moved.
- **Firebase is on the free Spark plan with no Cloud Functions.** Google sign-in's consent screen was created
  by Firebase itself (External, in production, name and email only), so no Google review was needed.
- **Memory is the number to watch.** Measure v5 on the same two accounts before Phase 2 decisions about
  keeping every account awake by default.
- One self-test run exited at 30 s having logged only its startup; an identical rerun completed. Cause unknown.

## Where the credentials are

`firebase-config.json` (public web config) and `oauth-client.json` (Desktop OAuth client) stay in the proof
folder and must never be committed. Firebase web API keys identify the project; Google does not treat a
Desktop client secret as confidential, but it is still kept out of the repository.
