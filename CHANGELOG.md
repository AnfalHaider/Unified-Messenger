# Changelog

All notable changes to Unified Messenger. Newest first.

Release notes and installers for each version are on the
[Releases page](https://github.com/AnfalHaider/Unified-Messenger/releases).

## v4.99.37

**A call you answered is no longer a customer to ring back.** Every entry in a WhatsApp chat that was a
call — not a message — was being counted as a missed call, given a **Call back** button, and added to the
queue. It did not matter how the call actually ended, or even who placed it.

On this workspace that made "Missed calls" the second-largest bucket in the queue, at 86. It is now **36**,
and the total waiting fell from 258 to 206.

Three kinds of entry were wrong:

- **Calls you answered on your phone.** WhatsApp logs these as *"Accepted on another device"*. You had
  already spoken to the customer; the app still asked you to call them back. 24 of these.
- **Calls that connected normally.** 34 of them.
- **Calls you placed.** Ringing a customer back because you rang them is work invented out of nothing.

Genuinely missed calls, and calls that were declined or failed to connect, are still counted — in those
cases the customer did not get what they rang for.

**Where the numbers come from.** WhatsApp records its own verdict on every call, and the app now reads it
rather than assuming. When the verdict cannot be read the call stays counted, because this queue's rule is
that a finished conversation costing you a glance is far better than a live one being dropped.

## v4.99.36

**The app now owns how it looks.** Two of the things that made it feel dated turned out not to be design
choices at all — they were your Windows settings leaking through.

The background tint was your **desktop wallpaper**. The window used a translucent material and the content
area sat directly on it, so whatever colour your wallpaper happened to be washed behind every number on the
dashboard. It looked different on every machine, and it made text contrast unpredictable.

The red on the toggles and the AI button was your **Windows accent colour**. That is why the app felt
permanently agitated: the chrome was painted in the same red the app uses for genuine problems, so real
alerts had nothing left to say. It also meant "selected" and "broken" looked identical.

Both now come from the app: a neutral ground in three levels, and one accent — the app's own blue — for
anything you can click or that is currently chosen. The translucent material stays on the title bar, where
Windows apps actually use it.

**Colour means status again.** Red, amber and green are now reserved for how an account is doing, and
nothing else. Six KPI numbers no longer each pick their own colour; only "Caught up" is coloured, because
it is the one figure that is a verdict.

**Account cards say what they mean.** Instead of an error icon, a large red percentage, and three
differently-coloured pills, each card now shows one plain-language verdict — *"Behind · 13% caught up"* —
and a labelled strip of the numbers underneath:

    REPLY TIME    ANSWERED TODAY    PAST 15M TARGET
    15.7h         4                 112

Nothing was removed; the same figures are there, in a reading order. This is also better for screen
readers than before — the status is now spelled out rather than encoded in an icon shape.

The status stripe down the edge of each card is now a tint rather than a solid bar, so a workspace where
every account is behind no longer opens as a wall of red.

## v4.99.35

**One type scale, one icon scale, one spacing grid — everywhere.** Text across the app was drawn at twelve
different sizes, icons at twelve more, and padding at twenty-nine distinct values that appeared in only
sixty-two places. Nothing looked broken; it looked *slightly* different everywhere, which is the kind of
thing you feel rather than notice.

Text now uses seven steps (11 · 12 · 14 · 16 · 20 · 24 · 32), icons four (12 · 16 · 24 · 40), and every
padding and margin sits on a 4px grid. Nothing below 11px survives — the app previously shipped 9px and
10px text, which is smaller than comfortable on a dashboard read at arm's length.

**The real fix is that it can't drift back.** Roughly half the app draws itself in code rather than markup,
and the design-system tests only ever read the markup. That is how the code-built surfaces came to carry
*more* distinct text sizes than every XAML file combined, in fewer places, with nobody able to see it. They
now draw from the same scale as the markup, the two definitions are checked against each other, and the
tests read both — so a new off-scale number fails the build instead of quietly accumulating.

No feature or number changed. This is purely how things are drawn.

## v4.99.34

**The app can no longer mistake a reading problem for a fresh install.** If Unified Messenger could not
open the file that lists your accounts — because security software was scanning it, or the folder was
briefly unavailable — it treated that as "this person has never used the app". It showed the first-run
welcome screen with a single demo account. Nothing had been lost, but nothing on screen said so, and the
natural reading of that screen is that a business's entire message history is gone. This is the bug behind
"why does my install version not have my data?".

The cause was one line: the check for "is this a first run" could not tell an absent file from an
unreadable one. It now opens the file and reads the actual error, so a missing file, an unparseable file,
and a file it simply could not reach are three different outcomes with three different responses.

**It also cannot lose your accounts that way.** The seeded starter account was one save away from being
written over the real list — renaming an account, adding one, or dragging one to reorder would have
replaced nine accounts with one, permanently and without a prompt. Saving is now refused outright for any
session that never managed to read the list.

**When it does happen, the app says so plainly.** A notice explains that nothing has been lost, names the
exact file so you can see for yourself that it is still there, quotes the underlying error, and offers
"Try again" — which usually works, since these blocks clear within seconds. The sidebar, the dashboard and
the Settings account list all stop claiming you have no accounts.

**Every launch now records which file it read and how many accounts came out of it,** so the next question
like this one is answerable from `app.log` in a single line instead of an evening of guesswork.

Also: an unparseable account file is no longer replaced unless the original bytes were successfully
preserved first, and a file containing a literal `null` is treated as damaged rather than as "no accounts".

## v4.99.33

**You choose where downloads go.** Saving a file now opens Windows' own save dialog, with the filename
already filled in and the file type named in words — "Image", "Voice note", "PDF document" — rather than
as an extension. It remembers the folder you picked, so the next save starts there. Cancelling the dialog
cancels the download rather than saving somewhere you just declined.

Files used to land wherever the built-in browser engine decided, which is a folder you never chose and
cannot easily find.

**Analytics and Reports now say how old their numbers are.** They showed message counts, charts, response
times and a whole written report with no indication of when the data was captured — so figures from a scrape
that failed three hours ago looked exactly like figures from thirty seconds ago. Both pages now carry an
"Updated …" line, and once the data is more than half an hour old it turns amber and tells you to re-sync.

**The account status dot now works in dark mode.** The little coloured dot beside each account in the
sidebar was still using fixed colours chosen for the light theme, so it was the one status signal that
never got the readability fix the rest of the app received. It now uses the proper colour for whichever
theme you are in, and both have been measured against the accessibility standard for non-text indicators.

**Cards, chips and panels now share one set of corner shapes.** Six slightly different roundings had
accumulated — cards at four different radii — which reads as untidy without being obviously wrong. There
are now three: small controls, panels, and cards.

## v4.99.32

Three additions aimed at the actual daily job rather than the numbers describing it.

**Work the queue from the keyboard.** `J` and `K` move (the arrow keys work too), `Enter` opens, `D` marks
done, `S` snoozes, `C` calls back, `R` copies a reply, and `?` shows the list. Sixty conversations at four
clicks each is a morning; the same queue with one hand on the keyboard is minutes.

Nothing fires while you are typing in the search box, and nothing fires when you are holding Ctrl or Alt,
so the shortcuts never eat a Windows or browser command. The selection stops at the ends of the list
rather than looping around, and after you clear a conversation it stays where you were instead of jumping
back to the top.

**Saved replies.** A library of ready answers, filled in with the customer's name and branch and copied to
the clipboard for you to paste and send. Nine are there from the moment you install — price list, opening
times, address, booking confirmation, an apology, a missed-call note, a reply for photos, a jobs answer,
and a holding reply — and every one is yours to edit.

If a customer is not in your contacts, WhatsApp gives their phone number instead of a name; a reply then
opens with "Hi there" rather than "Hi +923105325598".

**The app still never sends anything.** A saved reply goes to your clipboard. You paste it, read it, and
press send yourself.

**Missed calls have their own list.** 81 customers had rung and got nothing back, and until now they sat
in the queue as messages nobody could read, because a call has no text. They are now a filter of their
own, with a **Call back** button on each row that dials through your phone app. Only rows where a real
number is known get the button.

**The reply queue filters by what a row actually is.** At risk · Missed calls · Enquiries · Bookings ·
Photos & voice notes · Job & training · Business outreach. Each chip shows how many it holds before you
click it, and it combines with the branch and waiting-time filters.

## v4.99.31

**Downloads work.** Receiving a file and choosing to save it did nothing at all — no file, no error, nothing
you would ever see. WhatsApp decrypts a received file in the page and then opens it, and the app's own
navigation guard was discarding that as if it were an unwanted pop-up. Downloads now go through the
browser's normal save flow.

**Links open in your default browser.** A link a customer sent you did nothing when clicked. Links that
belong to the site you are monitoring still open in place; anything else — an article, a map pin, an
Instagram post — now opens in your own browser, where it belongs. Email addresses and phone numbers work
the same way.

Only links you actually click are opened. Nothing a page does on its own can open a browser window, and
the app only ever hands over ordinary web links, mail addresses and phone numbers — never anything that
could run a program on your PC.

**The startup error is gone.** Opening the same account twice in quick succession could make the app build
two copies of it, and the second one failed. Beyond the logged error it wasted around a gigabyte of memory
before failing, and it left the account showing "Connection error" for something that had never actually
failed to connect. Opening an account that is already opening now simply waits for it.

**Two windows can no longer collide.** If a second prompt appeared while one was already on screen, the app
threw an error, and in some cases one of the two messages was lost entirely. Prompts now wait their turn
rather than competing, so a notice you need to see is delayed at worst, never dropped.

## v4.99.30

**A quarter of the "customers waiting" were never messages.** Reading WhatsApp's own message type showed
what the queue was really made of. Of 471 conversations the app was reporting as customers waiting:

- **117 were system notices** — "your security code changed", protocol and template notices. Nobody wrote
  them and nobody is waiting on an answer. They are no longer counted.
- **81 were missed calls** appearing as messages with no readable text, so the row told you nothing. A
  customer who called and did not get through is still worth returning, so these are still counted — they
  now say they are calls.

Between them, that took the "message could not be read" count from 33 down to 1. Most of it was never
unreadable; it was mislabelled.

**Conversations whose message no longer exists are no longer counted.** If a customer deleted their
message, or it expired under disappearing messages, there is nothing left to reply to. The app now tells
that apart from a message it simply has not loaded yet — and it never guesses: an uncaptioned photo still
counts, because a photo is very often "can you do this?".

**Filter the reply queue.** Three new filter rows above the Needs-reply list:

- **How long they have waited** — Today, the last 7 days, backlog, or everything. It starts on the same
  window the headline figure uses, so the list and the number above it agree.
- **Which branch** — you could group by branch before, but not filter to one without typing its name.
- **What the customer wants** — At risk, Enquiries, Bookings, Job & training, Business outreach. Every
  chip shows how many conversations it holds before you click it, and chips with nothing in them are not
  shown at all.

"At risk" is the one to watch: complaints, price objections, and customers saying they will go elsewhere.
On real data it finds exactly those and nothing else.

**The metric tiles work from the keyboard now.** They were clickable but not focusable, so the whole
drill-down into the reply queue was mouse-only and invisible to screen readers. They are proper buttons
with full spoken labels ("Backlog: 184. 61 need a reply now. Press to see details."), and tiles with no
drill-down no longer collect a pointless tab stop.

**The dashboard stops saying the same number twice.** The headline figure was repeated in a tile
immediately below it at a smaller size, which read as two different facts. That tile now shows the
backlog instead — the half the headline cannot show. Same for "% caught up", which appeared in both the
hero line and its own tile.

**One tile is allowed to be louder than the others.** All six were drawn at identical size and weight, so
"Busiest window · 7PM" looked exactly as important as the number of customers waiting.

**"By account / By location / Needs reply" was really two different controls.** The first two change how
the view is grouped; the third replaces the view. They are now separate, and the grouping control hides
itself when it does not apply instead of sitting there doing nothing.

**The date box at the top now says "Metrics".** It only ever scoped the metric tiles, but unlabelled next
to Re-sync it looked like it filtered the whole page.

**Per-account cards say "open" rather than "waiting".** The headline counts this week; a card counts that
account's whole history. Two different scopes should not share the same word.

**Shared contacts, and the Report button.** A shared contact card no longer displays a string of internal
digits. The Report button has left the toolbar — the sidebar already has a Reports section, and the
reminder banner offers the report when it is actually worth opening.

## v4.99.29

**"460 customers waiting" is gone, and what replaced it is a number you can act on.** The count was a
pure direction flag: the customer sent the last message and nobody typed since. Measured on real salon
data that read **466 waiting, oldest 82 days** — but only 41 of them had actually asked anything, 82 were
the word "ok" or a relative of it, and 454 had already been read. It was measuring "nobody typed back",
not "somebody is waiting".

The real cost was not the size of the number, it was what it hid. Sitting unanswered in that list: a
customer reporting bruising after a waxing appointment, and another saying *"warna main kahin aur chali
jawon"* — otherwise I'll go elsewhere.

The same data now reads **76 needing a reply**, with 280 older conversations shown separately as backlog
and 104 not counted because the customer's last message only closed the conversation.

- **Conversation closers stop being counted.** "ok", "thanks", "ji", "g", "oky", "ok jazakallah",
  "walaikum us salam", a thumbs-up. English, Roman Urdu and Urdu script in one list, because customers
  mix all three in a single message.
- **Anything that asks something is always kept**, and that check overrides every closing rule — "ok but
  what time" is a question, not an "ok". Anything the app does not recognise, cannot read, or that is
  merely short stays in the count. It only ever removes a conversation on positive evidence that it is
  finished.
- **A greeting nobody answered now counts.** A bare "Hi" or "Salam" is a customer trying to start a
  conversation, not one ending it.
- **The backlog is separated, not hidden.** Conversations older than a week get their own number, so an
  82-day-old complaint stays visible instead of being buried among a week's worth of new messages.
- **Nothing is deleted.** Everything excluded is listed with the reason it was excluded, and the whole
  filter can be switched off in Settings to see the raw count again.
- **When the app cannot read a message, it says so** rather than quietly assuming the conversation is
  finished — a scrape that failed to load message text can no longer look like a quiet morning.
- **Local AI decides only the genuinely ambiguous ones**, on your own machine. With it off, those
  conversations simply stay in the count.

**Shared contacts no longer show as a string of digits.** 22 conversations displayed an internal
identifier like `102074813546715@lid` where the message should be. They now read "Shared a contact", and
still count as needing a reply.

## v4.99.28

**When the internet is down, the app now says so instead of telling you to open an account.** A
conversation scan that could not run reported *"this account's page is not loaded — open the account
once to finish loading."* That advice is right for an account that simply has not been opened yet, and
it is what the message was written for. But a dropped connection produces exactly the same symptom: the
page cannot load, so the scan's watchdog fires and the app fell back to the same sentence. Opening the
account cannot possibly help while the network is down, so the app was sending its owner to do something
futile and implying they had forgotten something.

It now names the real cause — *"there is no internet connection, so this account's page never loaded"* —
and says what happens next. A genuine scraper failure is still reported as one, so an offline machine
never becomes a blanket excuse that hides a real break.

**The sidebar keeps saying why an account is down, and stops promising a reconnect it has given up on.**
Two related mistakes, both found by running the app behind a dead network rather than by reading the
code. First, an account correctly reading "No internet — reconnecting…" reverted to a bare "Connection
error" the moment the first automatic retry fired — reloading cancels the in-flight page load, and the
cancellation reports a different, unrecognised error, so the app lost track of what it already knew.
Second, once the retry schedule ran out after about eight minutes, the row went on saying "reconnecting…"
for something it had stopped doing. Those accounts now read "No internet — tap to retry", which is both
true and something you can act on.

## v4.99.27

**Accounts in the sidebar now tell screen-reader users they can be opened.** Pressing Enter or Space on
an account has always worked, but the row announced only its name and status — nothing to suggest it did
anything at all. The location headings directly above them already said "press to collapse or expand";
the accounts underneath said nothing. They now end with "press to open", after the name, status and
unread count so the useful part is heard first.

Found by walking the keyboard focus order through the whole dashboard, which otherwise came back clean:
51 stops, every one of them named, no dead ends, and an order that follows the screen.

## v4.99.26

**The green, amber and red used for status are now readable in both themes.** They were one set of
colours shared between the light and dark themes, and each of them failed in one of them. In light mode
the green "on time" figure measured 2.28:1 against the card behind it and the amber 2.15:1 — well under
the 4.5:1 minimum for readable text, and worse than the brand-colour problem fixed earlier. In dark mode
it was the red that failed, at 2.84:1.

Each theme now has its own set, measured against the surface it is actually drawn on: 5.02, 5.02 and 4.83
in light; 6.03, 6.39 and 4.96 in dark. One shared set could never have worked — the contrast a colour
needs to be readable on white and the contrast it needs on near-black are mutually exclusive.

**Status is never signalled by colour alone.** Green and red are almost identical in brightness, so in
greyscale — or to the many people with red/green colour blindness — they are the same colour. Every place
the app uses status colour also says it in words or shows a symbol: the account rows spell out "Signed
out", "No internet — reconnecting…" or "Connection error", the trend badges carry an up or down arrow,
and the waiting pill reads "N awaiting" or "caught up". That was already true throughout; it is now
checked automatically so it stays true.

## v4.99.25

**"You're all caught up" now means it.** The dashboard decided you were caught up purely from the number
of customers waiting being zero — and an account the app cannot read reports zero, because there is
nothing to count. So if one branch's WhatsApp failed to load while the others happened to be quiet, you
got a green tick and "You're all caught up" while that branch was not being watched at all. The card just
below it said "couldn't read"; the headline above it never looked.

It now says what it actually knows: "Nothing waiting — but not everything was counted", naming how many
accounts could not be read, and distinguishing those from accounts that simply have not loaded yet. An
account that is merely out of date does not block the message, because that happens routinely and would
make the reassurance useless.

**Caught up on a date range no longer sounds like caught up on everything.** With "Today" selected, a
customer who wrote last week and is still unanswered is deliberately left out of today's count — that part
is intended. But the summary line said "No customers are waiting on a reply", which was not true of them.
It now reads "Caught up on this range" and tells you how many older conversations are still open.

The start-of-shift briefing was making the same claim, so it got the same fix — including what the
on-device AI is told, since it would otherwise write the same false reassurance more fluently.

## v4.99.24

**If your settings ever get reset, the app now tells you.** When Unified Messenger cannot read your saved
settings at startup — a file damaged by a crash, a backup tool, or antivirus holding it open — it falls
back to defaults and carries on. That much already worked, and your old file was already being kept
rather than deleted. What was missing is that nobody told you: your reply-time targets, business hours,
notification choices and AI setting would quietly revert, and the only record was in a log file you have
no reason to read.

You now get a notice at startup explaining what happened, confirming that your accounts and message
history are untouched, pointing out that automatic updates are worth re-checking, and giving you the
exact location of the copy of your previous file — with a button to open the folder it is in.

It does not claim to list which settings changed, because the file could not be read and there is
genuinely no way to know. It does not offer to restore automatically, for the same reason.

**Startup prompts no longer collide.** The setup wizard and the "pin to taskbar" prompt were both started
at the same moment, and Windows allows only one dialog at a time — so on a first run where both were due,
one of them silently never appeared, and the setup wizard was marked as completed anyway. They now run one
after another.

## v4.99.23

**The icon picker can now be used without looking at it.** Choosing an account's icon offered twenty-five
buttons — every social logo, every general icon, and the two import options — that a screen reader
announced simply as "button". There was no way to tell WhatsApp from a shopping cart except by sight.
Each one now says what it is.

**Waiting customers in the account drill-down are named too.** Opening an account's details lists who is
waiting; the button that opens each conversation announced as "button" while the "Mark as done" control
right next to it read out properly. It now reads "Open chat with <customer> in <account>", matching what
the main dashboard already said.

Both were found by opening the dialogs and reading them the way a screen reader does.

## v4.99.22

**Automatic updates now actually work.** They never have. The app checked GitHub at every launch,
downloaded the whole installer when a newer version existed, then refused to install it — and told
nobody. The check it ran required the installer to carry a code signature that nothing in the build
process has ever added, so it rejected every update it was ever offered, deleted the download, and threw
the failure away where no log or message could show it. If you have been wondering why the app never
updated itself, that is why. It now verifies the download against the published checksum instead, which
is a real integrity check on the file you receive.

> **One-time manual install required.** The faulty check lives in the copy of the app you are already
> running, so it will reject this release too — it cannot fix itself remotely. Install this version once
> by hand from the Releases page. After that, updates work on their own.

**Accounts recover on their own after the internet drops.** If a page failed to load — a router
rebooting, a wifi handover, a hotel network — the account simply stayed broken until you noticed and
refreshed it by hand. There was a self-healing mechanism, but it only watched accounts that had already
loaded successfully once, so it could never reach this case. The app now retries a failed account by
itself, five times over about eight minutes, backing off as it goes, and stops trying for problems that
retrying cannot fix.

**Plainer language when the network is the problem.** A dropped connection used to surface as
`HostNameNotResolved` beside an account, and clicking "Check for updates" with no internet produced
`No such host is known. (api.github.com:443)`. Those now say what actually happened. The "no releases
found" message also stopped handing you the developer's to-do list — it previously asked you to publish a
GitHub release and check an environment variable.

**A stalled update download can no longer hang.** If the connection died partway through, the download
would wait on it indefinitely and leave a half-written file behind. It is now bounded, and the partial
file is cleaned up.

Nothing about this release changes what leaves your machine, which remains nothing.

## v4.99.21

**Your daily numbers are now correct on the two days a year the clocks change.** Every date filter in the
app — Today, Last 7 days, the custom date pickers, "this year" — worked out when a day starts by taking
midnight and attaching the time-zone offset that applied *at the moment you happened to be looking*. On an
ordinary day those are the same thing. On the day the clocks go forward, "Today" secretly began an hour
before midnight and swept in the last hour of the previous evening; on the day they go back, the first
hour of the day was left out of today's figures entirely. Since those windows decide the caught-up
percentage, the awaiting counts, SLA met % and everything on the account cards, the numbers were being
measured over a set that quietly disagreed with the day they were labelled with — for one day, before
correcting itself.

Days are now worked out from the time zone properly, including the places where the clocks change at
midnight itself and local midnight either never happens or happens twice.

Nothing changes if you are somewhere that does not observe daylight saving.

Also in this release: the 7-day activity sparkline, the response-time day charts and the analytics
day/hour buckets were all checked across 23- and 25-hour days and found correct — that is now covered by
tests so it stays that way. The end-of-day projection reads up to 2% off on a transition day, which is
recorded and deliberately left alone; it is a forecast, and correcting it would cost more than it is
worth.

## v4.99.20

**Screen readers now announce the account cards themselves.** Each account on the command centre is a
card you can expand to see who is waiting — and that card, the main thing you move to and activate on the
product's busiest screen, announced only "button". It now reads, for example, "Depilex F-11 WhatsApp: 140
customers waiting. Expand to see who is waiting."

The review shortcuts got the same treatment: the "N to reply" button on each Google account now names the
location it belongs to, and each pending review names its reviewer.

## v4.99.19

**An account that simply hasn't opened yet is no longer reported as broken.** To save memory, accounts you
haven't looked at are not loaded in the background. Reading such an account can't work — there is no loaded
page to read — and after v4.99.15 that was being shown as "can't read this account — click Re-sync". Two
things wrong with that: nothing is broken, and Re-sync can't load a page that was deliberately left
unloaded. Those accounts now stay marked as syncing until you open them once, which is what actually
resolves it.

Genuine read failures — where the page *was* available and the read still failed — are unchanged and still
reported.

**Internal:** the fix in v4.99.17 for tests writing fake errors into your log file was incomplete; it
covered one code path and missed several others. It is now handled centrally, and verified: running the
affected test suites adds nothing to the log.

## v4.99.18

**Google Business accounts are no longer treated as if they were broken.** Google is a reviews and Q&A
channel — it has no customer conversations to read, and never will. The app was nonetheless trying to read
conversations from every Google account on every cycle, failing (correctly), and filling the log with
warnings about it. Following v4.99.15, it had also started marking those accounts "can't read this
account — click Re-sync" on the dashboard: three healthy accounts flagged as faulty.

Channels that have no conversation data are now skipped for that read entirely, rather than attempted and
reported as failures. Re-sync also stops telling you a Google account is "still loading", which sent you
to open a tab that could not have changed anything; it now says plainly that the channel has no
conversation metrics.

The practical benefit beyond the false alarm: your log stops filling with routine warnings, so a real one
is visible when it appears.

## v4.99.17

**Internal:** running the test suite no longer writes fake error entries into your log file. Tests that
deliberately simulate a failed save were recording those simulated failures in the same `app.log` the app
uses for real problems, so the log could contain errors that never actually happened.

No change to how the app behaves.

## v4.99.16

**The rest of the app now names its controls for screen readers.** Following v4.99.15, the remaining
pages were checked: the Analytics export button and its date range, the activity-pattern filters, the
reporting-period selector, the command-centre search box, and the dismiss buttons on the weekly-report
reminder and on every notification all announced only "button". They now say what they do.

Two of these were easy to miss: a search box's placeholder text is not read out as its name, and the
notification dismiss button is defined once but appears on every notification — so a single omission was
silent on every row in the list.

## v4.99.15

**Screen readers now name the buttons.** Nine controls on the command centre announced only "button" —
including **Re-sync**, which is the action the app itself tells you to take when something looks wrong.
Each account's "N awaiting" pill also announced the same phrase with no way to tell which branch it
belonged to; it now reads, for example, "Depilex F-11 WhatsApp: 137 customers waiting."

**The chart and icons are readable in dark mode.** The brand blue was the same shade in both themes and,
against a dark card, sat below the minimum contrast for something you are meant to be able to see — which
included the message-volume chart line. It is now lighter in dark mode. The same change improves the
Re-sync button, whose text was also marginally under the threshold on dark.

Light mode is unchanged.

## v4.99.14

**Updating no longer deletes your log file or your settings-recovery file.** Every update was clearing
`app.log` — the only record of anything going wrong — and any `settings.json.corrupt-….bak` left behind
when a data file could not be read. Since updates install themselves by default, that happened without you
choosing it, and it meant updating to fix a problem destroyed the evidence of the problem. The v4.99.4
notes tell you to look for that `.bak` file to get your settings back; it will now still be there.

**Uninstalling can now erase your data if you want it to.** Uninstalling removed the program but left
everything else — on the machine this was tested on, 7.2 GB, including your message history and signed-in
accounts. That remains the default, because it means reinstalling picks up exactly where you left off. But
it is now an explicit choice on the uninstall screen rather than something that just happens quietly.

**A first launch no longer says "Welcome back".** On a fresh install the dashboard greeted you as a
returning user and reported "1 personal account connected" — while telling you, further down the same
screen, that no accounts were connected. It now welcomes you properly and invites you to add your first
account.

## v4.99.13

**The review reply rate no longer reads "100% replied" above reviews that still need a reply.** With 996
of 1000 reviews answered, the panel rounded to 100% while listing the four outstanding ones directly
beneath. It also reported 0% when a small number had been answered. As everywhere else in the app, 100%
now means none outstanding and 0% means none answered.

A business with no reviews at all still reads 0%, not 100% — there is nothing to have replied to.

## v4.99.12

**The weekly report no longer contradicts itself.** Two percentages in the report were rounded in a way
that could produce nonsense in a single sentence — "996 messages this week — 100% of all customer volume"
while naming that account the busiest of several, and "100% of the 1000 customers who messaged this week
had contacted you before; 3 reached out for the first time." Both now follow the same rule as the rest of
the app: 100% means all, 0% means none.

The week-on-week volume change is untouched and can still exceed 100% — if your messages tripled, "up
200%" is the right answer.

**Your branch names keep their capitalisation.** The report's opening line lower-cased everything it
quoted, so an account called "Depilex DHA-2 WhatsApp" appeared as "depilex dha-2 whatsapp" in the one
sentence most likely to be forwarded to a manager.

## v4.99.11

**WhatsApp's own account no longer sits in your list of waiting customers.** WhatsApp sends occasional
one-way notices from an official account you cannot reply to. That account was being counted as a customer
waiting for an answer — and since replying is impossible, it never went away. On the machine this was found
on it had been "waiting" for 26 days. On a busy branch that is one extra; on a branch that is otherwise
caught up, it is the entire number, and you would go looking for a customer who does not exist.

**Photo messages show as "Photo" instead of a wall of characters.** When a customer's last message was an
image, the preview in your needs-reply list showed the image's raw encoded data —
`/9j/4AAQSkZJRgABAQAAAQABAAD…` — rather than anything readable. Around 3% of previews were affected. They
now read "Photo". Ordinary text is untouched, including messages that happen to start with a slash or the
word "data".

Both corrections also apply to data already saved on your machine, so they take effect as soon as you
update rather than waiting for the next sync.

## v4.99.10

**"SLA met" no longer reads 100% when replies missed the target.** The same rounding problem fixed for the
caught-up percentage in v4.99.9 was also present in your SLA figure — 499 replies inside your target and
one outside is 99.8%, which displayed as **100%**, with the breach still counted in the reply total right
beside it. It also reported 0% when a small number of replies had met the target.

As with caught-up, 100% now means no breaches and 0% means none met, with everything else shown as 1–99%.
The daily SLA trend follows the same rule. All four places in the app that turn a count into a percentage
now share one piece of code, so this cannot drift apart again.

Nothing else in the response-time figures changed: the median, the 90th-percentile, "answered today" and
the week-on-week arrows were all checked and were already correct.

## v4.99.9

**"100% caught up" now means nobody is waiting.** The caught-up percentage was rounded, so an account with
996 of 1000 chats handled — 99.6% — displayed as **100%**, complete with a green tick, directly beside
"4 awaiting" on the same card. Two numbers on one card disagreed, and the reassuring one was the wrong
one. The same rounding also reported 0% for an account that had handled a small number of chats.

100% is now reserved for genuinely nothing outstanding, and 0% for genuinely nothing handled; everything
in between shows 1–99%. The figure can now be up to a point pessimistic, but it will never tell you that
you are finished when you are not. The whole-business percentage follows the same rule: it only reaches
100% when every account has.

**The trend sparkline no longer disagrees with the Analytics chart.** The small 7-day chart on each
account card grouped messages by UTC date, while every other daily figure in the app groups by your local
date. If you are five hours ahead of UTC, everything that arrived between midnight and 5am was counted
against the previous day — so today's bar read low every morning, and the card and the Analytics page
showed different histories for the same account.

## v4.99.8

**An account the app can't read now says so, instead of looking quiet.** Until now, a branch with nothing
to report and a branch the app had stopped being able to read looked exactly the same on the command
centre — both showed "no activity". Those need opposite responses from you: one is good news, the other
means you have lost sight of that branch and customers may be waiting without you knowing.

Cards for an unreadable account now say "can't read this account — click Re-sync", in the same colour used
for anything else needing attention, with the recovery steps in the tooltip. Screen readers announce which
account it refers to. A location made up of several accounts flags if *any* one of them can't be read,
because its totals are then incomplete.

The warning only ever appears after a read has genuinely been attempted and failed — it is never guessed
at from an account simply having no messages, and it clears itself as soon as a read succeeds. An account
that hasn't been read yet, such as just after launch, is not flagged.

## v4.99.7

**One odd conversation can no longer wipe out an account's whole reading.** When the app read your chat
list, a single conversation coming back in an unexpected shape — a date arriving as a number instead of
text, say — threw away *every* conversation in that read, not just the odd one. The account then showed as
having no activity, which for a branch with customers waiting is the worst possible thing to get wrong
quietly. The scraper side already skipped bad conversations one at a time; the app side now does the same,
and notes in the log how many were skipped.

**A change in WhatsApp's data no longer quietly changes what "waiting for a reply" means.** If the
information the app relies on to tell whether you have replied goes missing, it falls back to using unread
badges instead. That fallback is deliberate — it keeps a number on screen rather than showing nothing —
but unread is a weaker signal, because opening a chat on your phone clears it even though nobody replied.
That substitution used to happen invisibly; it is now recorded in the log so it can be spotted and fixed.

## v4.99.6

**When an account can't be read, that now leaves a trace.** Reading an account has two routes: a fast one,
and a fallback used when the fast one fails. The fallback recorded nothing at all — no log entry, no
status — so an account that had stopped reporting entirely produced no evidence anywhere. The fast route
had always recorded its failures; the fallback now does too, including the case where a scan finishes but
finds no conversations, which is what a change on WhatsApp's side looks like.

**Known limitation, worth being clear about:** on screen, an account the app cannot read still looks the
same as an account that is simply quiet — both show "no activity". The numbers themselves are not
affected: an unreadable account is left out of your caught-up percentage rather than counted as perfect,
so it cannot flatter your figures. But if a branch shows "no activity" and you expected traffic, check the
log or click Re-sync. Telling these apart on screen is still to come.

## v4.99.5

**A damaged data file no longer disappears without trace.** The same weakness fixed for the settings file
in v4.99.4 also affected the file holding your marked-handled and snoozed chats, and the one holding your
KPI trend history. If either became unreadable, the app quietly started over with an empty one — nothing
in the log, and the damaged file was not kept, so there was nothing left to recover from. For the
marked-handled file that means every chat you had closed out reappears as waiting for a reply.

Both files are now kept aside as `.corrupt-….bak` next to your data, and the problem is written to the
log. All three data files now behave the same way, through shared code, so they cannot drift apart again.

## v4.99.4

**If your settings file can't be read, that now gets recorded.** When the settings file is damaged — a
half-finished write, a backup tool holding it open — the app falls back to factory defaults and carries
on. That part was working, and the unreadable file was already kept aside as a `.corrupt-….bak` so
nothing was destroyed. What was missing is that the message saying so was written in a way that gets
stripped out of the released build, so it appeared nowhere at all. Testing this on a real settings file
showed local AI switching itself off, "ask before updating" turning itself off, the account memory limit
reverting to unlimited, and one location losing its response-time target and opening hours — all
silently. The reset is now written to the log file, and a file that is locked rather than damaged no
longer stops the app from starting.

**Known limitation:** the app still does not *tell you on screen* when this has happened. If your
settings ever look like they have reset themselves, check the log and look for a
`settings.json.corrupt-….bak` file next to your settings — your previous values are in it. An on-screen
notice is still to come.

## v4.99.3

**An account that has stopped reporting now says so in plain English.** The warning on an out-of-date
account card read "stale — right-click the account → Refresh WebView, then Re-sync". "WebView" is an
internal term, and the line was long enough to be cut off mid-instruction inside the card. It now reads
"out of date — click Re-sync", with the full recovery steps in the tooltip. Screen readers now announce
which account the warning belongs to, which they previously did not.

Third-party notices now list every dependency the app ships, and the developer documentation was
corrected where it disagreed with the code.

## v4.99.2

**The command centre no longer blames the wrong branch for the oldest waiting customer.** The summary
line under the headline showed the longest wait next to an account name — but the name it showed was
whichever account had the *most* customers waiting, not the account the longest wait actually belonged
to. Because the two sit side by side, it read as one statement. On real data it claimed a 75-day-old
customer was at a branch whose own card said its longest wait was 50 days.

The longest wait is now labelled with the account it belongs to, the busiest account is labelled
separately as "furthest behind", and both are measured over the same time window the account cards use —
previously the headline and the cards measured over different windows and could disagree even about the
same account.

**Adding an account now tells you what that channel actually does.** The platform list showed six
channels as if they were equivalent, when only WhatsApp and WhatsApp Business produce oversight metrics
and Google Business produces review metrics. Picking Messenger or Discord created an account that worked
as a browsing tab but never appeared on the dashboard, with nothing explaining why. Each channel now
describes what it delivers before you add it, and channels that are not measured say so plainly. The
Google entry also stopped describing its review scraping as "planned" — that shipped some time ago.

## v4.99.1

**Closing the app no longer loses your work when one file can't be saved.** On shutdown the app saves
seven things: analytics, triage, chat snapshots, response times, contact history, your marked-handled and
snoozed chats, and KPI trends. They were saved as a single all-or-nothing step, so if the very first one
failed — a file locked by a backup tool, a full disk, a permissions problem — every remaining one was
skipped and the app still closed as if nothing had happened.

The visible effect was chats you had already marked handled coming back the next morning, snoozes
expiring early, and gaps in response-time history and trend charts. Because which items survived depended
on which file failed first, it looked like the app was just being flaky rather than a specific bug.

Each item is now saved independently, so one failure can no longer discard the others, and the names of
anything that failed are recorded so the app can tell you your data may be out of date.

## v4.99.0

**Custom URL accounts now load.** A Custom URL account opened to a blank page and stayed there. The
navigation guard kept its per-account list of allowed sites in a lookup that could silently lose its
entry, after which it fell back to the built-in platform list — which contains WhatsApp, Google,
Telegram, Messenger, Discord and Instagram, so every built-in account carried on working and only Custom
URL accounts broke. Each account now carries its own list directly, and the guard also stops logging a
scary "blocked navigation" warning for the blank page every WebView starts on.

**"Mark as done" is now a real button on every waiting customer.** Some customers send the last message
and simply don't need an answer, and there was no visible way to close those — the action existed, but
only behind an unlabelled "…" on the dashboard, and not at all in the per-account view. Every waiting row
now has a **Done** button, with snooze on its dropdown. As before, done isn't permanent: the conversation
comes straight back if that customer sends something new, and snoozes expire on their own.

**The dashboard stopped repeating the other pages.** The overview charts, the activity graph and the
review-health panel each have their own section now, so the dashboard's copies were a second thing to
maintain and a second thing to disagree with the first. The dashboard is what needs attention right now;
underneath it, three cards link to Analytics, Reviews and Reports, each carrying the one number that says
whether it's worth opening.

**The account leaderboard shows a number that means something.** It ranked accounts on a blended score
that subtracted a backlog penalty from the on-time rate — which pushed every account to 0% whenever
on-time was genuinely low, so the whole list read "0%". It now ranks and shows the real **on-time reply
rate**, numbered 1–5, with the backlog beside it instead of buried inside it. It has moved to Analytics,
alongside a new **Messages by Account** ring.

## v4.98.0

**The dashboard gains the overview row from the new design.**

Below your command center there's now:

- **Overview** — message volume across the last 7 days, with the busiest day highlighted.
- **Top Performing Accounts** — your accounts ranked by a score combining how often they reply on time
  with how big their current backlog is. Accounts the app hasn't measured enough replies for are left
  out rather than shown at a flattering 100%.
- **Message distribution by account** — a ring showing which accounts your messages actually come from.

**Response time and SLA met now show how they've changed** versus the previous period, alongside the
existing Messages/day change. A falling response time shows green because that's an improvement — the
arrow direction alone doesn't decide the colour.

Everything already on the dashboard is untouched: the waiting-customers headline, account cards,
needs-reply list, mark-handled and snooze, AI notes, and Re-sync all work exactly as before.

## v4.97.0

**The Analytics page, redesigned.**

Analytics is now a real dashboard instead of a single chart:

- **Four KPI cards** — Messages, Response Time, Replies (15m) and SLA Met — each with its change versus
  the previous period. A drop in response time shows green; a drop in raw message volume shows neutral,
  not alarming red.
- **Messages Over Time** — a bar chart with the busiest day highlighted and labelled.
- **Average Response Time** and **Replies Within 15 Minutes** — trend charts with a shaded fill.
- **SLA Performance** — a ring showing met vs missed, with anything the app genuinely can't time counted
  separately as "no SLA" rather than being quietly scored as a pass or a fail.
- A **period picker** (this week / 30 days / 90 days) and **Export** to CSV.

The existing activity patterns section (hour of day, day of week, month, heat map) is unchanged and still
sits below, so nothing you were using has gone away.

## v4.96.0

**Groundwork for the redesigned dashboard — the charts and the numbers behind them.**

The first step toward the new design: the reusable chart building blocks and, more importantly, the data
shaping they need. No screen looks different yet; this is the foundation the new Dashboard, Analytics and
Reports pages are built on next.

- **Delta badges that mean the right thing.** A "12% vs last week" chip now colours by whether the change
  is *good for that metric*, not by its arrow — response time falling shows green, raw message volume
  falling shows neutral (not alarming red).
- **Honest chart data.** New calculations that were missing: percentages that add up to exactly 100, a
  three-way SLA split that counts un-measurable channels as "no SLA" rather than faking a pass or fail, a
  "top performing accounts" ranking that won't crown an account we haven't synced yet, and per-day
  "replies within 15 minutes" trends.
- **A clearer type scale** so cards read label → value → headline instead of jumping straight from small
  text to one big number.

Everything here is covered by unit tests. The visible redesign lands in the following updates.

## v4.95.0

**A real left navigation: Analytics, Reviews and Reports are now their own sections.**

The sidebar used to list only your accounts. It now has navigable sections above them:

- **Analytics** — message volume and activity patterns (hour of day, day of week, month, heat map,
  week-over-week), previously reachable only by scrolling the dashboard.
- **Reviews** — Google Business review health on its own page, with a proper empty state when no Google
  account is connected.
- **Reports** — the business report as a browsable page rather than a dialog, with the period picker and
  Markdown/CSV export. It renders from the same builder as the dialog, so the two can't disagree.

The app also **reopens on whichever section you were last using** instead of always starting on the
dashboard, and all three sections are reachable from the command palette (`Ctrl+K`).

Your accounts, the scope switch, location groups, right-click menus, badges, and the notification and
settings buttons are all exactly where they were.

*Under the hood: "which screen is showing" used to be three separate true/false flags copied across four
files, including one left over from a feature that had been deleted. It's now a single value, so future
sections are a small change rather than a risky one.*

## v4.94.0

**Merges the API-modernization stream (v4.88–v4.93) with the Phase-5 channel-capability work.**

This is the first release since v4.87.0, so it carries everything from six intermediate versions:

- **Fast WhatsApp reader** — reads WhatsApp Web's live in-memory copy of your chats instead of its
  encrypted saved copy. Message previews for **every** waiting customer (82–88% of all chats, verified
  live) rather than only the ~60 drawn on screen, and a reply you send from your phone clears the
  "waiting" flag without a manual Re-sync. Falls back automatically if WhatsApp changes internally.
- **Faster refresh and honest status** — waiting counts refresh roughly every 25 seconds instead of 90.
  Accounts that aren't healthy show a status chip (*Starting*, *Stale*, *Scan QR*, *Failed*); healthy
  accounts show none, so a badge always means something.
- **Custom URL tabs are a real browser** — editable address bar, and **Save site** to keep the page
  you're on as its own account. Non-web addresses are refused, and typing something that isn't a URL is
  never forwarded to a search engine.
- **Per-channel capabilities** — each channel now declares what it can honestly contribute to oversight,
  so a platform is only measured once it ships the adapter backing that claim. Google is modelled as a
  *reviews* channel (Google Business Messages shut down in 2024), and Meta channels are constrained to
  aggregate-only reads because opening a thread there sends the customer a read receipt.
- **Housekeeping** — ~850 lines of orphaned code removed after a full reference audit; the README is now
  a product README and this changelog holds the version history.

## v4.93.0


**The fast WhatsApp reader now works. It didn't before.**

v4.88.0 introduced a reader that pulls from WhatsApp's live in-memory copy of your chats. Testing it
against real logged-in accounts showed it was **finding the wrong thing** — it located a small
look-alike list of 1–8 items instead of your actual chat list, produced nothing usable, and quietly fell
back to the old reader every single time. The safety net worked exactly as designed (your numbers were
never wrong), but none of the promised benefit was reaching you.

Two causes, both now fixed:

- It was reading WhatsApp's module *index* rather than loading the modules themselves, so it never saw
  the real data.
- It gave up scanning after 12,000 modules; WhatsApp Web ships about 16,400.

Measured on live accounts after the fix:

| | Before | After |
|---|---|---|
| Chats read per account | 0 (fell back) | ~850 |
| Phone numbers resolved | — | ~100% |
| Message previews | ~60 chats (old reader) | 82–88% of all chats |

Preview text fills in over the first minute while an account finishes syncing — names, phone numbers and
waiting-counts are correct immediately.

Also removed a connection-status feature from the reader that never worked and that nothing used.

## v4.92.0


**Dead-code removal. No behaviour changes.**

A reference audit found roughly 850 lines reachable from nothing: a second, divergent copy of the
chat-navigation flow (the live one was elsewhere), a chart helper whose chart had been removed, an
executive-insights builder with no caller, and several helpers kept alive only by their own tests. All of
it is gone, along with four now-pointless test files and two documentation pages that credited deleted
code for features something else provides.

Verified end-to-end: both projects build with zero warnings, 105 targeted tests pass, and the packaged
installer was installed and launched successfully over an existing multi-account profile.

## v4.91.0


**Silent failures are now visible.**

Two blind spots closed:

- **Session status chips on account cards.** An account could previously show "connected" while its
  numbers had quietly stopped refreshing. Accounts that aren't healthy now carry a chip — *Starting*,
  *Stale*, *Scan QR*, or *Failed* — with a tooltip saying what to do about it. Healthy accounts show
  **no** chip, deliberately: a badge on every card is wallpaper, a badge on one card is a signal. A
  location card shows its **worst** account, so a signed-out branch account can't hide behind its
  healthy siblings.
- **Settings → Data now names the live WhatsApp reader.** The fast reader (v4.88.0) falls back to the
  older one on its own, by design — losing previews is better than losing metrics. But a silent fallback
  is an invisible one, so Settings now reports whether it's active, on how many accounts, and when it
  last read successfully. There's also a switch to force the old reader if you're diagnosing something.

## v4.90.0


**Browse and save any site.**

A Custom URL tab already had Back / Forward / Reload / Home, but its address was a read-only label — you
could only ever see the URL the account was created with. Now:

- **The address bar is editable.** Type an address, press Enter, and the tab navigates there.
- **"Save site"** adds the page you're currently on to your sidebar as its own account, named after the
  site, with its own isolated sign-in. It asks first, so you can't add one by accident.

Real service tabs (WhatsApp, Google Business, Telegram, …) intentionally **don't** get this — they stay
pinned to their own site by the navigation guard, so an address bar there would only ever produce a
blocked navigation and a confusing dead end.

Two deliberate refusals, both to keep the app's "nothing leaves your machine" promise honest:

- Typing something that **isn't** a web address is rejected with an explanation. It is *not* forwarded to
  a search engine — that would ship whatever you typed off the machine.
- **Non-web addresses are blocked** (`file:`, `javascript:`, `data:`, `ftp:`, …). These tabs hold live
  signed-in sessions; `file:` would expose your disk to a page and `javascript:` is how script gets
  smuggled into someone else's site.

## v4.89.0


**Faster refresh, and an honest per-account status.**

Two changes that build directly on v4.88.0's in-memory reader:

- **Waiting counts refresh about every 25 seconds** (was 90). The new reader is cheap enough to run that
  often. If any account has fallen back to the older reader, the app keeps the whole cycle on the slower
  cadence rather than hammering the expensive one.
- **Each account shows a status chip** — *Live*, *Starting*, *Stale*, *Scan QR*, or *Failed*. Previously
  an account could show "connected" while its numbers had quietly stopped updating; that now reads
  **Stale**, with a plain-language explanation of what to do about it.

Under the hood, every channel now reports through one normalized event type instead of each part of the
app reaching into each scraper — which is what makes the Telegram, Messenger and Google review channels
straightforward to add later.

## v4.88.0


**Message previews for every chat, and a faster read on replies you sent from your phone.**

The app used to build its oversight numbers from the copy of your chats WhatsApp saves to disk. That copy
has two problems: the message text in it is **encrypted**, and it **lags** — a reply you send from your
phone can take a while to appear. To get preview text at all, the app had to read the chat rows WhatsApp
had drawn on screen (about 60 of them), so any customer further down your list showed a blank preview.

This release reads WhatsApp Web's **live, in-memory copy** of your chats instead — the same data the page
is rendering right now. That copy is already decrypted and always current, so:

- **Every waiting customer shows their actual message**, not just the ones near the top of the list.
- **A reply sent from your phone clears the "waiting" flag quickly**, without a manual Re-sync.
- Re-sync is faster, because it no longer needs to reload each account to harvest previews from the page.

This is additive and reversible. If WhatsApp ever changes its internals, the app **automatically falls
back** to the previous method rather than losing your numbers, and Settings → Data shows whether the new
reader is active. It remains strictly read-only — the app still never sends, replies, or marks anything
read on your account.

*Technique adapted from the open-source `wppconnect/wa-js` and `whatsapp-web.js` projects (both Apache-2.0),
which read the same in-page data. No third-party protocol library is used and nothing leaves your machine.*

## v4.87.0

- **Click-to-focus root cause, found and fixed.** Clicking a waiting customer has been "flaky, works sometimes" since v4.75, and every fix in between (v4.76/v4.78 name-vs-number search term; v4.79 oscillation) was a guess at the wrong layer. v4.84–v4.86 added a breadcrumb trace to app.log instead of guessing again. It settled it in three clicks: focus clicked the **correct** row every time (`want` phone == clicked title, saved *and* unsaved), reported success — and **no conversation opened** (`opened=<no-main-pane>`).
- The cause: **WhatsApp's chat rows don't respond to `element.click()`.** A synthetic click dispatches only a `click` event; the row's React handler wants the full pointer/mouse sequence a real mouse emits. So the code was clicking into the void and declaring victory — for six versions. `umRealClick` now dispatches `pointerdown → mousedown → pointerup → mouseup → click` on the title span (React listens at the document root and routes by target, so firing on the deepest node, not the row, is what reaches the handler). All three focus click-sites (rendered row, verified search result, top result) route through it.
- The search term was never the variable — neither name nor number was ever going to work through a click that did nothing. The verification read-back was hardened too (reports the composer box as well as the header, so a stale header selector can't masquerade as "no chat opened").
- Checks: `node`-run assertions that `umRealClick` fires the exact five-event sequence (and that a plain `.click()`, the old path, would not), plus the adapter script parses clean.

## v4.83.0

- **Review rows parse correctly — built against a live DevTools dump, not guesswork.** v4.82.0 showed the *location* as the reviewer, left the address/stars/age/`more_vert` inline in the text, and rendered the star glyphs as tofu boxes. The dumped card's real shape:
  ```
  0 "Depilex DHA-2 Islamabad"           <- location header: name…
  1 "Jinnah Boulevard, Islamabad"       <- …and address, on EVERY card
  2 "Anjum Afzal"                       <- the reviewer
  3 "×5 5 days ago"               <- stars AND age share ONE line
  4 "I had an excellent experience… More"  <- text, TRUNCATED by Google
  5 "reply"  6 "Reply"  7 "more_vert"
  ```
  The meta line is now the anchor: reviewer = the line directly above it (drops the header at any size), text = everything below. That line is why the old `^…$` age regex matched nothing.
- **Stars come from icon-font glyphs, not text or aria.** The page has *no* rating aria-label (its only ones are "Open review options"/"Review options"). Rating = the leading run of the first codepoint (`U+E838` = Material filled star), so a 3-star reads 3 — counting glyphs would read 5 every time, since all five slots are always rendered.
- **Full text now needs a click:** Google truncates long reviews in the DOM, so `__umGRExpand` clicks the "More" expander once per load, scoped to the cards actually read. If a build ignores the synthetic click the text stays truncated and the `... More` is stripped to an ellipsis.
- Check: `node UnifiedMessenger.Tests/Scripts/check-google-review-parse.js` — extracts the shipped JS out of the C# constant and runs it against the verbatim dumped card plus 1-star / rating-only / expanded / layout-drift cases.

## v4.82.0

- **Click a pending review → scrolls to it and highlights it; rows show the full review text, stars and age.** Google's review manager gives reviews **no individual URL** — they aren't addressable — so "open the exact review" means finding the card on the page: `FocusReviewAsync` navigates to /reviews if needed, re-bumps rows-per-page (a fresh load resets to 10), matches the card by reviewer name with a fall back to the Reply-button ordinal, then `scrollIntoView` + a 5s outline. ~12s retry window, mirroring `ConversationFocusHelper` (cold webview / merchant-view hand-back).
- **Card boundary is now found by action-button count, not character count.** The old climb took the smallest ancestor with 25–700 chars — so a review over 700 chars matched no ancestor and read as *empty*, which is why full text never worked. It now climbs to the largest ancestor still holding only that review's own Reply/Edit button.
- Extraction is best-effort by nature (Google exposes no stable per-review hooks); every field degrades independently and the Reply/Edit counts remain the reliable signal.

## v4.81.0

- **Google star rating + true review total:** each Google Business card now shows its real rating and lifetime review count (e.g. `4.6 ★ · 239 reviews · 100% replied (50 on this page)`). Neither exists on `business.google.com/reviews`; both live on the Search merchant view, so a background scrape (every 6h, on the manual/nav-allowed refresh only) reads the rating from its `Rated 4.6 out of 5,` aria-label and the total from the `239 Google reviews` body text, then navigates back to the reviews page. The replied % stays explicitly labelled as page-scoped rather than implying it covers every review.
  - Parse gotcha worth keeping: that page's `innerText` runs the two together (`"4.6239 Google reviews"`), so a bare `([\d,]+)` before "Google reviews" swallows the rating's decimal digit — it yields **6239**, not 239 (and `4.81,234` → 81234). The regex anchors on the rating to split them.

## v4.80.0

- **Reviews refresh on their own:** `ReviewHealthPanel.RefreshAsync` was only ever called from `RunDashboardResyncAsync`, so a review you'd already replied to kept showing as pending until a manual Re-sync. The panel now refreshes on load and every 5 minutes while the dashboard is open, passively (`window.__umGRAllowNav` blocks navigation on the auto path, so it never steals a visible tab).

## v4.78.0

- **Saved contacts searched by number, not name:** v4.76 started resolving saved names, which made `__umFocusConversation` search by the name — flaky (formatting/status noise) — while unsaved contacts kept searching by number (reliable). Focus now prefers the phone digits as the search term whenever a number is available (`term = phoneDigits.length >= 8 ? phoneDigits : name`); the saved-name row that comes back is verified by `umRowIsTarget`'s name check or the top-result fallback. Name is used only when there's no number. Removed the temporary v4.77.1 nav/focus diagnostic logging.

## v4.77.0

- **Focus falls back to the displayed number:** for an `@lid` chat whose contact-store phone map missed, `ContactPhone` came back empty, so focus had no searchable term and never searched. `__umFocusConversation` now, when `contactPhone` is empty/short and the name is a number (not letters), searches by the digits in `customerName` — the real number WhatsApp shows as the row title. So unsaved `@lid` numbers open again.

## v4.76.0

- **Saved contact names resolved (root fix for both display + focus):** `buildLidPhoneMap` now also builds a name map from the contact store (`name` → `verifiedName` → `pushname` → `shortName`, whichever exists — missing fields just skip, no regression), and the scan uses it so a saved `@lid` contact carries its real name. `OversightThreadEnricher` no longer forces the display to `"+<phone>"` when a real name exists — it prefers the name. Net: the needs-reply list shows "Muzzamil Naaz", and `__umFocusConversation` searches by that name (verified match, no full-number-search loop). Runs per-account, so each account resolves names from its own contact store.

## v4.75.0

- **Saved-contact focus no longer loops:** searching by number returns the contact shown by its **saved name** (no digits in the row), so `umRowIsTarget` never verified and the retry loop kept re-typing/clearing the search. `__umFocusConversation` now, after a precise full-number search (≥10 digits) with no verified row, clicks the **top result** — WhatsApp lists the matching contact/chat above any message-text hits, so it's the target. Fixes the flicker-loop and opens saved contacts. (Resolving the saved name for display + name-based search is the next step.)

## v4.74.0

- **Click-to-focus reaches `@lid` chats (the real fix):** the resolved phone (`ContactPhone`) is now threaded from the needs-reply click through `OpenInstance` → `InstanceNavigationRequest` → `ConversationFocusHelper` → `__umFocusConversation`. Focus previously only had the `@lid` privacy JID (not a real number), so it couldn't search — `term` was empty and it opened the inbox. It now searches WhatsApp by the resolved phone digits and verifies the result row before clicking, so `@lid`-with-a-known-number chats (which is what the real numbers in the list are) open correctly. Nameless `@lid` with no resolved phone remain unfocusable (nothing to search) — but those are dropped from the list entirely (v4.69).

## v4.73.0

- **About logo renders (real fix):** relative `ms-appx` `<Image Source="Assets/…">` doesn't resolve in this unpackaged app (no XAML `<Image>` in the app rendered), so the logo was blank even as a PNG. `AboutPage` now loads `icon-master.png` from its physical path via `ApplicationPaths.TryResolveIconMasterUri()` → `BitmapImage(new Uri(file://…))` in code-behind — the same file-path mechanism that already renders profile avatars and the tray icon.

## v4.72.0

- **About logo renders:** the hero logo was blank — a multi-frame `.ico` doesn't decode in a plain `<Image>` (it only works as the window `ImageIconSource`). Switched to the 1024×1024 RGBA `Assets/Branding/icon-master.png` master (transparent, renders reliably), shown at 112px with no backing tile.
- **Removed the "Free forever" badge** from the About hero (kept "100% local" and "No cloud").

## v4.71.0

- **Parallel Re-sync (Part A):** `RunResyncAsync` reloads + probes accounts through a bounded concurrency window (3 at a time) instead of strictly sequentially. Everything stays on the UI thread (WebView2 is UI-affine) — the awaits interleave, so the browser process runs the per-account scans concurrently and the wall-clock drops roughly by the concurrency factor. The progress bar advances as each account finishes (order-independent) and the ETA benefits from the more linear progress.
- **Background analytics refresh (Part B):** message-count analytics now refresh on their own. `BackfillSyncManager.SchedulePeriodicAnalyticsRefresh` runs *only* the message-aggregate scan (the separate `__umMsgAgg` global — it can't clobber the oversight snapshot scan), throttled to every 8 min per connected WhatsApp account, guarded against overlapping a full backfill or another refresh. Wired into the 90s `OversightAlertMonitor` tick, so the activity graph keeps up with new messages across all chats without a manual Re-sync. The live DOM observer still updates the open chat instantly.

## v4.70.0

- **Reliable click-to-focus (#1):** `ConversationFocusHelper` retry window widened from ~2.5s (5×500ms) to ~11s (16×700ms). A cold/just-switched WhatsApp webview needs several seconds to restore its session and render the chat list before `__umFocusConversation` can find or search for the target; the old window expired first, so the account opened, showed "loading", then gave up. Warm accounts still focus on the first attempt.
- **Live awaiting refresh (#2):** `OversightAlertMonitor` poll interval cut from 3 min to 90s, so the command center reflects new activity on its own (the dashboard already re-renders every 20s) without a manual Re-sync. The scan is a bounded chat-store read, so the cadence stays light.
- **Re-sync ETA (#2):** the progress line now shows an approximate "~time left", computed from elapsed time vs. progress, once past the quick reload phase.
- **Reviews click-through (#3):** already wired — the "N to reply" chip and each pending review row open that account's Google reviews page (`OnOpenReviewsClick` → `OpenInstance`); it benefits from the same wider focus window.
- **About screen redesign (#4):** a hero card with the app logo, name + version, local/no-cloud/free badges and an accent update button, plus "What it does" and checkmarked "Key features" cards. Uses the reliably-decoding `AppIcon.ico`.

## v4.69.0

- **Anonymous `@lid` contacts dropped from oversight:** a `@lid` privacy chat with **no** entry in the lid→phone map **and** no real name (empty, "New message", or a name that's just the `@lid` digits) is skipped in the scan. WhatsApp exposes no way to identify or open these, so they were non-actionable noise showing as fake `+<lid-digits>` numbers. `@lid` chats that resolve to a phone or carry a name are kept — real, reachable customers on WhatsApp's newer addressing.

## v4.68.0

- **No modal error on unfocusable chats:** `ShellNavigationCoordinator` no longer shows the blocking "Could not open conversation" dialog when focus fails. Some chats genuinely can't be auto-located (WhatsApp `@lid` privacy contacts with no name/number), and the account already opens on its inbox — a fine fallback — so interrupting with an error every time was wrong. The `InstanceNavigationFailed` event still fires for any future non-modal listener.

## v4.67.0

- **Self-chat excluded from oversight:** the "Message yourself" chat is keyed by your own number (a plain `@c.us` chat), so it passed the customer filter and could appear as awaiting. The scan now reads this account's own WID from WhatsApp Web's `last-wid-md` localStorage entry (e.g. `923262104455:95@c.us`), strips the device suffix, and skips the chat whose number matches — so your self-chat never counts as a customer awaiting a reply.

## v4.66.0

- **Verified click-to-focus (safety fix):** `__umFocusConversation` no longer clicks the first search result. It only clicks a row it can verify is the target — visible-title **phone digits match** (@c.us) or a **real contact name match** — for both the rendered-row and search paths. A nameless `@lid` privacy id has neither, so it is never searched (searching it matched unrelated message text and once opened the self/Notes chat); focus returns false → the account inbox opens instead of the wrong chat. `@lid` JIDs are treated as non-phone ids, not searchable numbers.

## v4.65.0

- **Deleted (recalled) messages no longer stay "awaiting":** the scan reads `data-icon="recalled"` from the rendered chat-list row / harvested preview, so a customer's deleted last message clears even when WhatsApp's persisted `lastMessage` is absent (throttled background webview). Threads through both the DOM hint (`umBuildDomChatHints`) and the preview harvest into the `lastRevoked` guard.
- **Click-to-focus works for off-screen and unsaved chats:** current WhatsApp Web sidebar rows carry no `data-id`, so `__umFocusConversation` now drives the chat **search box** (`input[aria-label="Search or start a new chat"]`, a React-controlled input) to render the target, then clicks the top result on the focus-helper's retry. Saved on-screen chats still take the fast title path.

## v4.64.0

- **"Needs reply" is direction-based, not unread-based:** the oversight snapshot now reports the same awaiting number the dashboard uses (`OversightSnapshotReader` reads back through `OversightChatSnapshotService.TryGetWindowed`), driven by the last message's `fromMe` direction (+ sticky-awaiting + mark-handled overrides) rather than WhatsApp's unread badge. Unread is per-device read-state, so two linked devices disagreed and a chat read-but-not-replied on the phone wrongly cleared; direction is message content, so installs converge.
- **WhatsApp official/system account (`0@c.us`) excluded:** its one-way notices no longer count as an awaiting customer chat, nor inflate the message analytics.
- **File downloads work like a browser:** a `DownloadStarting` handler + relaxed nav guard (`blob:`/`data:` no longer cancelled) let received WhatsApp media/documents save with WebView2's built-in download UI.
- **"Check for updates" moved to About** (next to the version); the manual install-when-available path is unchanged.
- **Tests always run the Release build:** `Directory.Build.props` defaults the repo to Release, so a bare `dotnet test`/`dotnet build` never produces a stale Debug binary.

## v4.59.0

- **New vs returning customers (Weekly report):** a new insight and at-a-glance line — *"12 new · 34 returning customers this week"* with the returning-customer rate. A local `ContactHistoryStore` records the first and last time each customer contact was seen per account (identity is the phone number when known, so saved/unsaved dedupe to one person; groups/status/broadcast are excluded). It's gated on ≥1 week of history so it doesn't misreport every contact as "new" right after install. Fully local, retention-bounded (180 days).

## v4.58.0

- **Bento dashboard layout (Phase C):** on wide windows (≥1360px) the **Activity patterns** and **Reviews** panels render **side by side** rather than stacked, using the horizontal space instead of a long vertical scroll. Below that width they reflow to a single full-width column, with an animated transition. The command center stays full-width above.
- **Motion & consistency (Phase D):** the bento panels animate on entrance and reflow; KPI tiles and the briefing strip now share the same 10px corner radius as the cards for a consistent card family.
- *Part 2 of the UI/UX modernization in `docs/ux-modernization-plan.md` (Phases C + D). Empty-state sweep, settings/dialog coherence and sidebar density follow.*

## v4.57.0

- **Command-center hero (Phase B):** the command center opens with one large, colour-coded status line — **"You're all caught up"** (green) or **"N customers are waiting for a reply"** (red) with the oldest wait, the account furthest behind, and a **Review now** button. It answers the one question you open the app for in ~5 seconds, instead of a row of equal-weight tiles with nowhere for the eye to land.
- **One banner, not four:** the digest, backlog, weekly-report and define-locations notices used to stack above the accounts. Now **at most one shows** (highest priority first) and the rest fold into a **"+N more"** count — so the accounts aren't pushed below the fold.
- **Dark-mode fix + design tokens (Phase A):** brand colours are now **theme-aware tokens** (`ThemeDictionaries`). Section headers previously used a hard-coded near-black that was nearly invisible on dark Mica — they now lighten correctly. Metric values and section headers also step up in size for a clearer type scale.
- *This is part 1 of the UI/UX modernization in `docs/ux-modernization-plan.md` (Phases A + B). Bento layout, card depth/motion, and the empty-state sweep follow in later releases.*

## v4.56.0

- **One-click local backup & restore (Settings → Data & Privacy):** back up your settings, accounts, message analytics, response-time history, KPI trend, oversight snapshot and custom account icons to a single `.zip` — fully local, nothing leaves the machine. **Restore** replaces the current data and prompts a restart. The WebView2 sign-in sessions (huge, machine-bound) and Ollama models (re-downloadable) are deliberately excluded, and restore is guarded against zip-slip and unrecognised archives.

## v4.55.0

- **Business-hours-aware response times:** set a location's working hours (Settings → Workspaces) or turn on Quiet hours, and the response-time / SLA clock **pauses outside those hours** — a customer who messages at 11 PM and is answered at 9 AM counts as a fast reply, not a 10-hour one. Falls back to raw wall-clock when no hours are configured.
- **AI-narrated report headline:** the Weekly report can open with a one-sentence, encouraging summary phrased by your **local Ollama model** (aggregate facts only — never customer names or message text; 12s timeout, degrades silently to the deterministic summary). Off unless local AI is enabled.
- **Save report as image:** a **Save as image (.png)** button on the Weekly report renders the summary + insights + trend to a shareable PNG. Alongside the existing Save (.md) / Export (.csv).
- **Weekly-report reminder:** a once-a-week, dismissible banner in the command center nudges you to review the business report. Fully local — **no OS scheduled task** (the app already runs continuously in the tray). Toggle in Settings → Notifications; opening the report resets the weekly clock.

## v4.44.0

- **Smarter AI shift briefing (#33/#34/#36):** the briefing now adds an **end-of-day projection** ("on pace for ~N today"), an **anomaly flag** ("busier than usual"), and a **ranking rationale** (the account furthest behind + its caught-up %). Deterministic heuristics with a local-AI swap when Ollama is on.

## v4.43.0

- **AI shift briefing (#25):** a one-line, whole-business "where to focus first" summary under the KPI band — deterministic heuristic always, swapped for a local-AI line when Ollama is on (aggregate counts only; account names but never customer names/text).
- **Week-over-week trend (#37):** the Activity patterns panel now shows this-week-vs-last-week message volume + the busiest weekday, derived from the on-device activity history.

## v4.42.0

- **Google Business review-health (Phase 4):** a new dashboard **Reviews** section scrapes each Google Business account's live reviews page for **reviews awaiting a reply** (the actionable signal) and **reply rate** on the loaded page. Refresh on demand. (Google exposes no aggregate rating/total on the manager reviews page, so those aren't shown.)

## v4.41.0

- **Custom account icons (expanded):** right-click an account → **Change icon** to choose a social-media brand logo (WhatsApp, Telegram, Instagram, Facebook, Messenger, X, TikTok, YouTube, LinkedIn, Discord, Pinterest, Reddit, WeChat, Google), a general icon, **import the account's profile photo**, or **upload an image from your PC**. Reset to initials anytime.

## v4.40.0

- **Command-center redesign:** at-a-glance KPI band (caught up · awaiting · messages/day · busiest window), redesigned account cards (avatar, status %, full-height status rail, awaiting pill, in-card AI strip), info-styled dismissible digest, single-scroll dashboard.
- **Activity patterns graph:** one filterable chart — Hour of day / Day of week / Month — with account + range filters, peak highlight, and a plain-language insight line. Reads an on-device activity-history log (retained ~400 days; fully local).
- **Durable oversight snapshot:** the live dashboard (caught-up %, awaiting list, counts) now persists to disk, loads instantly on launch with an "Updated …" stamp, and re-sync updates incrementally instead of starting blank. Analytics history merges (never wipes/double-counts) on re-sync.
- **Custom account icons:** right-click an account → **Change icon** to pick a social-media brand logo (WhatsApp, Telegram, Instagram, Facebook, Messenger, X, TikTok, YouTube, LinkedIn, Discord, Pinterest, Reddit, WeChat, Google — via a bundled Font Awesome Brands font), a general icon, or **import the account's profile photo** from its live session. Reset to initials anytime. Shows in the sidebar and dashboard cards.
- **Bug fixes:** removed a stray floating `Ctrl+D` tooltip (suppressed auto-generated accelerator tooltips); **fixed account names vanishing** after adding an account (the sidebar reused cached rows whose label references were cleared on rebuild — rows are now recreated so titles/status/badges stay correct); the Change-icon dialog no longer gets occluded by an open account's WebView.

## v4.34.0


- **The real cause of the freezes: a registry semaphore re-entrancy deadlock.** Every instance mutation — Remove, Move up/down, Rename, Set location, Mute, Memory tier — does `await _gate.WaitAsync()` and then looked up the instance via the public `FindById`, which itself did a synchronous `_gate.Wait()` on the **same** non-reentrant `SemaphoreSlim`. Holding the gate and re-acquiring it = instant permanent deadlock that froze the whole app (and was also why the registry tests "hang" per AGENTS.md). Fixed by giving in-gate callers a lock-free `FindByIdNoLock`. A regression test now drives all eight mutators in sequence under a 10s cap. *(v4.33's WebView2-timeout hardening still stands as defense-in-depth, but this deadlock was the freeze.)*

## v4.33.0


- **Instance context-menu freeze fixed (especially "Remove instance").** Removing an instance disposes its WebView2, and the teardown awaited `TrySuspendAsync()` on the UI thread with **no timeout** — on a busy/loading WebView that await could never complete, hanging the UI thread and freezing the *whole* app (so every other context-menu action you tried afterward also looked stuck). Two fixes: (1) disposal now **closes the WebView directly** without the pointless pre-close suspend; (2) **every WebView2 operation now has a 12-second timeout** (`WebViewUiAwaiter`) — a wedged op becomes a recoverable, logged error instead of a permanent freeze. This covers Remove, Refresh WebView, account switching, and the permanent-delete profile wipe. The non-WebView actions (Move, Rename, Mute, Set location, Memory tier) never touched WebView2 — they only appeared stuck because a hung Remove had already frozen the app.

## v4.32.0


- **Removed the dormant OCC / Work Queue subsystem.** Retired as a destination in v4.27.0 and with its SLA logic harvested into the command center in v4.31.0, the kanban/triage/branch-filter subsystem was deleted: **43 files / ~5,400 lines** — the OperationsCommandCenter control + partials, KanbanColumnBoard, WorkQueuePage, the `Occ*` filter/view-mode/state services, presenters, view-models, the branch pill bar, and 8 OCC tests; plus the Work-Queue navigation (ShowWorkQueueAsync, the Ctrl+Shift+Q shortcut, the sidebar Work Queue button, and the OCC command-palette/nav-event handlers). The **shared** SLA/triage engine (`ThreadData`, `BusinessHoursCalculator`, `OperationalThresholds`, `MessageTriageService`, `ThreadRegistryService`) is kept — it feeds the command center's "N late" metric. A few `Occ`-prefixed *utilities* that the live analytics/personal-dashboard depend on (`OccDateRangeFilterHelper`, the `OccQueueFilter`/`OccViewMode` enums) were kept as well. 83 tests green.

## v4.31.0


- **The real business-hours SLA is now on the cards (P1-A).** MASTER-PLAN §8's centerpiece — reply-latency measured within each location's working hours — was computed in the rollup but then thrown away in favour of the unread-based "caught up %". Each card now also shows a **"N late"** sub-metric (next to urgent/dropped): open conversations past their business-hours reply SLA (`ThreadData.IsSlaBreached` + per-location `BusinessHoursCalculator`). It's independent of the caught-up %, so responsiveness — not just unread state — is finally visible, and it shows 0 when there's no thread data.
- **OCC decision (P1-B):** the dormant Operations Command Center stays retired; its valuable SLA logic (which lives in shared services, not the kanban UI) is harvested into the command center rather than deleted. Documented in `docs/remaining-work.md`.

## v4.30.0


- **WCAG 1.4.1 coverage finished (P1-C).** The shape-distinct status glyph (✓/⚠/⨯) was only on comfortable-density cards. It now also appears on **compact cards** (where the % is hidden, so status was previously colour-only) and a warning glyph precedes the count on **Needs-reply rows** — status is never conveyed by colour alone anywhere.
- **Sticky-awaiting safety valve (P1-D).** v4.26's sticky-awaiting could, in theory, keep a chat "awaiting" forever if an outbound reply was never observed (no DOM hint, no persisted last message). A chat can now only be carried as awaiting via inheritance while its last activity is within **7 days**; past that an unconfirmed-clear is allowed through, so it can't get permanently stuck. A genuinely-waiting chat keeps getting fresh awaiting reads and is unaffected. (Regression test added.)

## v4.29.0


- **Workspace sidebar redesign.** A cleaner, more functional account rail (research-backed: clear hierarchy, channel cues, density, collapsible groups):
  - **Collapsible location groups** — each location sub-header (e.g. "DHA-2") now has a **chevron + account count** and can be collapsed/expanded (click or keyboard); collapse state persists across refreshes and only applies in the expanded rail.
  - **Channel-aware row subtitles** — each account's second line now shows its **channel** ("WhatsApp", "Meta Business Suite", "Google Business", "Discord"…) instead of a repeated "Connected · syncing". Real problems still surface ("Signed out — tap to reconnect", "Connection error"); transient connecting/syncing is conveyed by the status dot's colour.
  - **Tighter density** so more accounts fit without scrolling.
  - The full sidebar contract (navigation events, badges, health dots, accessibility, compact icon-rail, context menus) is preserved — purely additive/visual.

## v4.28.1


- **Command center no longer shows "No professional accounts yet" on startup** while WhatsApp's first IndexedDB history scan is still running. It now reads **"Syncing accounts — reading each account's local history…"** when oversight accounts exist but haven't reported data yet, and only says "no accounts" when there genuinely are none.

## v4.28.0


- **New embed channels — Discord, Meta Business Suite, Instagram.** "Add account" now offers these alongside WhatsApp / Google Business / Telegram / Messenger / generic. They were missing, so trying to add (e.g.) a Discord or Meta Business Suite account fell back to **WhatsApp** — the instance then loaded WhatsApp Web. Each new channel is embed-only (own isolated session, branded accent, no oversight scraping yet).
- **Google Business "browser not supported" fixed.** Only Discord previously got a desktop user-agent; every other embed used WebView2's default UA, which Google/Meta reject. All embed channels (Google Business, Meta, Messenger, Telegram, Discord, Instagram, generic) now send a clean desktop **Chrome UA**. WhatsApp keeps its default UA (the scraper depends on it). *Note:* Google's sign-in may still resist embedded browsers (their anti-embedding is aggressive); the UA fix removes the blanket "unsupported browser" block.
- *Heads-up:* instances created before this (named "Meta Business Suite"/"Discord" but stored as WhatsApp) won't auto-correct — remove and re-add them on the proper channel.

## v4.27.1


- **Embed channels no longer clutter the command center.** A *professional* Google Business / Telegram / Messenger / generic instance has no WhatsApp chat store to scan, so it would show in the oversight cards stuck at "syncing…" forever. The command center (and the "Needs reply" list) now include only oversight-capable platforms (WhatsApp family). Embed channels stay fully visible and usable in the sidebar — they just don't appear as oversight cards.

## v4.27.0


- **"Needs reply" — the Work Queue, merged into the Dashboard.** The command-center segmented control gains a third mode (**By account ∣ By location ∣ Needs reply**). "Needs reply" is a single **flat, cross-account list of every customer awaiting a reply, worst-first** (most unread, then longest-waiting), each row a click-through straight to the live chat. It's derived entirely from the same oversight snapshot that powers the per-card accordion — no manual drag-to-status, no drift, and fully consistent with the read-only stance (a row just navigates you to the chat to reply by hand). Respects the date window and compact density.
- **Standalone Work Queue (kanban OCC) retired** as a sidebar destination — its purpose now lives in "Needs reply." The page and OCC code remain intact and dormant (still reachable via Ctrl+Shift+Q / command palette), so the change is reversible. *Why:* a manual kanban duplicated the Dashboard's awaiting data, fought the app's passive/derived philosophy, and sat at the wrong altitude for an owner (a doer's tool, not an overseer's).

## v4.26.1


- **Embed channels now appear in the sidebar.** Adding a Google Business (or Telegram / Messenger / generic URL) account left it addable and visible in the Work Queue but **invisible in the sidebar** — so it could never be opened, and therefore "never loaded." The sidebar was gated on a WhatsApp-only check (`IsPlatformModuleEnabled`) that's really the "participates in WhatsApp scraping pipelines" gate. Split it: WhatsApp-only stays for backfill/adapter/analytics, and a new `IsSidebarVisible` (any addable platform) drives sidebar visibility. Embed channels now show and open normally. (4 regression tests across the embed platforms.)

## v4.26.0


- **Removing an instance no longer crashes** with *"the application called an interface that was marshalled for a different thread."* The teardown touches WebView2 (COM/STA) and UI-coupled services, so it must run on the UI dispatcher thread; it's now pinned there via `UiThreadRunner` (a plain `ConfigureAwait` isn't enough — WinRT awaitables resume on thread-pool threads regardless).
- **Moving an instance up/down no longer hangs the app.** The sidebar menu rebuild used an incremental reconciliation that could re-insert a cached row still parented at another index — WinUI mishandles re-parenting inside the same panel and wedged the layout pass. The rebuild now detaches and re-adds in order (flicker-free at this list size).
- **Opening a chat no longer counts as "replied."** Caught-up % is direction-first now: a chat is "awaiting" when the last message is **not from us**, using WhatsApp's persisted message direction first and the rendered-row direction next; the unread marker (which clears the instant you open a chat) is only a last resort. A new **sticky-awaiting** rule keeps a chat marked awaiting until an outbound reply is actually observed, so opening an off-screen chat can't silently flip it to "caught up." (3 regression tests.)

## v4.25.1


- **Cleaner AI insight strips:** the on-device model's output is now sanitized harder before it reaches a card. Previously a sentence ending in a quote (e.g. `…respond immediately." Next action steps: …`) slipped a verbose run-on and a stray quote into the strip. The sanitizer now cuts at the first sentence/clause boundary (`. ! ? ;`) regardless of a following quote, strips interior quotes, and enforces a hard word/character cap (the small model routinely ignores the prompt's length limit). Three regression tests added.

## v4.25.0


- **WCAG 1.4.1 — non-color status cue:** each command-center card now shows a shape-distinct status glyph (✓ on track / ⚠ needs attention / ⨯ behind) next to the caught-up %, so health is communicated by **shape, not colour alone**. The glyph carries an accessible name and tooltip. Closes the §9 "colour + icon + text (never colour alone)" principle for the L0 cards (comfortable density).

## v4.24.0


- **Shell modernization (closes the v4.24.0 increment of the UI/UX plan):**
  - **Title-bar scope selector:** the account-scope switch (All ∣ Professional ∣ Personal) moved out of the sidebar and into the title bar, per the §9 shell spec. It still appears only when both scopes have accounts, persists across sessions, and re-renders the rail immediately. The scope state stays owned by the sidebar; the title-bar control drives it through a small public API (`WorkspaceSidebar.SetScope` / `ScopeSelectorStateChanged`), so the rail's render logic was not disturbed.
  - **Title-bar AI toggle:** a one-click **AI** toggle in the title bar mirrors `AppSettings.EnableLocalAi` — flip on-device AI insight strips on/off without opening Settings. It stays in sync if the same setting is changed from Settings → AI, and degrades gracefully to heuristics when off or when Ollama is unavailable.
  - **Compact sidebar by default:** fresh installs now open with the 56px icon rail (`SidebarPinnedExpanded` defaults off); the title-bar pin button expands it. **Existing users keep their persisted expanded/compact preference** — the new default only applies to brand-new installs.

## v4.23.0


- **Shell IA foundation:** core state and dialog infrastructure for the oversight-foundation branch:
  - `ShellViewState.WorkQueue` — new enum case; `WorkspaceSidebarHelper`, `WorkspaceSidebarViewModel`, and `MainWindowViewModel` all track work-queue selected state correctly so the sidebar selection ring follows navigation.
  - **SetLocationDialog** — `IsProfessional` accounts can now be grouped under named locations via the context-menu "Set location…" entry; existing locations auto-populate the editable combo.
  - **ConfirmPermanentDeleteDialog** — safety confirmation before permanent instance removal (replaces an inline `ContentDialog` build in `ShellController`).
  - **AutoUpdateDialog** — prompt shown by `GitHubUpdateService` when a newer installer version is detected; user can install now or defer.
  - **PinToTaskbarDialog** — one-time nudge (respects `HasPromptedPinToTaskbar` setting) to pin the app for quick access.
  - **MainWindow event-handler refactor:** all inline lambdas in `AttachShellHandlers` / `DetachShellHandlers` are now named methods so the detach pass actually removes the correct delegate (previously the detach was a no-op because lambda identity doesn't match).
  - **Sidebar drag-reorder removed:** OLE drag-in-ScrollViewer causes a WinUI freeze; the drag loop code and `_isDragging` guard have been removed. The `InstanceReorderRequested` event stub is preserved with `#pragma warning disable CS0067` for future re-wiring.
  - **ScopeFilterCombo compact-mode fix:** the scope filter combo was shown unconditionally in compact sidebar mode; it now follows the same `_isCompact` guard as other labels.

## v4.22.0


- **Command-center visual modernization:** three coordinated improvements to make the dashboard closer to the designed target:
  - **Vertical card layout:** each account card now shows a colored avatar circle (initials + account accent color), a large bold caught-up %, and the sparkline stacked naturally rather than as a fixed-width horizontal row.
  - **Bar-chart sparklines:** the 7-day activity trend is now a bar chart (colored vertical bars, rounded tops) instead of a line polyline — more readable at a glance and color-matched to each account's health status.
  - **Urgent + dropped sub-metrics:** `UrgentCount` and `DroppedCount` were already computed in the oversight engine but never surfaced in the UI. They now appear as compact "N urgent / N dropped" labels under the % when non-zero.
  - **Jump button on the needs-attention banner:** when urgent customers are waiting, the banner now shows a "Jump" button that navigates directly to the most critical account's WebView.
  - **"Define locations" CTA:** when no workspace profiles are configured and the dashboard is in per-account mode, a one-line prompt offers a direct link to the Workspace Management settings section.
  - **Segmented grouping control:** the "By account / By location" toggle switch is replaced by two adjacent toggle buttons that read as a segmented control.
  - **Insight strip dark restyle:** the AI/heuristic insight strip uses a dark neutral surface (consistent regardless of alert severity) with an amber ✦ badge — severity is already communicated through the % color in the card header.

## v4.21.0


- **Telegram + Meta Messenger embed channels (Phase 5 — embed slice):** "Add account" now offers **Telegram** (`web.telegram.org`) and **Messenger** (`messenger.com`) as branded channel options, each loading in its own isolated WebView session with a per-platform accent colour. Same embed-slice model as Google Business (v4.20.0) and generic web page (v4.19.0): the channels are fully usable for manual reading and replying, but **no adapter scraping** yet — conversation metrics (unread/awaiting) are not yet surfaced in oversight. Each will have its own adapter when a live logged-in account is available to tune the DOM reader against. Meta Messenger carries higher maintenance risk (Meta actively fights automation) so the adapter scope is passive read-only only.

## v4.20.0


- **Google Business reviews channel (Phase 4 — embed slice):** "Add account" now offers **Google Business**, a branded channel that loads your Google Business reviews console (`business.google.com`) in its own isolated session — reviews one click away alongside your messaging accounts. **Scope note:** this is the *embed* slice. Automatic review-metric scraping (star rating, % responded, unanswered count surfaced in oversight) is the planned next step — it needs a live, logged-in Google Business account to build and tune the DOM reader against, so it ships separately rather than as unverified code. For now the channel routes to the no-op adapter (no oversight metrics).

## v4.19.0


- **Generic web-page instances (Phase 3):** "Add account" now offers a **Web page** platform — enter any http/https URL and it's monitored in its own isolated WebView tab (own profile/session), just like a messaging account. No adapter scraping and **no oversight metrics** (it routes to the no-op adapter), so it's a lightweight way to keep a dashboard, booking page, or web tool one click away. The plumbing (custom-URL field, no-op adapter, generic chrome CSS) was already present; this registers the `generic` platform so it's selectable and no longer collapses to WhatsApp.

## v4.18.0


- **Command-center filter + density (Phase 3 — scale):** a **filter box** in the command-center toolbar narrows the cards to accounts/locations whose name matches as you type (in By-location view, a location shows if its own name matches — all members — or only the members that match). A **Compact / Comfortable** toggle switches to denser rows (tighter padding, smaller %, sparkline and freshness label hidden) so a multi-location owner with many accounts can see more at once. No data changes — purely how the existing oversight rows are filtered and laid out.

## v4.17.0


- **Local-AI insight strips (Phase 2):** when **Settings → AI** is enabled and the on-device Ollama runtime is reachable, each "needs attention" strip is re-phrased by the local model (`phi3:mini` by default) into a natural one-line assessment + next step, marked with a small **✦ AI** tag. It's **fully on-device** — only aggregate counts (waiting/unread/oldest-wait/caught-up %) are sent to the local model, never customer names or message text. Generation is background, cached per account by a state signature, and serialized so a burst of accounts doesn't hammer the runtime. If AI is off, still loading, or unreachable, the strip shows the instant **heuristic** line from v4.15.0 — so it never blocks or regresses.

## v4.16.0


- **Idle-session reaper (track C — lifecycle/memory hardening):** the WebView concurrency cap was only enforced when a *new* account was opened, so briefly-visited accounts stayed live and held RAM indefinitely. A 1-minute timer now closes any non-visible session that's sat idle past `IdleSessionReapMinutes` (default 20). **Professional accounts are exempt** — they stay live so background oversight keeps reading them — and the **visible** account is never reaped. Closing a session doesn't sign it out (the profile's on-disk data persists); it reloads, still signed in, on next open. Set the minutes to 0 to disable. Complements the existing per-instance LRU cap, memory tiers, low background memory target, and 90s stale-adapter recovery.

## v4.15.0


- **Command center insight strips (track B):** any account that needs attention now shows a one-line, plain-language summary under its health row — e.g. *"Needs attention — 5 customers are waiting on a reply · 3 unread · oldest 2 hrs ago."* The strip is **amber** when the account is still mostly caught up and **red** when it's falling behind; fully caught-up accounts stay quiet (no strip). It's a **deterministic, on-device heuristic** — instant, no cloud, no API, no AI runtime required, so it always works at zero cost. (Optional local-AI enhancement can layer on top later.)

## v4.14.0


- **Command center visual polish (track A):** each account is now a proper **card** (rounded border, surface background) with a **status-colored accent bar**, a **large prominent caught-up %**, a 15px account name, and clearer awaiting labels ("needs reply" for read-but-unanswered chats instead of "0 unread").

## v4.13.2


- **Groups/broadcasts excluded from oversight:** the replied-based "awaiting" introduced in 4.13.1 wrongly counted internal team groups (e.g. "Team Anfal", "Daily Branch Status") and broadcasts as awaiting a reply. Oversight now skips `@g.us` / `@broadcast` / `@newsletter` / status chats — only 1:1 customer conversations count.
- **Clean message previews:** the sidebar preview scrape now targets the message-text span and strips icon-token noise (`ic-imagePhoto`, `wds-ic-readYou`, `ic-push-pin`).
- **Better unsaved-contact names:** broader title extraction (primary cell + title attribute) so unsaved 1:1 chats show their number/name more often instead of "Unsaved contact".

## v4.13.1


- **"Awaiting" now means not-replied, not just unread:** a chat where the **customer had the last word** counts as awaiting even after you open/read it. Derived from the last-message **direction** (the sidebar's sent-tick), falling back to the unread marker only when the chat isn't rendered. Fixes "it says caught up even though I haven't replied."
- **Phone numbers for unsaved contacts:** 1:1 chats with no saved name now show the number from the sidebar (was "Unsaved contact").
- **No more accordion flashing:** the command center skips its card rebuild when nothing changed (render change-detection), so the 20s auto-refresh no longer makes the lists flicker.

## v4.13.0


- **Command center is the home surface (L0):** the Dashboard now *is* the command center (full width), with Personal Overview in a collapsible Expander.
- **Work Queue (L1):** the Operations Command Center moved to a dedicated **Work Queue** page with its own sidebar button (`Ctrl+Shift+Q`), header status, and refresh; its branch-filter / lane-focus / urgent-queue navigation commands route here.
- **Workspace Management in Settings:** a "Workspace management" section with a live summary and an "Open workspace manager" button (still reachable via `Ctrl+K`).
- **Guided cold-start onboarding:** a 4-step first-run wizard (Welcome → Add account → Set locations → Hours/SLA) with per-step skip, then optional follow-up dialogs. Hardened so a wizard hiccup never crashes startup or nags on every launch.

## v4.12.4


- **Sidebar drag-reorder removed:** dragging a row inside the scrolling menu reliably froze the app (a WinUI drag-in-ScrollViewer issue that three targeted fixes couldn't resolve). Drag is disabled (`CanDrag=false`, drop target off). **Reorder accounts via the right-click "Move up / Move down" menu** (added in 4.12.3) — reliable and freeze-free.

## v4.12.3


- **Real drag-freeze cause fixed:** the sidebar navigated to an account on **`PointerPressed`**, so the instant you pressed to start a drag it kicked off a heavy WebView switch on the UI thread → freeze. Navigation now happens on **`Tapped`** (a click without a drag), so dragging no longer triggers a switch.
- **Reliable Move up / Move down:** the account right-click menu gains drag-free reorder via `MoveInstanceAsync`, so repositioning always works regardless of drag.

## v4.12.2


- **Drag-reorder hang fixed:** after the v4.12.1 crash fix, a drag could still freeze the app because frequent connection-status updates (accounts "Connecting…/syncing") called the sidebar's `Refresh` *during the live drag*, restructuring `MenuStack` and removing the dragged row out from under the OLE drag loop. The sidebar now tracks an `_isDragging` flag (set on `DragStarting`, cleared on `DropCompleted` and before the deferred reorder) and **skips the structural rebuild while a drag is in progress** — doing only safe content updates, then restructuring once the drag ends. Context-menu actions verified wired end-to-end.

## v4.12.1


- **Drag-reorder crash fixed:** dragging a sidebar account to reposition it crashed natively because the reorder rebuilt the menu — removing the dragged element — *synchronously inside the drop event*. The reorder is now **deferred to the next dispatcher tick** so the drag-drop operation completes first. Both handlers are also exception-guarded.
- **Same class fixed on the OCC kanban board:** its drag-over accessed `DragUIOverride` without a null check and fired transfer/re-render events synchronously in the drop — now null-guarded, exception-safe, and deferred.

## v4.12.0


- **Shell IA (step 3) — location rail:** right-click a professional account → **"Set location…"** to assign it a location (pick an existing one or type a new name; "Clear" to remove). Accounts sharing a location now appear under a **location sub-header** in the sidebar (single-account locations stay flat so the rail isn't cluttered) — and they already roll up together in the command center's By-location view. New lightweight `UpdateInstanceBranchKeyAsync` (metadata only, no session reload).

## v4.11.1


- **Startup-crash hotfix:** the new scope-switch ComboBox fired its `SelectionChanged` during `InitializeComponent` (from an initial `IsSelected`), which ran the sidebar render before services were ready and crashed startup ("Cannot create instance of type WorkspaceSidebar"). The initial selection is removed and the handler is guarded until the sidebar's first real refresh.

## v4.11.0


- **Shell IA (step 2) — scope switch:** when both Professional and Personal accounts exist, the sidebar shows an **All / Professional / Personal** selector that filters the account list to one scope (persisted). Hidden for single-scope setups so it never hides your only accounts.

## v4.10.0


- **Shell IA (step 1) — scope-grouped sidebar:** the account list now splits into **Professional** and **Personal** sections (when both exist) instead of one flat "Active accounts" list, making the Personal/Professional scope first-class in navigation. A single-scope setup keeps one clean header. Foundation for the location rail and scope switch.

## v4.9.3


- **Audit fixes:** By-location accordions now keep their expanded/collapsed state across the 20s auto-refresh (instance rows already did; locations didn't). The IndexedDB scan is now serialized per instance, so the background monitor and a manual Re-sync can't clobber each other's shared result.

## v4.9.2


- **"Since you were last here" digest (A):** once per session, the command center summarizes what's waiting — *"Since Jun 18, 9:14 AM: 7 new awaiting reply · 21 total across 2 accounts · oldest since…"* — using a persisted last-seen timestamp. (`OversightChatSnapshotService.BuildDigest`.)
- **Hardened chat-store read (B):** the IndexedDB scan watchdog now allows 20s (was 8s) so a busy account's `getAll` over thousands of chats completes instead of timing out into "syncing…".
- **Configurable alert threshold (C):** Workspace management (Ctrl+K) now has an **"Alert when awaiting reply reaches N"** setting (0 = off, default 5); the background monitor reads it each pass.

## v4.9.1


- **Custom date range:** the command-center window selector adds **"Custom range"**, revealing **From/To** calendar pickers. Caught-up % and the awaiting list are then scoped to chats active in that range (To is inclusive through end-of-day). `OversightWindow.Custom` + `windowEndUtc` plumbed through the snapshot queries.
- **More robust message preview:** the awaiting-list glimpse now scrapes the sidebar's secondary cell with broader selectors and falls back to matching by chat **title** when the row `data-id` doesn't line up with the chat id — so previews show for more chats.

## v4.9.0


- **Proactive awaiting-reply alerts:** a background monitor re-reads each connected professional account's unread snapshot every ~3 minutes and raises a **desktop toast** when an account's awaiting-reply count crosses a threshold (default 5) — edge-triggered so it won't spam. This also keeps the command-center numbers fresh between manual re-syncs. (`OversightAlertMonitor` + `OversightSnapshotReader`.)
- **Message glimpse in the awaiting list:** each waiting chat now shows a one-line preview of its last message (scraped from the sidebar, since WhatsApp Web doesn't persist `lastMessage` in the chat store), so you can triage who to answer first without opening each chat.

## v4.8.9


- **Header and awaiting list can no longer disagree:** previously an account whose chat-store read hadn't landed showed thread-based numbers in the header ("21 awaiting reply") while the accordion — driven only by the unread snapshot — was empty. Now an account with no chat data reads **"syncing…"** with a matching empty list, so the headline always reflects the actual waiting customers.

## v4.8.8


- **Click-through focus fixed:** opening a chat from the awaiting list now matches the sidebar row by its **`data-id` (JID)** rather than the visible title — so chats whose internal id never appears in the title text (especially WhatsApp `@lid` privacy ids) focus correctly instead of failing with "could not focus the requested chat".
- **Honest contact labels:** only real phone ids (`@c.us`) render as a `+number`; WhatsApp privacy ids (`@lid`) show "Unsaved contact" instead of a fake 15-digit "number".

## v4.8.7


- **Readable names in the awaiting list:** unsaved WhatsApp contacts (which the chat store titles generically as "New message") now show the **phone number derived from the chat JID** (e.g. "+92332…"), so every waiting customer is identifiable.

## v4.8.6


- **Awaiting-reply list is now an inline accordion** under each account card (not a popup): expand a row to reveal the waiting customers (name + unread, worst-first); click one to open that chat. Header click no longer navigates away, and expanded rows stay open across the auto-refresh.

## v4.8.5


- **"Awaiting reply" is now click-through:** clicking the count opens a flyout listing the actual customers waiting (name + unread count, worst-first), scoped to the date window and aggregated across a location's accounts. Click any entry to jump straight into that WhatsApp conversation. The snapshot now keeps each chat's JID + name for this.

## v4.8.4


- **Exact "N awaiting reply" count** next to each account's caught-up % — the number of chats with unread customer messages (not yet responded to) within the selected date window. Replaces the stale thread-based urgent/dropped columns with the actionable number that matches the metric.

## v4.8.3


- **The date filter now works on the caught-up metric.** The chat-store snapshot keeps each chat's last-activity time, so Today / Last 7 days / All time scope the % to conversations *active in that window* (e.g. "of the chats active today, how many are caught up"). An account with no chats active in the window reads "no activity" rather than a stale number.

## v4.8.2


- **Cleaner command center:** removed the now-inert date-window selector (caught-up % is a live signal, so the window didn't change it) and relabeled the headline to "caught up".
- **Resilient first-load probe:** the IndexedDB scan now self-settles via a watchdog and the Re-sync probe retries, so an account whose WhatsApp Web is still loading no longer shows a hard timeout — it resolves on a later pass.

## v4.8.1


- **Trustworthy on-time = "caught up %":** the command center now derives each account's headline number from **WhatsApp's own unread marker**, read directly from the chat store in local IndexedDB (chats with no unread customer message = caught up). This needs no message history and no fragile name matching, so it reflects reality even when the app's reconstructed thread list is stale — fixing the misleading near-0% readings.
- **Why:** WhatsApp Web (multi-device) keeps only a small recent cache in the `message` store and does not persist per-chat `lastMessage`, but `unreadCount` is reliable for every chat. Reading the `chat` store with a single bounded `getAll` also avoids the long-cursor read-transaction hang that the `message` store caused.
- **Manual "Re-sync history"** button refreshes the snapshot on demand; the regular startup backfill keeps it current. Also fixes a WebView2 plumbing bug (ExecuteScriptAsync does not await promises) that made the IndexedDB read silently return nothing.

## v4.8.0


- **Command center is now the default landing tab**, with auto-refresh (20s) and per-row 7-day activity sparklines.
- **Date-windowed on-time** (Today / Last 7 days / All time, default Today): responsiveness is measured over conversations active in the window — including messages that arrived before the account was connected today — while older open conversations are surfaced as carried backlog ("from history") instead of saturating the number.
- **Per-account location rollup:** By-location groups each account into exactly one location (no more split accounts) and never leaks a raw branch id / instance GUID as a location name.
- **Robust backfill from IndexedDB:** history is read straight from WhatsApp Web's local `model-storage` (stable chat JIDs for every conversation, no DOM walking), replacing the bounded 3-chat DOM scroll. **Reconciliation** migrates legacy title-keyed threads to their stable JID and marks conversations whose last message is from you as **answered**, so on-time reflects what was actually replied. `OversightWindow` + `ReconcileConversationKey` (new unit tests).

## v4.7.0


- **Oversight redesign foundation (master plan Phase 1):** a new **Command center** dashboard tab showing per-account / per-location health (on-time %, urgent, dropped, freshness) sorted worst-first, with a needs-attention banner, By-account↔By-location toggle, and collapsible location accordions revealing member accounts. **Workspace Management** (`Ctrl+K → Manage workspaces`) sets per-location SLA targets + business hours (the SLA clock pauses outside working hours). **Drill-down:** click an account row to open its WhatsApp view. Backed by `OversightRollupBuilder` + `OversightService` (11 new unit tests). See `docs/MASTER-PLAN.md`.

## v4.6.0


- **P1 UX pass (UI/UX audit):** KPI strip now sits **above** the date-range/volume card (work surfaces sooner); the empty volume panel shows a **"Sync message history" CTA** instead of a dead end; the Live/Historical control has an explicit **"View mode"** label; thread cards add **non-color SLA glyphs** (⚠ breached / ⏱ approaching) for WCAG 1.4.1; and the card action reads **"Open chat →"**. All five P1 items shipped and verified.

## v4.5.0


- **SLA metric integrity (UI/UX audit P0-3):** Backfilled/historical threads are no longer counted as SLA breaches — the SLA clock applies only to threads observed live after connect. Added an **at-risk** warning window (≥50% of the threshold) and a **carried-over-from-history** count, so the OCC headline numbers reflect the real live workload instead of reading "all open exceed SLA". Decision recorded: the app stays on WhatsApp Web and **does not use Meta/WhatsApp APIs**.
- **CI asset guard:** the build now fails if runtime assets (`Assets\AppIcon.ico`, branding) are missing from output, preventing the class of bug behind the v4.4.0 tray crash.
- **Empty-state copy:** clearer, directional guidance on the message-volume panel.
- See `docs/ui-ux-research-and-recommendations.md` for the full audit and the sequenced remainder.

## v4.4.0


- **Launch-stability hardening:** fixed three startup/early-runtime crashes — a filter-chip null-reference during OCC XAML load, a taskbar-pin WinRT call that is unavailable in unpackaged builds, and a fatal tray-icon load when bundled assets were missing.
- **Asset packaging fix:** `AppIcon.ico` and brand wordmark images are now copied to the publish/install output (`CopyToOutputDirectory`); the sidebar wordmark and tray icon render correctly.
- **Update integrity:** installer verification now performs full Authenticode policy validation via `WinVerifyTrust` (chain + trust, not signature-presence only), with an optional publisher pin and existing SHA-256 sidecar check.
- **Installer path fix:** `installer.iss` / `installer-arm64.iss` read publish output from `bin\<Platform>\Release\...`, preventing stale XBF/DLL packaging.

## v3.7.0


- **Settings-only Ollama:** Lite installer (~66 MB); no embedded Ollama zip. Runtime downloads on Settings › AI enable with size disclosure and progress UI.
- **Wave 0 UX honesty:** Thread cards show heuristic previews and source badges (Heuristic / AI / Analyzing…) instead of misleading "Awaiting AI" copy.
- **Local Ollama AI:** Settings › AI section (enable toggle, download runtime, endpoint, model picker, test connection, pull progress); optional OCC header chip (AI ready / AI offline).
- **Inference pipeline:** Heuristic-first triage with bounded AI enrichment for top urgent live threads via OllamaSharp (gated until runtime is running).

## v3.4.0


- **WhatsApp startup backfill (P0?P3):** Re-wired `BackfillSyncManager` + `WhatsAppBackfillProvider` after connect; IndexedDB candidate collection with unread/recent/all modes; conversation+day dedupe store; triage enqueue + `RecordBackfillInbound` + thread registry timestamps.
- **P1 metadata:** Message-store daily sent/received aggregates (no decryption); sidebar snapshot ingress.
- **P2 scroll-back:** Open-chat history chunk collection; OCC backfill status caption (`UmBrandTealDarkBrush`).
- **P3 deep backfill (MVP):** Opt-in bounded sidebar walk (max 3 chats); full async automation deferred.
- **Settings:** Startup backfill toggle, mode, max chats, recent window, deep backfill opt-in.

## v3.3.0


- **Phase 10+ audit completion:** Personal Overview binds ViewModel `ObservableCollection`s directly (list virtualization restored); high-contrast theme support via system detection; cross-column kanban drag updates thread status and persists to `triage_v2.json`.
- **OCC UI polish:** Metric cards, thread cards, kanban, message-volume chart, and workspace sidebar token pass (uncommitted polish merged).

## v3.2.1


- **Startup fix:** Adapter script preload before WebView2 COM calls during `WarmAll` startup (cross-thread registration); rebuild installers for installed users.

## v3.2.0


- **Ultimate audit remediation:** Persist triage, thread registry, and kanban display order to `triage_v2.json`; doc reconciliation; OCC keyboard reorder (`Alt+Up/Down`, `Escape`).
- **Dead code removal:** Global hotkey service, legacy multi-platform connection handshake profiles, unused `AwaitingLocalAi` enum.
- **UX & ops:** Command palette thread search, first-run Personal vs Professional onboarding, HTTPS-only WebView navigation, default startup warm mode `VisibleOnly`.
- **Tests:** Triage persistence round-trip + kanban keyboard reorder unit tests.

## v3.1.5


- **UI hyper-loop polish:** Design-token pass across Operations Command Center, Personal Overview, kanban, message-volume chart, metric/thread cards, and workspace sidebar; shared scroll-offset preservation for list refresh stability.
- **Token cleanup:** Command palette modal scrim, notification feed typography, and sidebar compact padding wired to theme tokens.

## v3.1.4


- **Hyper-loop audit fixes:** Stop WhatsApp telemetry from double-counting analytics, ignore orphan branch keys in OCC pills, guard OCC date-range picker races and unload leaks, reuse message-volume chart geometries, and clear telemetry timers on adapter dispose.
- **Tests:** Two regression tests for branch-key collection and telemetry analytics isolation.

## v3.1.3


- **Full branding refresh:** Gradient app icon plus UNIFIED MESSENGER wordmark on About and sidebar; brand blue accent tokens (#1B75BB?#2E3191).
- **Audit fixes:** Removed dead copilot hotkey registration, fixed CI benchmark gate, refreshed UiSmoke OCC probes.

## v3.1.2


- **Updated branding:** Gradient four-bubble app icon applied across shell, tray, toasts, About page, and installers.

## v3.1.1


- **Startup fix:** Light/Dark theme no longer crashes launch when applied before the main window is created.

## v3.1.0


- **Dashboard overhaul:** OCC date-range filtering, message-volume trend chart, deeper WhatsApp telemetry ingress.
- **Sidebar UX:** Compact status labels, WhatsApp-focused instance list, improved truncation and tooltips.
- **Workspace purge:** Removed legacy multi-platform adapters, Ollama/AI stack, and obsolete tests/docs.
- **534** unit tests (x64, Release); trimmed UiSmoke harness (sidebar, OCC, Personal, settings, notifications).
