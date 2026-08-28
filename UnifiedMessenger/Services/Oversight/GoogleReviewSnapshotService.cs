using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.UI.Dispatching;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Holds per-account Google Business review health, scraped from the live <c>business.google.com/reviews</c>
/// page. Reliable signals only: a review shows a <b>Reply</b> button when unanswered and an <b>Edit</b>
/// button once the owner has responded — so unanswered = Reply count, answered = Edit count. Google does not
/// expose an aggregate rating or total review count on this manager page (per-review stars are SVG-only), so
/// those are intentionally not scraped. Counts reflect the currently-loaded reviews page (Google paginates).
/// </summary>
public sealed class GoogleReviewSnapshotService
{
    /// <summary>A best-effort preview of one review still awaiting a reply (reviewer + snippet from the card DOM).</summary>
    /// <summary><paramref name="Index"/> is the review's position in the page's Reply-button order — the
    /// fallback for click-through when the reviewer name doesn't match back (Google renders it inconsistently).</summary>
    public readonly record struct PendingReview(string Reviewer, string Text, int Stars, string Age, int Index);

    /// <summary>
    /// The profile's OFFICIAL Google rating and lifetime review count (e.g. 4.6 / 239) — verified live on the
    /// Google Search merchant view. The reviews manager carries neither, so this is scraped separately.
    /// </summary>
    public readonly record struct ProfileRating(string Rating, int? Total, DateTimeOffset CapturedAtUtc);

    public readonly record struct ReviewHealth(
        int Unanswered,
        int Answered,
        DateTimeOffset CapturedAtUtc,
        bool HasData,
        IReadOnlyList<PendingReview> Pending,
        int PagesRead = 1,
        // True when the traversal clicked through to a page whose Next button was disabled — i.e. we
        // reached the end and these totals are the WHOLE profile, not a window onto it. This is a stronger
        // statement than comparing against the separately-scraped lifetime total, because it is a fact
        // about what this scrape actually did rather than an inference from two numbers of different ages.
        bool ReachedLastPage = false)
    {
        public int Total => Unanswered + Answered;

        /// <summary>
        /// Share of loaded reviews that have a reply. 100 means none outstanding; 0 means none replied to.
        /// </summary>
        /// <remarks>
        /// The <c>Total &gt; 0</c> guard is load-bearing and must stay: <see cref="MetricMath.HonestPercent"/>
        /// returns 100 for a zero total — correct for "nothing outstanding" elsewhere, but here it would
        /// tell a business with no reviews that it had replied to 100% of them.
        /// </remarks>
        public int ReplyRatePercent => Total > 0 ? MetricMath.HonestPercent(Answered, Total) : 0;
    }

    // Page helpers shared by the counting scrape and the focus click-through. Re-installed on every call —
    // idempotent, and the page may have reloaded since the last one.
    private const string PageHelpers =
        // Bump "Rows per page" to its max once, so the counts cover more than the default 10. Returns true if
        // it just kicked one off (the caller should wait and re-poll).
        // ponytail: synthetic .click() drives Google's jsaction listbox (opener jsname=LgbsSe, options carry
        // data-value); if a Google build ignores it this simply no-ops and we count the default page — no
        // regression. Upgrade path if it stops working: dispatch a real MouseEvent instead of .click().
        // The done-flag is set only AFTER the control is found. It used to be set on entry, which meant a
        // scrape that ran before Google had rendered the rows-per-page control burned the flag on a failed
        // attempt and never retried for the life of that page — leaving the account pinned to Google's
        // default of 10 rows. Observed live: Google Men DHA-2 reported 10 reviews pass after pass while its
        // siblings reported 50, which looked like a smaller profile and was actually this.
        "window.__umGRBumpRows=function(){if(window.__umGRrowsDone)return false;try{" +
        "var rb=document.querySelector('[aria-label=\"Number of rows per page\"]');if(!rb)return false;" +
        "window.__umGRrowsDone=1;" +
        "var op=rb.querySelector('[jsname=\"LgbsSe\"]');if(op)op.click();" +
        "setTimeout(function(){try{var o=[].slice.call(rb.querySelectorAll('[data-value]'));" +
        "var m=o.reduce(function(a,c){return (+(c.getAttribute('data-value')||0))>(+(a.getAttribute('data-value')||0))?c:a;},o[0]);" +
        "if(m)m.click();}catch(e){}},250);return true;}catch(e){return false;}};" +
        "window.__umGRButtons=function(re){return [].slice.call(document.querySelectorAll('button'))" +
        ".filter(function(x){return re.test((x.innerText||'').trim());});};" +
        // A review's card = the LARGEST ancestor of its Reply/Edit button that still holds only that one
        // action button; climb one more and you're in the list container holding every review.
        // The old heuristic took the smallest ancestor with 25–700 chars, which truncated long reviews (a
        // >700-char review matched no ancestor at all and read as empty) — hence full text was never available.
        "window.__umGRCard=function(btn){var n=btn.parentElement,best=null;" +
        "for(var i=0;i<10&&n;i++){var bs=n.querySelectorAll('button'),acts=0;" +
        "for(var j=0;j<bs.length;j++){if(/(^|\\b)(reply|edit)\\b/i.test((bs[j].innerText||'').trim()))acts++;}" +
        "if(acts>1)break;" +
        "if(((n.innerText||'').trim()).length>=25)best=n;" +
        "n=n.parentElement;}return best;};" +
        // Verified against a live pending card (DevTools dump, v4.83.0). Its innerText lines are:
        //   0 "Depilex DHA-2 Islamabad"            <- location header: name…
        //   1 "Jinnah Boulevard, Islamabad"        <- …and address, on EVERY card
        //   2 "Anjum Afzal"                        <- the reviewer
        //   3 " 5 days ago"   <- stars AND age share one line
        //   4 "I had an excellent experience… More" <- review text, truncated by Google
        //   5 "reply"  6 "Reply"  7 "more_vert"    <- icon ligature + label + ⋮ menu
        // So the meta line is the anchor: the reviewer is the line directly above it (which drops the header
        // whatever its size), and the text is everything below it. The page carries NO rating aria-label —
        // its only aria-labels are "Open review options"/"Review options" — so the glyphs are the sole source.
        "window.__umGRPua=/[\\uE000-\\uF8FF]/g;" +
        "window.__umGRAgeRe=/^(a|an|\\d+)\\s+(second|minute|hour|day|week|month|year)s?\\s+ago$/i;" +
        // Stars are Material icon-font glyphs (filled star = U+E838), not text — five slots rendered
        // filled-first, so the rating is the leading run of the FIRST codepoint. Deriving it that way avoids
        // hard-coding which codepoint means filled vs outline (only the 5-star case was observable live).
        // THE RATING IS IN THE COLOUR, NOT THE GLYPH. Verified live over 19 pending reviews on DHA-2: every
        // review renders FIVE spans of the identical codepoint U+E838, and the rating is how many of them are
        // gold rgb(251,188,4) versus grey rgb(218,220,224). The previous reader counted the leading run of
        // the first CODEPOINT, which is five every time — so every review on the page was reported as 5
        // stars. Five unanswered ONE-star reviews were being shown to the owner as "★5 · Positive" and
        // ranked below praise, which is precisely backwards from what this surface is for.
        //
        // The leading run of the COLOUR is the rating: Google has no zero-star review, so the first star is
        // always filled, and the filled ones always come first. Comparing against the first span's own colour
        // rather than a hard-coded gold means a Google restyle changes nothing here.
        "window.__umGRStarsFromCard=function(card){try{" +
        "var els=[].slice.call(card.querySelectorAll('*')).filter(function(el){" +
        "return el.children.length===0&&/[\\uE000-\\uF8FF]/.test(el.textContent||'');});" +
        "if(els.length<5)return 0;" +
        "var cols=els.slice(0,5).map(function(el){return getComputedStyle(el).color;});" +
        "var first=cols[0],n=0;" +
        "for(var i=0;i<cols.length;i++){if(cols[i]===first)n++;else break;}" +
        "return (n>=1&&n<=5)?n:0;}catch(e){return 0;}};" +
        // Google truncates long reviews in the DOM ("…impressed by the... More"), so full text only exists
        // after its More expander is clicked. Scoped to the cards we actually read, once per page load.
        // ponytail: synthetic .click() on the leaf element whose exact text is "More" — same jsaction bet as
        // the rows-per-page bump. If a build ignores it, text simply stays truncated (we strip the "More").
        "window.__umGRExpand=function(btns){if(window.__umGRexpDone)return false;window.__umGRexpDone=1;var n=0;" +
        "try{for(var i=0;i<btns.length&&i<8;i++){var c=window.__umGRCard(btns[i]);if(!c)continue;" +
        "var els=c.querySelectorAll('*');" +
        "for(var j=0;j<els.length;j++){var e=els[j];" +
        "if(e.children.length===0&&/^(more|read more)$/i.test((e.textContent||'').trim())){try{e.click();n++;}catch(x){}}}}}" +
        "catch(x){}return n>0;};" +
        // Reads one pending review out of its card. Best-effort by nature (Google exposes no stable per-review
        // hooks) — each field degrades on its own, and the Reply/Edit counts stay the reliable signal.
        // ---- PAGINATION ----------------------------------------------------------------------------
        // Google caps rows-per-page at 50 (the control offers 10/25/50), so any profile with more than 50
        // reviews was being counted from its first page only. The Next button is found by its Material icon
        // ligature "navigate_next" FIRST and its English aria-label second: the ligature is the same in
        // every locale, so this keeps working on a Google rendered in Urdu.
        "window.__umGRNextBtn=function(){var all=[].slice.call(document.querySelectorAll('button,[role=\"button\"]'));" +
        "var byIcon=all.filter(function(e){return /(^|\b)navigate_next(\b|$)/.test((e.innerText||'').trim());});" +
        "if(byIcon.length)return byIcon[0];" +
        "var byAria=all.filter(function(e){return /^next$/i.test(e.getAttribute('aria-label')||'');});" +
        "return byAria.length?byAria[0]:null;};" +
        "window.__umGRHasNext=function(){var n=window.__umGRNextBtn();" +
        "return !!(n&&!n.disabled&&n.getAttribute('aria-disabled')!=='true');};" +
        // A page fingerprint, so the reader can tell "the next page has rendered" from "I am looking at the
        // same page again". Without it a fast poll counts the outgoing page twice and inflates every total.
        "window.__umGRFp=function(){var b=window.__umGRButtons(/(^|\b)(reply|edit)\b/i);" +
        "if(!b.length)return '';var c=window.__umGRCard(b[0]);" +
        "return c?((c.innerText||'').trim().slice(0,120)):'';};" +
        "window.__umGRNext=function(){var n=window.__umGRNextBtn();" +
        "if(!n||n.disabled||n.getAttribute('aria-disabled')==='true')return false;" +
        "window.__umGRprevFp=window.__umGRFp();" +
        // DO NOT reset __umGRexpDone here. It used to be reset so later pages could expand their truncated
        // reviews, and that silently broke the page-change guard: the fingerprint is the first card's text,
        // the expander REWRITES that text, so a page that had not actually advanced still produced a
        // different fingerprint. Every page counted itself again until the 40-page ceiling — 2,000 reviews
        // for a salon with roughly 239. Previews come from the early pages anyway, so leaving later pages
        // unexpanded costs nothing that matters.
        //
        // The effective preview cap is EIGHT, not the 24 this comment used to claim: the read below slices
        // replyBtns to the first 8, and MaxPages is 1, so the C# accumulator's 24 is never reached. That
        // gap is why the desk's "Unanswered" tile was a sample masquerading as a total — see
        // ReviewCoverage.QueueIsSample.

