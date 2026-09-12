# Channels

One folder per channel, each providing the same four things. The point is containment: a channel whose page
changed overnight costs its own figures and nothing else, and the app says which one went quiet instead of
quietly averaging it away.

| Part | What it is | Where it runs |
|---|---|---|
| `inject` | Page script that installs the reader | In the account's page |
| `scan` | An expression returning the reader's JSON | In the account's page |
| `signedOutProbe` | An expression saying whether the page is asking for a login | In the account's page |
| `parse` | Turns that JSON into `ChatEntry[]`. **Never throws.** | In the app |

`parse` may also return `notReady`, which means the page has not brought its reader up yet — WhatsApp Web
builds its stores several seconds after the page loads. Only the module can tell that apart from a genuinely
empty inbox, and the difference is the whole health line: a page still starting must never be reported as a
reader that broke.

The module itself is data plus a pure function: no file reads, no network, no Electron. That is what makes it
testable against made-up page output, and it is why `inject` is handed a `load` function rather than reaching
for the disk.

```
channels/
  types.ts              the contract
  index.ts              the registry and per-module health
  whatsapp/index.ts     store bridge: decrypted in-memory models, so previews exist
  instagram/index.ts    the inbox list only — opening a chat would mark it read
  reader.test.ts        made-up page output for every module
```

## Rules

**Never open a conversation to read it.** Opening marks it read and tells the customer you saw it. Anything a
channel cannot see without opening is simply not reported, and the screens say so.

**A bad read costs one read.** `parse` returns an empty result with `skipped` counted rather than throwing;
the shell catches per account, so one broken channel cannot stop the others. `reader.test.ts` pins this.

**Never test against real customer data.** Fixtures here are invented. Real data is only ever read live, on
the owner's machine.

**Empty is not quiet.** When a read returns nothing, the shell asks `signedOutProbe` before believing it, and
a page asking for a login is recorded as signed out — never reported as an account with nobody waiting. The
order matters: sign-in is asked **first**, because a signed-out WhatsApp has no store to read and would
otherwise look like a reader that is still starting.

**A reader is judged per channel, not per account.** `ModuleHealth` counts good and failed reads for the
module, because a page that changed shape changed for every account on that channel. Only an empty read from
a page that is signed in and whose reader is up counts as a failure; the app shows the result in
Settings › Accounts › Channel readers.

## Adding a channel

1. Write `channels/<name>/index.ts` against `ChannelModule`.
2. Add it to `MODULES` in `channels/index.ts`.
3. Add fixtures to `reader.test.ts` — at least: a good read, a malformed one, and an empty one.
4. Add the channel to `CHANNELS` in `core/config.ts` with `reads: true`.

A channel in `core/config.ts` with `reads: false` is one the app can show but cannot measure. Its description
must say so, and its accounts show no figures rather than zeroes.

## Still the v5 files

`inject` currently loads the reader scripts from the v5 tree (`UnifiedMessenger/Assets/Scripts`). They are the
shipped, proven readers; copying them here before this phase's work would have forked them. Moving them in is
the last step of this phase, once each has its own fixtures.
