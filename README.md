# Unified Messenger

A Windows app for a business with several WhatsApp, WhatsApp Business, Instagram and Google accounts. It
watches the accounts you are already signed in to and shows **who is waiting for a reply**, for how long, and
at which branch — with reply times, backlog, missed calls and Google reviews beside them.

**It reads; you reply.** The app never sends a message, never replies to a review, and never clicks anything
inside a chat.

**Current release:** v6.0.0. Release notes in [`CHANGELOG.md`](CHANGELOG.md); installers on the
[Releases page](https://github.com/AnfalHaider/Unified-Messenger/releases).

## What it does

- **The line.** Every unanswered conversation across every account, longest wait first, against the reply
  target you set, with a warning before it runs out. Colour means lateness and nothing else.
- **Your accounts, side by side.** Each stays signed in in its own window, as it would in a browser.
- **Reports** for today, 7 days and 30: reply times, backlog and reopened conversations, missed calls, busy
  hours, each exportable.
- **Google reviews:** the rating, the lifetime total, and which reviews still have no reply.
- **An assistant on the PC**, off until switched on: it answers from the app's own figures and drafts a reply
  for you to send yourself.
- **A workspace, if you want one:** sign in with Google to share the setup — accounts, locations, opening
  hours, targets, saved replies — between your own PCs.

## Where the information lives

Messages, customer names, reply times and the account logins stay on the PC that read them. Nothing is
uploaded, and there is no analytics or crash reporting. A workspace holds only your Google name and address,
when your PC last checked in, and the business setup — never customers, messages, figures or logins.

The privacy policy and the product's home page are in [`v6/site/`](v6/site/).

## Building it

The app is Electron + TypeScript + React in [`v6/`](v6/README.md), which has the full developer guide:
commands, folder-by-folder layout, and how a release is made.

```
cd v6
npm install
npm start            # the app, on this PC's data
npm test             # core and app logic
npm run smoke        # Playwright drives the real app
npm run dist         # dist\UnifiedMessenger6Setup.exe
```

Working on this repository with an AI agent: read [`AGENTS.md`](AGENTS.md) first.

## History

Versions 1 to 5 were a WinUI 3 / .NET app. Version 6 is a rebuild on Electron, so that the same code can run
on other platforms later and each account can keep its own signed-in session cleanly. The v5 source was
retired on 2026-09-20 and remains in git history; what was learned from it is in `docs/memory/lessons/`.