        "try{n.click();}catch(e){return false;}" +
        "window.__umGR={state:'loading'};return true;};" +
        "window.__umGRRead=function(btn,idx){var card=window.__umGRCard(btn);" +
        "var raw=(((card&&card.innerText)||'').split('\\n')).map(function(s){return s.trim();})" +
        ".filter(function(s){return s.length>0;});" +
        "var isAct=function(l){return /^(reply|edit|share|delete|report|more_vert|more|less|read more|show (more|less)|\\(owner\\))$/i.test(l);};" +
        "var mi=-1;for(var i=0;i<raw.length;i++){" +
        "if(window.__umGRAgeRe.test(raw[i].replace(window.__umGRPua,'').trim())){mi=i;break;}}" +
        "var stars=0,age='',name='',body='';" +
        // Stars come from the CARD, not the meta line: the line's glyphs are identical for every rating and
        // only their rendered colour differs. See __umGRStarsFromCard.
        "if(mi>=0){age=raw[mi].replace(window.__umGRPua,'').trim();stars=window.__umGRStarsFromCard(card);" +
        "name=mi>0?raw[mi-1]:'';" +
        "body=raw.slice(mi+1).filter(function(l){return !isAct(l);}).join(' ');}" +
        // No meta line (locale/layout drift): show the text but NO name — the first line is the location, and
        // a wrong reviewer name is worse than a generic one.
        "else{body=raw.filter(function(l){return !isAct(l);}).join(' ');}" +
        "body=body.replace(/\\s*(\\.\\.\\.|\\u2026)\\s*More$/i,'\\u2026').replace(window.__umGRPua,'').trim();" +
        // Google's own placeholder for a star-only review. It was being scraped as the review body and shown
        // in the queue as though the customer had written it, which reads as a real sentence from a real
        // person. A rating with no words is a fact worth showing, but it is not a quote.
        "if(/^the user didn'?t write a review/i.test(body))body='';" +
        "return {reviewer:(name||'Reviewer').slice(0,60),text:body.slice(0,1200),stars:stars,age:age,idx:idx};};";

    // Counts Reply (unanswered) vs Edit (answered) buttons on the reviews page; navigates there first if the
    // Google Business webview is on a different page. Idempotent — safe to run repeatedly while polling.
    private const string KickoffScript =
        "(function(){try{" + PageHelpers +
        // Only navigate to /reviews when explicitly allowed (a user-driven Re-sync). A background refresh
        // passes allowNavigate:false so it can never yank the owner off whatever Google page they're reading.
        "if(!/\\/reviews(\\/|$)/.test(location.pathname)){" +
        // Any google.com host, not just business.google.com. The rating scrape parks this very WebView on the
        // Search merchant view (www.google.com/search?…), which is the ONLY place the rating and lifetime
        // total exist — and a business.google.com-only test cannot navigate back from there, so the reviews
        // scrape that runs straight afterwards returned 'notreviews' and gave up. That made the manual
        // Re-sync path, the one the owner explicitly asked for, the one that failed to refresh review counts.
        "if(window.__umGRAllowNav&&/(^|\\.)google\\.com$/i.test(location.host)){if(!window.__umGRnav){window.__umGRnav=1;location.href='https://business.google.com/reviews';}window.__umGR={state:'navigating'};return;}" +
        "window.__umGR={state:'notreviews'};return;}" +
        "if(window.__umGRBumpRows()){window.__umGR={state:'loading'};return;}" +
        "var replyBtns=window.__umGRButtons(/(^|\\b)reply\\b/i);" +
        "var reply=replyBtns.length;" +
        "var edit=window.__umGRButtons(/\\bedit\\b/i).length;" +
        "if(reply+edit===0){window.__umGR={state:'loading'};return;}" +
        "if(window.__umGRExpand(replyBtns)){window.__umGR={state:'loading'};return;}" +
        // Still showing the page we just navigated away from — keep waiting rather than counting it twice.
        "if(window.__umGRprevFp&&window.__umGRFp()===window.__umGRprevFp){window.__umGR={state:'loading'};return;}" +
        "var pending=replyBtns.slice(0,8).map(function(btn,i){return window.__umGRRead(btn,i);});" +
        "window.__umGR={state:'done',unanswered:reply,answered:edit,pending:pending,hasNext:window.__umGRHasNext()};" +
        "}catch(e){window.__umGR={state:'error'};}})()";

