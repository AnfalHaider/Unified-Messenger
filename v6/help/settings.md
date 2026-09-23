# Settings

Everything that changes how the app counts, reads and tells you things. Changes are saved as you make them.

## Look and reading

![Look and reading](shot:settings-look-and-reading)

- **Appearance**: match Windows, light or dark. Also in the title bar.
- **How see-through the window is**: from solid to 40% see-through, in steps. It takes the whole window, the
  account pages with it, and changes as you pick it — no restart. The figures get harder to read the further you
  go, so the app stops well before invisible.
- **Reply target**: how quickly a customer should hear back. Everything red is measured against this.
- **Read each account every**: how often each page is read. Reading is quiet and never opens a chat.
- **Chats each WhatsApp read takes in**: how far back a read looks, from 100 chats to 2,000. Higher sees older
  conversations and costs a little more time on every pass. Instagram has no such setting: its page holds only
  the top threads of Primary, so that number is Instagram's, not the app's.
- **Backlog after**: a customer waiting longer than this leaves the line and is counted as backlog in Reports.
- **Leave out chats that ended themselves**: a last message like "ok thanks" is not someone waiting. Those chats are listed in [Set aside](help:set-aside).
- **Not customers**: words that mark staff names ("Staff", "Team", "Supplier") and the team's own numbers. Chats they match are never counted, anywhere. A word must stand on its own: "Staff" catches "Bilal Staff" but not "Staffordshire". Numbers can be typed any way: +92 300…, 0300….
- **When I close the window**: keep reading in the background from the tray, or quit.
- **Memory**: put accounts you are not using to sleep, and after how long.

## Opening hours

![Opening hours](shot:settings-opening-hours)

Each location's hours, day by day, and the days you are closed. While a location's hours are switched on, waits there count only while it is open, so a message that arrives at night does not look ten hours late in the morning.

## Notifications

![Notifications](shot:settings-notifications)

- **Tell me when**: a customer is about to pass the target, someone has waited over an hour, an account needs
  signing in, a missed call has not been returned, an account has stopped being read, or a one- or two-star
  review arrives.
- **Quiet hours**: no notifications between these times.
- **Summaries**: the morning digest, and saving last week's report by itself on Mondays.

## Saved replies

![Saved replies](shot:settings-saved-replies)

Sentences you type often, with a short name. They appear beside every chat with a **Copy** button. The app never sends them; you paste them into the chat yourself.

## Assistant, Workspace, Privacy, About

- **Assistant** switches the local assistant on or off and downloads what it needs, only when you press the button. See [Assistant](help:assistant).
- **Workspace** › **Your sign-in**: **Sign in with Google** opens your browser, where you choose your Google account; the app comes back by itself when you are done. You stay signed in until you press **Sign out**. Once signed in, **Your workspace** appears:
  - **No workspace yet**: give it the business's name and press **Start the workspace**. This PC's accounts, locations, opening hours, holidays, reply target, saved replies and not-a-customer rules are kept in the workspace. Logins, messages, customers and figures never leave this PC.
  - **A second PC** signed in with the same Google account gets that setup by itself. Its accounts arrive without their logins, so each shows **Sign in needed** until you sign in to it on that PC.
  - **Changes**: an admin's changes reach the workspace within seconds; other PCs pick them up when they start, every six hours, or when you press **Sync now**. If two PCs change the setup at the same moment, the first change stands and the second PC says so.
  - **What stays on each PC**: its theme, notifications, quiet hours, the assistant, the digest and which accounts are muted there.
  - **Invited?** When someone has invited your Google address, **Your workspace** says which workspace and offers **Join**. Joining brings its setup; accounts already on your PC stay.
- **Members of the workspace** lists who is in it and when each person's PC last checked in. Admins can:
  - **Invite someone** by their Google address, as a member or an admin. The app sends no email: tell them to open Unified Messenger and sign in with that address.
  - **Make admin** or **Make member**. Members see the shared setup and cannot change it; admins can change it and manage members.
  - **Remove** someone. Their PCs sign out and wipe the logins they had from the workspace at their next check (when the app starts, and every six hours while it runs), and say so on screen. Accounts that were only ever on their PC stay. To cut access at once, also remove their PC on the phone: WhatsApp › **Linked devices**.
  - **Restore** someone removed, and **Withdraw** an invitation not yet taken up.
- **If the workspace is paused**, every PC in it shows a screen saying so and nothing else, until it is active again. Nothing on those PCs is deleted. See [Owner console](help:owner).
- **A week offline:** a PC that has not reached the workspace for seven days asks to reconnect before it shows anything, so someone removed cannot keep reading by staying offline. Nothing is deleted; it carries on as soon as it reaches the workspace.
- **Workspace** is where you sign in, join a workspace you were invited to, and see who else is in it. The app
  asks you to sign in with Google before it opens: it reads nothing and opens no account page until the address
  you signed in with has been invited to a workspace. If it has not, the app says so and names the address, in
  case it was the wrong Google account.
- **Privacy** lists what is kept on this PC, measured from disk when you open it: the account logins, what the
  app recorded, your notes and saved replies, the reviews last read, the support log and the assistant's model,
  with where each one is managed. Anything that could not be measured says so rather than showing nothing.
- **About** shows the version you are running and looks for newer ones. **Check for updates** asks GitHub for the
  latest release; the app also asks by itself a couple of minutes after it starts and every six hours. Nothing is
  downloaded until you press **Download it**, and nothing is installed until you press **Install and restart**, because
  installing closes the app and every account page with it. Putting it off leaves the update downloaded and ready.
  Your logins, figures and waiting customers are all kept across an update.
