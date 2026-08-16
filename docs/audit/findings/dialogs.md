# Findings — Dialogs opened live

Session 2, `v4.99.23`. Session 1 verified all 12 dialogs **structurally** (close path, accessible name on
the dialog itself, no dead ends) but had only ever *opened* two of them. This pass opened five more in the
running app and read the UI Automation tree — the same tree a screen reader consumes.

## What was opened, and what was not

| Dialog | Opened live? | Verdict |
|---|---|---|
| `AddInstanceDialog` | session 1 | — |
| `WeeklyReportDialog` | session 1 | — |
| **`AccountDetailDialog`** | **yes** | **8 unnamed buttons → fixed** (F-DIALOG-02) |
| **`ChangeIconDialog`** | **yes** | **25 unnamed buttons → fixed** (F-DIALOG-01) |
| **`RenameInstanceDialog`** | **yes** | Clean |
| **`DeleteInstanceDialog`** | **yes** | Clean — and notably well written |
| **`WorkspaceManagementDialog`** | **yes** | Clean |
| `SetLocationDialog` | no | Synthetic right-click is flaky; the menu item did not appear on two of three attempts |
| `EditInstanceMetadataDialog` | no | Trigger not located |
| `PinToTaskbarDialog` | no | Only shown once per install, and `HasPromptedPinToTaskbar` is already set |
| `AutoUpdateDialog` | no | Requires a newer release to exist on GitHub |
| `ConfirmPermanentDeleteDialog` | no | **Deliberately not reached** — it is the second step of permanent deletion, and the machine holds the owner's real accounts |

**Five of the ten remaining dialogs were opened. Five were not, and are listed above with the reason.**

## How to drive a dialog from a script

Recorded because it took several attempts and the constraints are not obvious:

- **Enumerate in the same call that opens the dialog.** ContentDialogs close when another process takes
  focus, so a two-call open-then-inspect always reads an empty tree.
- **Menu flyouts are not under the main window.** `MenuFlyoutItem`s live in a separate popup; a
  `FindAll` scoped to the app window returns zero. Search from `RootElement` filtered by `ProcessId`.
- **Sidebar account rows expose no `Invoke` or `SelectionItem` pattern**, so they cannot be activated
  through UIA. Synthetic input is required: `SetCursorPos` + `mouse_event`, with a `MOVE` event and a
  ~400 ms settle before the button-down or WinUI ignores it. It is still only ~⅔ reliable.
- **Never invoke every element named "Close".** That matches the *title bar* Close button and shuts the
  app. It happened here; the app exited cleanly and nothing was lost, but scope the search to the
  dialog's own subtree.
- Menu items are reached by their **accessible** name, which differs from the visible text — "Rename
  instance" vs "Rename instance...", "Remove instance permanently" vs "Remove instance...".

## F-DIALOG-01 — The icon picker announced twenty-five identical "button"s

- **Severity:** S2 · **Confidence:** confirmed (live UIA capture, before and after) · **Status:** **FIXED**

`ChangeIconDialog` builds each icon as a `Button` whose content is an `Ellipse` + `FontIcon`. A WinUI
Button only derives an automation name from *string* content, so all 23 icon choices, plus the two
import/upload rows, came back with no name at all:

```
[Button] ''  <-- UNNAMED INTERACTIVE     (x25)
```

The dialog was operable only by sight. A screen-reader user could tab through two dozen
indistinguishable controls and never learn which was WhatsApp and which was a shopping cart. The names
existed the whole time — as trailing `//` comments beside each glyph.

Fixed with parallel `BrandIconNames` / `GeneralIconNames` arrays feeding
`AutomationProperties.SetName` plus a tooltip. Kept as separate arrays rather than a third tuple field
deliberately: the glyph arrays hold Private Use Area codepoints and the fewer edits near them the better.

**Verified against the running app:**