    /// <summary>
    /// Clears the injected per-page state and reloads, so a pass always begins at page one.
    /// </summary>
    private const string ResetScript =
        "(function(){try{window.__umGRprevFp=null;window.__umGR=null;window.__umGRrowsDone=0;" +
        "window.__umGRexpDone=0;if(/\\/reviews(\\/|$)/.test(location.pathname)){location.reload();}" +
        "return true;}catch(e){return false;}})()";

    private const string ReadScript = "(window.__umGR?JSON.stringify(window.__umGR):'{\"state\":\"none\"}')";

    // Scrapes the profile's official rating + lifetime review count. These live ONLY on the Google Search
    // merchant view — business.google.com/reviews has neither. Verified live on that page:
    //   • rating  → an aria-label reading exactly "Rated 4.6 out of 5,"  (cleanest, locale-stable-ish source)
    //   • total   → body text "435 Google reviews", OR a bracketed "4.6 ★ (991)" with no label at all
    // NOTE: innerText renders them CONCATENATED ("4.6239 Google reviews"), which is why a \b-anchored number
    // regex finds nothing — both numbers have to be pulled out of one match. The rating captured beside the
    // count is the one we keep; the aria-label is only a fallback (see the PRECEDENCE note below).
    // business.google.com/ (root) redirects a single-location profile to that view, so we use Google's own
    // redirect instead of guessing a search URL. Navigation is allowed on the first attempt only.
    private const string RatingKickoff =
        "(function(){try{" +
        // The aria-label rating is a FALLBACK, not the primary source — see the precedence note below.
        "var a=[].slice.call(document.querySelectorAll('[aria-label]'));var ar=null;" +
        "for(var i=0;i<a.length;i++){var m=/Rated\\s+([0-5][.,]\\d)\\s+out\\s+of\\s+5/i.exec(a[i].getAttribute('aria-label')||'');" +
        "if(m){ar=m[1].replace(',','.');break;}}" +
        "var t=(document.body&&document.body.innerText)||'';" +
        "var paired=null;" +
        // innerText renders the rating and count RUN TOGETHER ("4.6239 Google reviews"), so a bare ([\d,]+)
        // before "Google reviews" swallows the rating's decimal digit -> 6239 instead of 239 (and "4.81,234"
        // -> 81234). Anchor on the rating so the two split correctly; the [^\d] run also tolerates a layout
        // that separates them ("4.6 ★ 239 Google reviews").
        // The run is 12 rather than 6 because Google renders FIVE star glyphs on some profiles
        // ("4.7 ★★★★☆ 435 Google reviews" — seven characters between the numbers). At 6 this failed to match,
        // which cost the paired rating and fell back to the aria-label; live, that reported 3.0 for a 4.7
        // profile. Widening is safe: the run is [^\d], so it can never step over another number to pair up
        // two figures that don't belong together.
        "var c=/([0-5][.,]\\d)[^\\d]{0,12}([\\d,]+)\\s+Google\\s+reviews/i.exec(t);" +
        "var tot=null;if(c){tot=c[2].replace(/,/g,'');paired=c[1].replace(',','.');}" +
        // PARENTHESISED LAYOUT — "4.6 ★ (991) · Beauty salon". Google renders some profiles this way, with the
        // count in brackets and the words "Google reviews" appearing NOWHERE on the page, so every pattern
        // above misses it and the profile reports no lifetime total at all. Found from the owner's own
        // screenshots of their three locations: two render this way and only the third was parsing, which is
        // why the coverage line could never say "of 991" for them. Anchored on the rating for the same reason
        // as above — a bare \((\d+)\) would match any other bracketed number on the page.
        "if(!tot){var c3=/([0-5][.,]\\d)[^\\d(]{0,12}\\((\\d[\\d,]*)\\)/.exec(t);" +
        "if(c3){tot=c3[2].replace(/,/g,'');paired=c3[1].replace(',','.');}}" +
        // Fallback for a layout with no rating next to the count: require a non-digit/dot before it so we
        // still can't slice a number out of the middle of another one.
        "if(!tot){var c2=/(?:^|[^\\d.,])([\\d,]{1,7})\\s+Google\\s+reviews/i.exec(t);tot=c2?c2[1].replace(/,/g,''):null;}" +
        // PRECEDENCE: the rating that sits NEXT TO the review count wins over the aria-label.
        // The aria-label search takes the first "Rated X out of 5" anywhere in the document, and the merchant
        // view carries several — individual reviews have their own star labels, and a related-businesses
        // panel lists other branches with theirs. Measured live against the owner's three profiles, the
        // aria-label was wrong on two of three: the DHA-2 profile (truly 4.6) reported 4.7, which is the
        // rating of a DIFFERENT Depilex branch on the same page, and the Men profile (truly 4.7) reported
        // 3.0 from a single review. Both totals were right, because a total is only ever accepted with its
        // own rating beside it in the same run of text — which is exactly the property that makes the paired
        // rating trustworthy and the free-floating one not. A wrong star rating on a salon's own dashboard is
        // the kind of number an owner would check against Google and then stop believing the whole app.
        "var r=paired||ar;" +
        "if(r||tot){window.__umGRate={state:'done',rating:r,total:tot};return;}" +
        "if(window.__umGRateAllowNav){location.href='https://business.google.com/';window.__umGRate={state:'navigating'};return;}" +
        "window.__umGRate={state:'loading'};" +
        "}catch(e){window.__umGRate={state:'error'};}})()";

    private const string RatingReadScript = "(window.__umGRate?JSON.stringify(window.__umGRate):'{\"state\":\"none\"}')";

    /// <summary>
    /// Scrolls the owner straight to one specific pending review and outlines it. Google's review manager has
    /// no per-review URL — reviews simply aren't individually addressable — so "open the exact review" means
    /// finding its card on the page rather than deep-linking to it. Navigates to /reviews first if the webview
    /// is elsewhere (e.g. left on the merchant view by a rating scrape). Returns false until the list renders.
    /// </summary>
    private static string BuildFocusScript(string reviewer, int index) =>
        "(function(){try{" + PageHelpers +
        "if(!/\\/reviews(\\/|$)/.test(location.pathname)){" +
        "if(/business\\.google\\.com/.test(location.host)){if(!window.__umGRFnav){window.__umGRFnav=1;location.href='https://business.google.com/reviews';}}" +
        "return false;}" +
        // A fresh page load resets to 10 rows, so a pending review further down would be unreachable.
        "window.__umGRBumpRows();" +
        "var want=" + JsonSerializer.Serialize(reviewer ?? string.Empty) + ";var idx=" + JsonSerializer.Serialize(index) + ";" +
        "var btns=window.__umGRButtons(/(^|\\b)reply\\b/i);if(!btns.length)return false;" +
        "var wl=want.toLowerCase(),target=null;" +
        "if(wl){for(var i=0;i<btns.length;i++){var c=window.__umGRCard(btns[i]);" +
        "if(c&&(c.innerText||'').toLowerCase().indexOf(wl)>=0){target=c;break;}}}" +
        // Name didn't match back — fall back to the same position in the Reply-button order, which is exactly
        // the order the scrape read the pending list in.
        "if(!target&&idx>=0&&idx<btns.length)target=window.__umGRCard(btns[idx]);" +
        "if(!target)return false;" +
        "target.scrollIntoView({block:'center'});" +
        "try{var o=target.style.outline;target.style.outline='3px solid #1a73e8';target.style.outlineOffset='2px';" +
        "setTimeout(function(){try{target.style.outline=o||'';}catch(e){}},5000);}catch(e){}" +
        "return true;}catch(e){return false;}})()";

    /// <summary>
    /// Scrolls to and highlights one pending review on the account's reviews page. Call after opening the
    /// instance. Best-effort: returns false if the page never renders the list (the account still opens).
    /// </summary>
    public async Task<bool> FocusReviewAsync(string instanceId, string reviewer, int index)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        var id = instanceId.Trim();
        var script = BuildFocusScript(reviewer, index);
        var connection = InstanceConnection.Current;

