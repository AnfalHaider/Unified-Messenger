# Google Business Profile API: the checklist

Decided with the owner on 2026-09-19. Google blocks sign-in inside the app's own pages ("Couldn't sign you in. This
browser or app may not be secure"), so reviews will come from Google's official Business Profile API instead of
the reviews page. It costs nothing: Google lists quotas for these APIs, not prices, and no billing account is
needed. **We do this together at the end, after the rest of the roadmap and the first v6 update to v5 users.**
The owner signs in to Google Cloud; Claude does the configuring, asking before each step that changes anything.

## What the owner needs ready

- [ ] Signed in to **Google Cloud Console** with the Google account that owns project **`unified-messenger-5549a`**.
- [ ] That account, or one we add, is an **owner or manager of the three Business Profiles** (DHA-2, F-11, Men DHA-2),
      and the profiles are **verified**.
- [x] A **website** for the product: built in 6.6 (`v6/site/`), **live since 2026-09-20** on Firebase Hosting at
      `https://unified-messenger-5549a.web.app` — home (`/`) and privacy policy (`/privacy`). These are the two
      addresses the consent screen asks for.
- [ ] An **email address on the same domain** as that website. Google's access form checks that the two match, and a
      `web.app` address cannot have email, so this needs a domain the owner buys (and then a custom domain on the same
      free hosting). Not needed for the consent screen in Testing, only for the Business Profile API access form.
- [ ] About thirty minutes for the setup, then waiting for Google's approval email (usually days to a couple of
      weeks).

## Done in the console, 2026-09-20/21 (owner signed in, Claude driving the browser)

- [x] **Both APIs enabled** on `unified-messenger-5549a`: My Business Account Management, and My Business
      Business Information. (The reviews API, "Google My Business API" v4, only becomes enable-able after approval.)
- [x] **Consent screen (Branding) saved**: app name *Unified Messenger*, support email and developer contact
      `anfalhaider@gmail.com`, home page `https://unified-messenger-5549a.web.app/`, privacy policy
      `https://unified-messenger-5549a.web.app/privacy`, authorised domains `unified-messenger-5549a.web.app` and
      `unified-messenger-5549a.firebaseapp.com`. Read back after a reload.
- [x] **Publishing status left as Firebase made it: External, In production.** This checklist used to say keep it
      in Testing; that was written before workspaces existed. Testing admits only a list of test users, which would
      stop a customer signing in to their own workspace. In production with an unverified restricted scope means the
      `business.manage` consent shows an "unverified app" warning and counts against a 100-user cap until Google
      approves - fine for the owner's own profiles, and verification comes with the access application anyway.
- [ ] **Scopes not yet listed on the consent screen** (Data Access: `openid`, `userinfo.email`, `userinfo.profile`,
      `business.manage`). Tried three times; the browser pane kept losing its rendering while the app window was in
      the background, and the dialog's Save went with it. Nothing depends on this today - the app asks for its
      scopes at sign-in - but Google's verification submission wants them listed. Two minutes by hand: Data Access,
      Add or remove scopes, tick the three basic ones, paste
      `https://www.googleapis.com/auth/business.manage` into "manually paste scopes", Add to table, Update, Save.
- [ ] **Not applied for yet:** the Business Profile API access form itself, which still wants an email address on
      the website's own domain (see above).

## What is already built

The reader is done and tested (3.1b, 2026-09-20): `v6/core/google-api.ts` and `v6/app/google-api.ts`, switched off
behind `settings.googleApi.enabled`. Once Google approves, switching it on and adding a Connect button to the Google
account's screen is all that is left — nothing else in the app changes, because the API's reviews become the same
cards the page reader already produces.

## What Claude configures, with the owner's approval at each step

1. **Enable the APIs** in the project: My Business Account Management API and My Business Business Information API.
   (The reviews API, "Google My Business API" v4, becomes available to enable only after approval.)
2. **OAuth consent screen** (Google Auth Platform): app name *Unified Messenger*, support email, logo, homepage,
   privacy policy, authorised domain. Audience **External**, left in **Testing** with the owner's Google accounts
   as test users. Testing needs no Google review and allows up to 100 test users, enough for the owner's own
   profiles from day one.
3. **Scopes**: `https://www.googleapis.com/auth/business.manage` (Google offers no read-only scope for these APIs;
   its consent text reads "Manage your Business Profile on Google", and the app description must say plainly that
   the app only reads) plus `openid` and `email`, so the app can show which Google account is connected.
4. **Credentials**: one OAuth client of type **Desktop app**. Its id goes into the app's build as a local file that
   is never committed (same rule as `firebase-config.json` and `oauth-client.json`). A desktop client's secret is not
   confidential by Google's own definition; sign-in uses PKCE and a loopback redirect.
5. **Access request**: the Business Profile API contact form (`support.google.com/business/contact/api_default`,
   "Application for Basic API Access"): company name, website, contact email on that domain, **project number**,
   and a use-case description. Claude drafts the description: *a desktop app that reads a business's own reviews,
   ratings and reply status for its own locations, on the owner's PC; it posts nothing and stores nothing off the
   PC.*
6. **Confirm approval**: in the project's quotas, the Business Profile APIs change from **0** to **300 requests per
   minute** once approved. Then enable the v4 API.

## What the app does (built before then, switched off until approval)

- **Connect Google** on each Google Business account opens the owner's normal browser (Google's approved way for a
  desktop app, so the "couldn't sign you in" block does not apply), and Google hands back a token.
- The token is kept **encrypted on this PC** (Electron `safeStorage`) and never leaves it. Calls go straight from the
  PC to Google: no server of ours, no oversight data anywhere else.
- Every half hour it reads each location's reviews through `accounts.locations.reviews.list` (every review, not just
  the latest 50, with star ratings, reply status, the average rating and the total), into the same `core/reviews.ts`
  records and the same Reviews screen.
- Quota: 300 requests a minute, shared by every customer of the product. A location read every half hour uses a small
  fraction of one; roughly a few thousand locations fit before asking Google for more, which is also free.

## Before other businesses use it (later)

- [ ] Move the consent screen from Testing to **In production** and submit it for Google's review: homepage, privacy
      policy, the authorised domain verified in Search Console, and possibly a short video of the sign-in. Claude
      confirms at that point whether `business.manage` needs only the free brand review or the paid security
      assessment Google reserves for "restricted" scopes (it is believed to be the former).
- [ ] Ship the update that switches the API reader on.