```
before: buttons: 30   UNNAMED: 25
after : buttons: 32   UNNAMED: 0
names : WhatsApp icon | Telegram icon | Instagram icon | Facebook icon | Messenger icon | X icon |
        TikTok icon | YouTube icon | LinkedIn icon | Discord icon | Pinterest icon | Reddit icon |
        WeChat icon | Google icon | Message icon | Contact icon | People icon | Home icon | Mail icon |
        Star icon | Shopping cart icon | Map pin icon | Settings icon |
        Import this account's profile photo | Upload an image from this PC | Reset to initials | …
```

## F-DIALOG-02 — The per-account drill-down did not name its customer rows

- **Severity:** S3 · **Confidence:** confirmed (live capture, before and after) · **Status:** **FIXED**

`AccountDetailDialog` renders one row per waiting customer: an open-the-chat button on the left, a
"Mark … as done" split button on the right. The split button was named. The open button was not — its
content is a `StackPanel` of `TextBlock`s, so it announced as a bare "button", eight times, each beside a
correctly-named sibling.

What makes this a clean finding rather than an oversight of principle: the command centre's *equivalent*
row already does it right —

```csharp
AutomationProperties.SetName(button, $"Open chat with {displayName} in {inst.DisplayName}");
```

— so the drill-down was simply missed when it was added. Fixed with the identical wording so both
surfaces announce the same.

**Verified against the running app:** `buttons: 14  UNNAMED: 0  named open-chat rows: 8`, e.g.
`"Open chat with Munaza Asghar (F-11 Customer) in Depilex F-11 WhatsApp"`.

## F-ORCH-06 confirmed live in the account context menu

The open S3 about developer vocabulary is not confined to Settings. Every item in the sidebar's
right-click menu uses "instance" as its **accessible name**, which is what a screen reader speaks:

| Accessible name (spoken) | Visible text |
|---|---|
| Move instance to Personal workspace | Move to Personal workspace |
| Set instance location | Set location... |
| Move instance up in sidebar | Move up |
| Move instance down in sidebar | Move down |
| Rename instance | Rename instance... |
| Change instance icon | Change icon... |
| Mute instance notifications | Mute notifications |
| Memory tier submenu | Memory tier |
| Refresh instance WebView | Refresh WebView |
| Remove instance permanently | Remove instance... |

Note the visible text is *already* mostly free of the jargon ("Move up", "Mute notifications") — it is
specifically the accessible names that speak it, so sighted and screen-reader users are being told
different things. Left open, under F-ORCH-06, because renaming these belongs with that finding's sweep
and its caveat: "your local Ollama **instance**" is correct English and must stay.

## Verified CLEAN

| Dialog | Controls | Notes |
|---|---|---|
| `RenameInstanceDialog` | 7 | Labelled `Edit` ("Display name"), Rename + Cancel both named, 0 unnamed |
| `DeleteInstanceDialog` | 9 | 0 unnamed. Genuinely good copy: it explains *both* options and their consequences — "Remove from sidebar keeps your login session on disk so you can restore the account later without scanning the QR code again" and "Permanent delete wipes all cookies, cache, and profile data from disk. This cannot be undone." Cancel present. Opened and dismissed with Escape; **never confirmed** |
| `WorkspaceManagementDialog` | 25 | 0 unnamed. Spinners carry their range in the accessible name (`Minimum5 Maximum120`), time pickers are named, explanatory text present |
| `AccountDetailDialog` (rest) | — | Dialog itself named for the account; "+ 140 more" is an honest truncation marker; Close and "Open WhatsApp" both named |

## What was NOT covered

- **The five dialogs listed as not opened**, above.
- **Empty states.** The handoff asked what the destructive dialogs do with 0 accounts. That cannot be
  staged on this machine without deleting the owner's real accounts, and the dialogs take an instance as
  a constructor argument, so the 0-account case is a question about whether the *menu item* is reachable
  at all, not about the dialog. Not answered.
- **Tab order inside any dialog** was not walked — only the tree was read.
- **No screen reader was actually run.** As in session 1, this reads the UIA tree those tools consume,
  which is necessary but not sufficient.