        // ~12s of attempts: the account may have just been opened cold, or be sitting on the merchant view
        // from a rating scrape — so the early attempts navigate and the list renders a few seconds later.
        // Mirrors ConversationFocusHelper's retry window, for the same reason.
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                var raw = await connection.ExecuteScriptAsync(id, script).ConfigureAwait(true);
                if (ConversationFocusHelper.ParseScriptBoolean(raw))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Gives up on this account for this pass, and used to do so without a word. The Reviews
                // card then simply kept reporting nothing read yet, which is indistinguishable from a slow
                // first pass — the exact ambiguity that hid the background refresh never arming at all.
                AppLogger.LogWarning(
                    $"GoogleReviews.{id}",
                    $"Could not reach the reviews page: {ex.GetType().Name}.");
                return false;
            }

            await Task.Delay(600).ConfigureAwait(true);
        }

        return false;
    }

    private static readonly Lazy<GoogleReviewSnapshotService> LazyInstance = new(() => new GoogleReviewSnapshotService());

    public static GoogleReviewSnapshotService Instance => LazyInstance.Value;

    /// <summary>
    /// One scrape per account at a time.
    /// </summary>
    /// <remarks>
    /// <b>Why this is not optional.</b> A traversal CLICKS THROUGH the account's live WebView, so two of
    /// them on one account do not merely duplicate work — they corrupt each other, each counting whatever
    /// page the other just advanced to. Two callers exist by design (this service's background pass and
    /// the desk's own refresh) and they collided the moment paging was introduced: the log showed
    /// the same account finishing twice in the same second with different answers — "Read 250 across 5
    /// pages" beside "Read 600 across 12 pages", and 197 unanswered beside 167.
    ///
    /// <para>
    /// A caller that arrives while a scrape is running gets the cached value rather than queueing. Waiting
    /// would just mean two full traversals back to back for a number that changes a few times a week.
    /// </para>
    /// </remarks>
    /// <summary>
    /// How long to let a session prove it can run scripts before treating it as asleep.
    /// </summary>
    /// <remarks>
    /// Waking a WebView is asynchronous: <c>EnsureSessionAsync</c> and <c>TryResumeSessionAsync</c> both
    /// return before the page can actually execute anything. Measured live — all three accounts failed a
    /// single immediate probe at 14:00:23, and all three were running scripts by 14:00:36. A one-shot check
    /// therefore reported a session that was waking up perfectly normally as asleep, and skipped it.
    ///
    /// <para>
    /// 15 seconds because that measured gap was 13, and a budget under the number it was chosen from would
    /// fail the exact case it exists for. Still a quarter of the 60-second poll it replaced, so the waste
    /// this was added to remove stays removed.
    /// </para>
    /// <para>
    /// Settable so tests can shorten it — a suite that really waited this out would add 15 seconds per case
    /// to a run that currently takes 23 for everything.
    /// </para>
    /// </remarks>
    internal static TimeSpan ScriptReadyBudget { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Whether the account's session can execute JavaScript, waiting briefly for one that is still waking.
    /// </summary>
    /// <remarks>
    /// A suspended WebView, or one that has not been created yet, runs nothing — every script comes back
    /// null. Left alone that never resolves, so the scrape must not spend its whole budget on it; but a
    /// session the pass has just asked to wake needs a moment before it can answer, and failing it instantly
    /// skips exactly the accounts the wake was added to rescue. Anything other than a clean, expected answer
    /// within the budget counts as "not running".
    /// </remarks>
    private static async Task<bool> CanRunScriptsAsync(IInstanceConnection connection, string instanceId)
    {
        // A session that is already up answers the first probe, so the common path costs one call as before;
        // only a session that failed it pays the wait.
        var deadline = DateTimeOffset.UtcNow + ScriptReadyBudget;

        while (true)
        {
            try
            {
                var probe = await connection.ExecuteScriptAsync(instanceId, "1").ConfigureAwait(true);
                if (probe is not null && probe.Trim('"', ' ') == "1")
                {
                    return true;
                }
            }
            catch
            {
                // A session mid-creation can throw rather than return null. Same meaning: not ready yet.
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(500).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// When each account was last scraped and whether that attempt read anything — see the freshness floor.
    /// </summary>
    private readonly ConcurrentDictionary<string, (DateTimeOffset At, bool ReadData)> _lastScrapeAttempt =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>UI thread for raising toasts from the background pass.</summary>
    private DispatcherQueue? _ui;

    /// <summary>Drives the sidebar's unanswered-review badge. Optional: the pass works without it.</summary>
    private INotificationHubService? _notificationHub;

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _scrapeLocks =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, ReviewHealth> _byInstance =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, ProfileRating> _ratingByInstance =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A profile rating barely moves, and each scrape costs a visible round-trip to the Search view
    /// and back — so re-scrape at most this often.</summary>
    public static readonly TimeSpan RatingRefreshInterval = TimeSpan.FromHours(6);

    /// <summary>
    /// The shortest gap between two automatic scrapes of the same account.
    /// </summary>
    /// <remarks>
    /// Set just under the panel's own 5-minute auto-refresh so that timer still lands every time, while the
    /// incidental re-entries around it — panel reloads, dashboard redraws — collapse into the cached result.
    /// A user-driven Re-sync passes <c>force</c> and ignores this.
    /// </remarks>
    internal static readonly TimeSpan MinimumRescrapeInterval = TimeSpan.FromMinutes(4);

    /// <summary>
    /// The shorter floor applied after an attempt that read nothing.
    /// </summary>
    /// <remarks>
    /// A cold WebView often fails its first scrape — the page has not run the injected reader yet, or the
    /// account is the one on screen and this pass was not allowed to navigate. Holding those off for the
    /// full four minutes would leave the Reviews card empty for four minutes after every launch, which is a
    /// visible regression traded for traffic nobody asked to save. This still cuts a failing account from
    /// roughly one attempt every three seconds to one every forty-five.
    /// </remarks>
    internal static readonly TimeSpan FailedRetryInterval = TimeSpan.FromSeconds(45);

    /// <summary>
    /// Whether an automatic scrape should be skipped because this account was scraped too recently.
    /// </summary>
    /// <param name="lastAttemptUtc">When this account was last scraped, successfully or not; null if never.</param>
    /// <param name="lastAttemptReadData">Whether that attempt actually came back with review counts.</param>
    /// <param name="force">True for owner-driven Re-sync, which is never throttled.</param>
    /// <remarks>
    /// Pulled out as a plain function so the rule can be tested without a WebView. See
    /// <see cref="MinimumRescrapeInterval"/> for why the floor exists at all.
    /// </remarks>
    internal static bool ShouldSkipAsTooRecent(
        DateTimeOffset? lastAttemptUtc,
        bool lastAttemptReadData,
        DateTimeOffset nowUtc,
        bool force)
    {
        if (force || lastAttemptUtc is not { } last)
        {
            return false;
        }

        // A clock that has gone backwards (NTP correction, DST edge, a VM resuming) must not be able to
        // silence the scrape until real time catches up. Treat it as "not recent" and read again.
        if (nowUtc < last)
        {
            return false;
        }

        var floor = lastAttemptReadData ? MinimumRescrapeInterval : FailedRetryInterval;
        return nowUtc - last < floor;
    }

    /// <summary>
    /// Puts each Google account's unanswered-review count on its sidebar row.
    /// </summary>
    /// <remarks>
    /// The rail already renders a badge per account, driven by NotificationHub — Google accounts simply
    /// never set one, because nothing about reviews fed into it. This is the whole of that fix: the count
    /// the desk already computes, published through the plumbing that was always there.
    ///
    /// <para>
    /// Only published for accounts that were actually read. Writing 0 for an account whose scrape failed
    /// would clear a real badge and say "nothing waiting" about a location nobody managed to check.
    /// </para>
    /// </remarks>
    private void PublishUnansweredBadges(IReadOnlyList<MessengerInstance> accounts)
    {
        if (_notificationHub is null)
        {
            return;
        }

        foreach (var account in accounts)
        {
            var health = Get(account.Id);
            if (!health.HasData)
            {
                continue;
            }

            var unanswered = health.Unanswered;
            _ui?.TryEnqueue(() =>
            {
                try
                {
                    _notificationHub?.UpdateBadgeCount(account.Id, unanswered);
                }
                catch (Exception ex)
                {
                    AppLogger.LogWarning("GoogleReviews", $"Badge update failed: {ex.Message}");
                }
            });
        }
    }

    /// <summary>
    /// Notifies the owner about unhappy reviews seen for the first time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs at the end of a background pass, when every account has been read, so a batch that spans two
    /// locations produces one notification rather than one per account.
    /// </para>
    /// <para>
    /// <b>Quiet hours are honoured by skipping the whole evaluation, not just the toast.</b> Marking the
    /// reviews as seen and then staying silent would mean the owner is never told about a one-star that
    /// landed overnight — it would be "already seen" by morning. Leaving them unrecorded means the first
    /// pass after quiet hours end raises them properly.
    /// </para>
    /// </remarks>
    private async Task RaiseUnhappyReviewAlertsAsync(IReadOnlyList<MessengerInstance> accounts)
    {
        try
        {
            if (QuietHours.IsQuietNow(AppSettingsService.Instance.Settings))
            {
                return;
            }

            var queue = ReviewQueue.Build(accounts.Select(account => (
                account.Id,
                string.IsNullOrWhiteSpace(account.DisplayName) ? "Google Business" : account.DisplayName,
                (ReviewHealth?)Get(account.Id))));

            // A pass where every scrape failed must not count as the first look — see RecordAsync.
            var readSomething = accounts.Any(account => Get(account.Id).HasData);

            var tracking = ReviewAlertTracking.Current;
            var (fresh, seen) = ReviewAlerts.Evaluate(queue, tracking.Seen(), tracking.Seeded);
            await tracking.RecordAsync(seen, readSomething).ConfigureAwait(true);

            if (ReviewAlerts.BuildToast(fresh) is not { } toast)
            {
                return;
            }

            AppLogger.LogInfo(
                "GoogleReviews",
                $"Notifying about {fresh.Count} newly-seen unhappy review(s).");

            _ui?.TryEnqueue(() =>
                AppNotificationService.Instance.ShowInfoToast(toast.Title, toast.Body, fresh[0].InstanceId));
        }
        catch (Exception ex)
        {
            // A missed notification must never break the pass that produces the numbers.
            AppLogger.LogWarning("GoogleReviews", $"Review alerting failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public ReviewHealth Get(string instanceId) =>
        !string.IsNullOrWhiteSpace(instanceId) && _byInstance.TryGetValue(instanceId.Trim(), out var health)
            ? health
            : default;

    /// <summary>The account's official rating/total, or null if never scraped.</summary>
    public ProfileRating? GetRating(string instanceId) =>
        !string.IsNullOrWhiteSpace(instanceId) && _ratingByInstance.TryGetValue(instanceId.Trim(), out var r)
            ? r
            : null;

    /// <summary>
    /// Scrapes the official rating + lifetime review count from the Google Search merchant view (reached via
    /// business.google.com/'s own redirect). Throttled by <see cref="RatingRefreshInterval"/>. The caller must
    /// run the reviews scrape afterwards, which navigates back to /reviews.
    /// </summary>
    public async Task<ProfileRating?> ScrapeRatingAsync(string instanceId, bool force = false, bool allowNavigate = true)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        var id = instanceId.Trim();
        _ratingByInstance.TryGetValue(id, out var cached);
        var haveCached = _ratingByInstance.ContainsKey(id);
        if (!force && haveCached &&
            DateTimeOffset.UtcNow - cached.CapturedAtUtc < RatingRefreshInterval)
        {
            return cached;
        }

        // This scrape has to navigate — the rating and lifetime total exist ONLY on the Search merchant view,
        // never on the reviews manager. So on the account the owner is currently looking at, a background pass
        // would visibly yank their page away. Skip it instead and take the reading on a later pass, or when
        // they switch away; a rating that refreshes an hour late is not worth hijacking the screen for.
        var isOnScreen = string.Equals(
            InstanceSessionManager.Instance.VisibleInstanceId,
            id,
            StringComparison.OrdinalIgnoreCase);
        if (!allowNavigate && isOnScreen)
        {
            return haveCached ? cached : null;
        }

        var connection = InstanceConnection.Current;

        // Same fast fail as the review scrape, and it matters twice over here: the rating runs straight
        // after the review read on the same account, so a sleeping session used to cost 60s and then 60s
        // again. See CanRunScriptsAsync.
        if (!await CanRunScriptsAsync(connection, id).ConfigureAwait(true))
        {
            AppLogger.LogWarning(
                $"GoogleRating.{id}",
                "The account's session is not running scripts (asleep or not yet open), so the rating could not be read. " +
                "Skipped rather than polling it for 60s.");
            return haveCached ? cached : null;
        }

        // A wall-clock budget, not an attempt count. The old `attempt < 24` at 400ms gave this scrape ~9.6s
        // to navigate to business.google.com, follow Google's redirect to the Search merchant view, and let
        // that page render — measured live, it does not finish in that time from cold, so the loop ran out
        // and returned null. Silently: there was no log on the give-up path, so the symptom was simply that
        // no profile ever had a lifetime total and nothing anywhere said why. Same budget the reviews scrape
        // already uses, for the same reason.
        var deadline = DateTimeOffset.UtcNow + PollBudget;
        var lastState = "none";
        var first = true;
        while (DateTimeOffset.UtcNow < deadline)
        {
            // Only the first attempt may navigate; later ones just poll the redirected page.
            var kickoff = $"window.__umGRateAllowNav={(first ? "true" : "false")};" + RatingKickoff;
            first = false;
            try
            {
                await connection.ExecuteScriptAsync(id, kickoff).ConfigureAwait(true);
            }
            catch
            {
                AppLogger.LogInfo($"GoogleRating.{id}", "The account's webview could not run the rating script.");
                return haveCached ? cached : null;
            }

            await Task.Delay(400).ConfigureAwait(true);

            string? raw;
            try
            {
                raw = await connection.ExecuteScriptAsync(id, RatingReadScript).ConfigureAwait(true);
            }
            catch
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            string inner;
            try
            {
                inner = JsonSerializer.Deserialize<string>(raw) ?? raw.Trim('"');
            }
            catch
            {
                inner = raw.Trim('"');
            }

            try
            {
                using var doc = JsonDocument.Parse(inner);
                var root = doc.RootElement;
                var state = root.TryGetProperty("state", out var s) ? s.GetString() : null;
                if (state is not null)
                {
                    lastState = state; // remembered so a give-up can name where it got stuck.
                }

                if (state != "done")
                {
                    continue; // navigating / loading — keep polling.
                }

                var rating = root.TryGetProperty("rating", out var rEl) ? rEl.GetString() : null;
                int? total = null;
                if (root.TryGetProperty("total", out var tEl) &&
                    tEl.ValueKind == JsonValueKind.String &&
                    int.TryParse(tEl.GetString(), out var tVal))
                {
                    total = tVal;
                }

                if (string.IsNullOrWhiteSpace(rating) && total is null)
                {
                    AppLogger.LogInfo($"GoogleRating.{id}", "Merchant view read, but neither rating nor review total was found on it.");
                    return null;
                }

                var result = new ProfileRating(rating ?? string.Empty, total, DateTimeOffset.UtcNow);
                _ratingByInstance[id] = result;

                // The other half of the day's reading. Parsed here rather than stored as a string so the
                // history holds a number the trend maths can use directly.
                double? numericRating = double.TryParse(
                    rating, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsedRating)
                    ? parsedRating
                    : null;
                ReviewHistory.Current.Record(
                    id, numericRating, total, unanswered: null, answered: null);

                // Logged because the lifetime total is what the coverage line leans on, and a null total is
                // invisible in the UI — it just quietly degrades "covers the first 50 of 991" to "covers 50
                // loaded reviews". Two of the owner's three profiles were silently in that state.
                AppLogger.LogInfo(
                    $"GoogleRating.{id}",
                    $"Profile rating {(string.IsNullOrWhiteSpace(rating) ? "unknown" : rating)} — " +
                    $"lifetime total {(total is { } tv ? tv.ToString("N0") : "NOT FOUND")}.");
                return result;
            }
            catch
            {
                // transient parse race — keep polling.
            }
        }

        // Never fall out of this loop silently again. A missing lifetime total has no visible symptom — the
        // coverage line just stops naming a denominator — so without this line the only evidence is a number
        // that isn't there.
        AppLogger.LogWarning(
            $"GoogleRating.{id}",
            $"Gave up after {PollBudget.TotalSeconds:0}s still in state '{lastState}' — " +
            (lastState == "navigating"
                ? "the merchant view never finished loading."
                : "the page never showed a rating or review count (is this account signed in?)."));
        return haveCached ? cached : null;
    }

    /// <summary>The most recent capture time across all accounts — the "as of" stamp for the Reviews section.</summary>
    public DateTimeOffset? LastCapturedUtc =>
        _byInstance.IsEmpty ? null : _byInstance.Values.Where(v => v.HasData).Select(v => (DateTimeOffset?)v.CapturedAtUtc).Max();

    /// <summary>
    /// How often the background pass re-reads each Google account's reviews.
    /// </summary>
    /// <remarks>
    /// Slow on purpose. Every scrape navigates a real WebView to business.google.com/reviews and waits for
    /// it to render, which is far more expensive than the WhatsApp poll's in-memory read — that one runs
    /// every 25–90 seconds and would be abusive here. Reviews arrive a few times a week, so half an hour is
    /// already far finer-grained than the data changes.
    /// </remarks>
    internal static readonly TimeSpan BackgroundInterval = TimeSpan.FromMinutes(30);

    /// <summary>
    /// How long one account's reviews scrape may spend waiting for Google to render.
    /// </summary>
    /// <remarks>
    /// Sized for a COLD page: navigate to business.google.com/reviews, let the app authenticate the view,
    /// and wait for a review list to exist. The previous budget worked out at about nine seconds, which was
    /// enough only for an account already sitting on the right page — the slowest of the owner's three
    /// timed out every pass. Nothing is blocked while this runs; it is a background WebView.
    /// </remarks>
    internal static readonly TimeSpan PollBudget = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan InitialPollInterval = TimeSpan.FromMilliseconds(350);

    private const double MaxPollIntervalMs = 2000;

    private static readonly TimeSpan FirstPassDelay = TimeSpan.FromMinutes(2);

    private DispatcherQueueTimer? _backgroundTimer;
    private IInstanceRegistryService? _registry;
    private bool _backgroundStarted;
    private bool _passRunning;

    /// <summary>
    /// Starts the periodic review refresh.
    /// </summary>
    /// <remarks>
    /// <b>Why this exists at all.</b> <c>ReviewDesk</c> (then <c>ReviewHealthPanel</c>) was the ONLY caller of
    /// <see cref="ScrapeAsync"/>, so reviews were read exclusively while the owner had the Reviews page
    /// open — and its two triggers both forbade navigation, so even then it usually read nothing. The
    /// dashboard's Reviews card reads this service's cache, which therefore stayed empty forever and
    /// rendered "Not scanned yet" indefinitely. Observed live across an entire evening with three healthy,
    /// signed-in Google accounts.
    ///
    /// <para>
    /// The first pass is delayed so it never competes with startup, when every account is still warming its
    /// WebView and the owner is waiting to see their dashboard.
    /// </para>
    /// </remarks>
    public void StartBackgroundRefresh(
        IInstanceRegistryService registry,
        DispatcherQueue ui,
        INotificationHubService? notificationHub = null)
    {
        if (_backgroundStarted || registry is null || ui is null)
        {
            return;
        }

        _backgroundStarted = true;
        _registry = registry;
        _ui = ui;
        _notificationHub = notificationHub;

        _backgroundTimer = ui.CreateTimer();
        _backgroundTimer.Interval = FirstPassDelay;
        _backgroundTimer.Tick += async (timer, _) =>
        {
            // After the deliberately short first delay, settle into the real cadence.
            timer.Interval = BackgroundInterval;
            await RefreshAllAsync().ConfigureAwait(true);
        };
        _backgroundTimer.Start();

        AppLogger.LogInfo(
            "GoogleReviews",
            $"Background review refresh started — first pass in {FirstPassDelay.TotalMinutes:0} min, " +
            $"then every {BackgroundInterval.TotalMinutes:0} min.");
    }

    /// <summary>
    /// Reads every Google account's reviews once. Safe to call at any time; overlapping calls are dropped.
    /// </summary>
    public async Task RefreshAllAsync()
    {
        if (_passRunning || _registry is null)
        {
            return;
        }

        _passRunning = true;
        try
        {
            // Deliberately the same predicate ReviewDesk uses to pick Google accounts, including the
            // sidebar-visibility check. If the background pass and the panel disagreed about which accounts
            // count, the dashboard card and the Reviews page would quietly report different totals.
            var accounts = _registry.Instances
                .Where(i => i.IsProfessional
                            && PlatformModuleSettingsHelper.IsSidebarVisible(i.Platform)
                            && string.Equals(
                                PlatformDefinition.NormalizePlatformId(i.Platform),
                                "googlebusiness",
                                StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (accounts.Count == 0)
            {
                return;
            }

            foreach (var account in accounts)
            {
                // WAKE THE SESSION FIRST — without this the whole pass was theatre.
                //
                // Measured live: for over an hour every scrape in every pass returned state 'none', meaning
                // the injected reader never ran at all. A WebView that has been suspended does not execute
                // scripts, and with nine accounts against an LRU cap of six a session may simply not exist
                // yet after a restart. Either way the page cannot be read, and the scrape has no way out of
                // it on its own: it can only navigate to /reviews from a page already on a google.com host,
                // and a session that isn't running isn't on any host. It recovered only when something
                // unrelated happened to warm the view.
                //
                // Both calls are cheap no-ops when the session is already up and running, and neither makes
                // the account visible — creating a session is separate from showing one.
                try
                {
                    await InstanceSessionManager.Instance.EnsureSessionAsync(account).ConfigureAwait(true);
                    await InstanceSessionManager.Instance.TryResumeSessionAsync(account.Id).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    // A session that refuses to come up is this account's problem, not the whole pass's.
                    AppLogger.LogWarning(
                        $"GoogleReviews.{account.Id}",
                        $"Could not bring the account's session up before scraping: {ex.GetType().Name}: {ex.Message}");
                }

                // allowNavigate:false — ScrapeAsync grants navigation itself for any account that is not the
                // one on screen, which is every account during a background pass in all but one case.
                await ScrapeAsync(account.Id, allowNavigate: false).ConfigureAwait(true);

                // The lifetime total belongs in the background pass for the same reason the review scrape
                // does: until this call was added, the Reviews page was its ONLY caller, so an owner who
                // never opened the Reviews page had no rating and no lifetime total — which silently
                // downgrades the coverage line from "covers the first 50 of 991" to "covers 50 loaded
                // reviews", the honest wording for a number we never read. Own throttle
                // (RatingRefreshInterval, 6h) so a 30-minute pass does not scrape it twelve times a day;
                // this call is a no-op whenever the cached value is still fresh.
                await ScrapeRatingAsync(account.Id, force: false, allowNavigate: false).ConfigureAwait(true);
            }

            PublishUnansweredBadges(accounts);
            await RaiseUnhappyReviewAlertsAsync(accounts).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // A background refresh must never be able to take the app down.
            AppLogger.LogWarning("GoogleReviews", $"Background refresh failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _passRunning = false;
        }
    }

    /// <summary>
    /// Scrapes the account's reviews page (navigating to it if needed) and stores the result. Returns null
    /// when the webview isn't loaded, isn't a Google Business page, or the reviews list never renders.
    /// </summary>
    /// <summary>
    /// How many pages one scrape may walk. <b>Currently 1 — the traversal is disabled.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why it is off.</b> Paging produced totals that were both impossible and unstable: 700 reviews for
    /// a profile with roughly 239, climbing by one page on every pass, and the largest account pinned at
    /// the 2,000-review ceiling. Three separate defects were found and fixed along the way — a rows-per-page
    /// flag that burned on failure, a page-change fingerprint invalidated by the very expander it was
    /// supposed to survive, and two traversals racing on one WebView — and the numbers were still wrong
    /// afterwards. Something in the reset-to-page-one path is still not doing what it claims.
    /// </para>
    /// <para>
    /// A single page is <i>partial</i> but <i>correct</i>, and <see cref="ReviewCoverage"/> now states that
    /// plainly ("covers the first 50 of 239") instead of implying otherwise. Partial and honest beats
    /// complete and wrong, which is the only reason this is a revert rather than a fourth attempt.
    /// </para>
    /// <para>
    /// <b>To re-enable</b>, raise this and first prove, against a live account: that the reset genuinely
    /// returns the list to page one, and that two consecutive passes produce identical totals. The pagination
    /// machinery below (Next detection, fingerprinting, partial-result handling) is kept precisely so that
    /// work starts from here rather than from nothing.
    /// </para>
    /// </remarks>
    internal const int MaxPages = 1;

    /// <summary>
    /// Reads the account's reviews, walking every page Google offers, and stores the totals.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why it pages.</b> Google's rows-per-page control caps at 50, so a profile with more reviews than
    /// that was being counted from its first page alone — and the result was presented as though it were
    /// the whole profile. This now clicks through to the end, and records in
    /// <see cref="ReviewHealth.ReachedLastPage"/> whether it actually got there.
    /// </para>
    /// <para>
    /// <b>A partial read is kept, not discarded.</b> If page four of six times out, the three pages already
    /// counted are still returned, with <c>ReachedLastPage=false</c> so nothing downstream can claim they
    /// are complete. Throwing away good pages because a later one was slow would be worse for the owner
    /// than an honestly-labelled partial count.
    /// </para>
    /// </remarks>
    public async Task<ReviewHealth?> ScrapeAsync(string instanceId, bool allowNavigate = true, bool force = false)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        // FRESHNESS FLOOR — measured, not theoretical. On startup this scraped every Google account roughly
        // six times in two minutes. ReviewDesk kicks off a scrape from its Loaded handler, and the
        // dashboard reloads that panel on every alert-monitor tick and adapter-health change, so each reload
        // fired a fresh pass over all three accounts. The existing SemaphoreSlim only blocks CONCURRENT
        // passes; a pass that finishes in three seconds is not concurrent with the one that follows it.
        //
        // Enforced here rather than in the panel because the panel is not the only caller and the next one
        // added would reintroduce this. Every automatic path — first load, the 5-minute timer, the 30-minute
        // background pass — is subject to it. `force` is for the paths the owner explicitly triggers, where
        // being told "no, that is recent enough" would be wrong.
        //
        // This is real traffic to a real Google account that can be rate-limited, not just wasted work.
        // Keyed on the last ATTEMPT, not the last successful result. Throttling on cached data would leave
        // an account that is failing — signed out, slow to render, timing out — as the one account still
        // scraped on every single panel reload, which is both the worst case for traffic and the least
        // likely to start working because of it.
        var cacheKey = instanceId.Trim();
        var hadAttempt = _lastScrapeAttempt.TryGetValue(cacheKey, out var previous);
        if (ShouldSkipAsTooRecent(
                hadAttempt ? previous.At : null,
                hadAttempt && previous.ReadData,
                DateTimeOffset.UtcNow,
                force))
        {
            return _byInstance.TryGetValue(cacheKey, out var recent) ? recent : null;
        }

        // WHY THIS IS NOT SIMPLY `allowNavigate`.
        //
        // The reviews scrape can only read business.google.com/reviews, and the guard below refused to
        // navigate there on any background refresh. Both entry points in ReviewDesk — the initial
        // load and the auto-refresh timer — pass allowNavigate:false. So unless the owner happened to be
        // sitting on the reviews page themselves, the kickoff returned 'notreviews' and the panel showed
        // "Not scanned yet" indefinitely.
        //
        // The guard's reason was sound — never yank the owner off a page they are reading — but that risk
        // only exists when they are actually LOOKING at that account.
        var isOnScreen = string.Equals(
            InstanceSessionManager.Instance.VisibleInstanceId,
            instanceId,
            StringComparison.OrdinalIgnoreCase);
        var mayNavigate = allowNavigate || !isOnScreen;

        var key = instanceId.Trim();
        var gate = _scrapeLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(0).ConfigureAwait(true))
        {
            // Another traversal is already walking this account's pages. Hand back what we have rather
            // than driving a second set of Next clicks through the same WebView.
            return _byInstance.TryGetValue(key, out var inFlight) ? inFlight : null;
        }

        // Stamped here — gate held, about to do real work — so the floor measures actual scrapes. Stamping
        // before the gate would let a call that turned out to be a duplicate reset the clock for the one
        // genuinely running. Recorded pessimistically as "read nothing" and upgraded only once counts come
        // back, so a scrape that throws, times out, or never returns still gets the short retry floor rather
        // than the long one.
        _lastScrapeAttempt[key] = (DateTimeOffset.UtcNow, false);

        try
        {
        var kickoff = $"window.__umGRAllowNav={(mayNavigate ? "true" : "false")};" + KickoffScript;
        var connection = InstanceConnection.Current;

        // FAST FAIL ON A SESSION THAT ISN'T RUNNING SCRIPTS.
        //
        // A suspended or not-yet-created WebView executes nothing, so `window.__umGR` is never set and the
        // reader reports state 'none' — for the entire 60-second budget, on every account, on every pass.
        // Measured live: six minutes of polling dead views, three times an hour, producing nothing.
        //
        // This asks the page one trivial question first. It deliberately does NOT test the URL: a page
        // mid-navigation is legitimately not on /reviews yet and must still be waited for. It tests only
        // whether script runs at all, which is the one thing no amount of waiting will change.
        if (!await CanRunScriptsAsync(connection, instanceId).ConfigureAwait(true))
        {
            AppLogger.LogWarning(
                $"GoogleReviews.{instanceId}",
                "The account's session is not running scripts (asleep or not yet open), so there is nothing to read. " +
                "Skipped rather than polling it for 60s.");
            return _byInstance.TryGetValue(key, out var stale) ? stale : null;
        }

        // Start every pass from page one. The traversal leaves the WebView on whatever page it stopped at,
        // and the injected flags live on `window`, so without this the next pass resumed from there and
        // counted a different slice each time — the totals climbed by exactly one page per pass, which is
        // what gave the game away. A reload also re-arms the rows-per-page bump.
        if (mayNavigate)
        {
            try
            {
                await connection.ExecuteScriptAsync(instanceId, ResetScript).ConfigureAwait(true);
                await Task.Delay(1200).ConfigureAwait(true);
            }
            catch
            {
                // A failed reset just means this pass reads from wherever the page already was.
            }
        }

        var unanswered = 0;
        var answered = 0;
        var pending = new List<PendingReview>();
        var pagesRead = 0;
        var reachedLastPage = false;

        while (pagesRead < MaxPages)
        {
            var page = await ReadCurrentPageAsync(instanceId, kickoff).ConfigureAwait(true);
            if (page is not { } read)
            {
                break;
            }

            unanswered += read.Unanswered;
            answered += read.Answered;

            // Previews are for the reply queue, and it only ever shows the most urgent handful. Collecting
            // every page's worth would grow without bound on a profile with hundreds of reviews.
            foreach (var item in read.Pending)
            {
                if (pending.Count >= 24)
                {
                    break;
                }

                pending.Add(item);
            }

            pagesRead++;

            if (!read.HasNext)
            {
                reachedLastPage = true;
                break;
            }

            string? advanced;
            try
            {
                advanced = await connection
                    .ExecuteScriptAsync(instanceId, "(window.__umGRNext?window.__umGRNext():false)")
                    .ConfigureAwait(true);
            }
            // Coverage is safe either way — reachedLastPage is only set when Google itself says there is no
            // next page — so a break here correctly reports "stopped before the last page". What was
            // missing is *why* it stopped. D5 keeps pagination capped at one page because walking every
            // page over-counted by two to three times, and re-enabling it needs to distinguish "Google ran
            // out of pages" from "the advance script threw". Without that the traversal is unfalsifiable.
            catch (Exception ex)
            {
                AppLogger.LogWarning(
                    $"GoogleReviews.{instanceId}",
                    $"Stopped paginating after page {pagesRead}: the advance script threw {ex.GetType().Name}.");
                break;
            }

            if (advanced is null || !advanced.Contains("true", StringComparison.OrdinalIgnoreCase))
            {
                AppLogger.LogInfo(
                    $"GoogleReviews.{instanceId}",
                    $"Stopped paginating after page {pagesRead}: no further page was offered.");
                break;
            }
        }

        if (pagesRead == 0)
        {
            return null;
        }

        var health = new ReviewHealth(
            unanswered, answered, DateTimeOffset.UtcNow, true, pending, pagesRead, reachedLastPage);
        _byInstance[instanceId.Trim()] = health;

        // Counts came back, so this attempt earns the long freshness floor. Everything that reaches here
        // without setting this — a timeout, an exception, a page that never rendered — keeps the short one.
        _lastScrapeAttempt[instanceId.Trim()] = (DateTimeOffset.UtcNow, true);

        // Nulls for rating and total: this scrape does not read them, and passing 0 would record a collapse
        // on every day the six-hourly rating scrape happened not to run.
        ReviewHistory.Current.Record(
            instanceId.Trim(), rating: null, lifetimeTotal: null, unanswered: unanswered, answered: answered);

        AppLogger.LogInfo(
            $"GoogleReviews.{instanceId}",
            $"Read {unanswered + answered} review(s) across {pagesRead} page(s) — " +
            $"{unanswered} unanswered, {answered} answered, {pending.Count} preview(s). " +
            (reachedLastPage ? "Reached the last page." : "Stopped before the last page."));

        return health;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>One page's worth of counts, plus whether Google offers another page after it.</summary>
    private readonly record struct PageRead(
        int Unanswered, int Answered, IReadOnlyList<PendingReview> Pending, bool HasNext);

    /// <summary>
    /// Polls the current reviews page until it renders something countable, then reads it once.
    /// </summary>
    private async Task<PageRead?> ReadCurrentPageAsync(string instanceId, string kickoff)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        var connection = InstanceConnection.Current;

        // TIME-BOUNDED, NOT ATTEMPT-BOUNDED.
        //
        // This was `attempt < 24` with a flat 350ms wait — a budget of roughly nine seconds, which is what
        // it actually spent before giving up on Google Depilex DHA-2 (16:48:02 → 16:48:11 in the log). Nine
        // seconds is generous for a page already sitting on /reviews and nowhere near enough for a COLD
        // one, which has to navigate to business.google.com/reviews, authenticate the view, and let
        // Google's app render a review list before anything is countable. The two accounts that succeeded
        // were simply the two that were quick.
        //
        // Counting attempts also hid the problem: 24 attempts sounds like plenty until you notice each is
        // a third of a second. A deadline says what is actually being promised.
        var deadline = DateTimeOffset.UtcNow + PollBudget;
        var pollInterval = InitialPollInterval;
        var lastState = "none";

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await connection.ExecuteScriptAsync(instanceId, kickoff).ConfigureAwait(true);
            }
            catch
            {
                return null;
            }

            await Task.Delay(pollInterval).ConfigureAwait(true);

            // Back off gently. Early polls catch a fast page quickly; later ones stop hammering a slow one.
            pollInterval = TimeSpan.FromMilliseconds(Math.Min(pollInterval.TotalMilliseconds * 1.35, MaxPollIntervalMs));

            string? raw;
            try
            {
                raw = await connection.ExecuteScriptAsync(instanceId, ReadScript).ConfigureAwait(true);
            }
            catch
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            string inner;
            try
            {
                inner = JsonSerializer.Deserialize<string>(raw) ?? raw.Trim('"');
            }
            catch
            {
                inner = raw.Trim('"');
            }

            try
            {
                using var doc = JsonDocument.Parse(inner);
                var state = doc.RootElement.TryGetProperty("state", out var s) ? s.GetString() : null;
                lastState = state ?? lastState;
                if (state == "done")
                {
                    var unanswered = doc.RootElement.GetProperty("unanswered").GetInt32();
                    var answered = doc.RootElement.GetProperty("answered").GetInt32();
                    var pending = new List<PendingReview>();
                    if (doc.RootElement.TryGetProperty("pending", out var pendingEl) &&
                        pendingEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in pendingEl.EnumerateArray())
                        {
                            var reviewer = item.TryGetProperty("reviewer", out var r) ? r.GetString() ?? "" : "";
                            var text = item.TryGetProperty("text", out var tx) ? tx.GetString() ?? "" : "";
                            var stars = item.TryGetProperty("stars", out var st) && st.ValueKind == JsonValueKind.Number
                                ? st.GetInt32()
                                : 0;
                            var age = item.TryGetProperty("age", out var ag) ? ag.GetString() ?? "" : "";
                            var idx = item.TryGetProperty("idx", out var ix) && ix.ValueKind == JsonValueKind.Number
                                ? ix.GetInt32()
                                : pending.Count;
                            if (!string.IsNullOrWhiteSpace(reviewer) || !string.IsNullOrWhiteSpace(text))
                            {
                                pending.Add(new PendingReview(
                                    string.IsNullOrWhiteSpace(reviewer) ? "Reviewer" : reviewer,
                                    text,
                                    stars is >= 1 and <= 5 ? stars : 0,
                                    age,
                                    idx));
                            }
                        }
                    }

                    var hasNext = doc.RootElement.TryGetProperty("hasNext", out var hn) &&
                                  hn.ValueKind == JsonValueKind.True;
                    return new PageRead(unanswered, answered, pending, hasNext);
                }
                if (state is "notreviews" or "error")
                {
                    // Previously a bare `return null`, which is precisely why "Not scanned yet" was
                    // impossible to diagnose: the panel showed an empty state and nothing anywhere said
                    // that the scrape had run, reached a page it could not read, and given up.
                    AppLogger.LogWarning(
                        $"GoogleReviews.{instanceId}",
                        state == "notreviews"
                            ? "The account is not on business.google.com/reviews and this pass was not " +
                              "allowed to navigate there, so no review counts could be read."
                            : "The reviews page threw while being read; no counts were taken.");
                    return null;
                }
                // navigating / loading / none → keep polling.
            }
            catch
            {
                // transient parse race — keep polling.
            }
        }

        // Ran out of budget. Naming the state it was stuck in is the difference between a shrug and a
        // diagnosis: 'navigating' means the page never arrived, 'loading' means it arrived but never
        // rendered a countable list, and 'none' means the kickoff script never took hold at all.
        AppLogger.LogWarning(
            $"GoogleReviews.{instanceId}",
            $"Gave up after {PollBudget.TotalSeconds:0}s still in state '{lastState}' — " +
            lastState switch
            {
                "navigating" => "the reviews page never finished loading.",
                "loading" => "the page loaded but never rendered a countable review list.",
                "none" => "the page never ran the reader script (is this account signed in?).",
                _ => "no review counts could be read."
            });
        return null;
    }
}
