using System.Collections.Concurrent;
using System.Text.Json;

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
    public readonly record struct PendingReview(string Reviewer, string Snippet);

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
        IReadOnlyList<PendingReview> Pending)
    {
        public int Total => Unanswered + Answered;

        public int ReplyRatePercent => Total > 0 ? (int)Math.Round(100.0 * Answered / Total) : 0;
    }

    // Counts Reply (unanswered) vs Edit (answered) buttons on the reviews page; navigates there first if the
    // Google Business webview is on a different page. Idempotent — safe to run repeatedly while polling.
    private const string KickoffScript =
        "(function(){try{" +
        // Only navigate to /reviews when explicitly allowed (a user-driven Re-sync). A background refresh
        // passes allowNavigate:false so it can never yank the owner off whatever Google page they're reading.
        "if(!/\\/reviews(\\/|$)/.test(location.pathname)){" +
        "if(window.__umGRAllowNav&&/business\\.google\\.com/.test(location.host)){if(!window.__umGRnav){window.__umGRnav=1;location.href='https://business.google.com/reviews';}window.__umGR={state:'navigating'};return;}" +
        "window.__umGR={state:'notreviews'};return;}" +
        // Bump "Rows per page" to its max once, so the counts below cover more than the default 10.
        // ponytail: synthetic .click() drives Google's jsaction listbox (opener jsname=LgbsSe, options carry
        // data-value); if a Google build ignores it this simply no-ops and we count the default page — no
        // regression. Upgrade path if it stops working: dispatch a real MouseEvent instead of .click().
        "if(!window.__umGRrowsDone){window.__umGRrowsDone=1;try{" +
        "var rb=document.querySelector('[aria-label=\"Number of rows per page\"]');" +
        "if(rb){var op=rb.querySelector('[jsname=\"LgbsSe\"]');if(op)op.click();" +
        "setTimeout(function(){try{var o=[].slice.call(rb.querySelectorAll('[data-value]'));" +
        "var m=o.reduce(function(a,c){return (+(c.getAttribute('data-value')||0))>(+(a.getAttribute('data-value')||0))?c:a;},o[0]);" +
        "if(m)m.click();}catch(e){}},250);" +
        "window.__umGR={state:'loading'};return;}}catch(e){}}" +
        "var b=[].slice.call(document.querySelectorAll('button'));" +
        "var replyBtns=b.filter(function(x){return /(^|\\b)reply\\b/i.test((x.innerText||'').trim());});" +
        "var reply=replyBtns.length;" +
        "var edit=b.filter(function(x){return /\\bedit\\b/i.test((x.innerText||'').trim());}).length;" +
        "if(reply+edit===0){window.__umGR={state:'loading'};return;}" +
        // Best-effort: for each unanswered review, climb to the smallest ancestor card holding its text and
        // pull the reviewer name (first meaningful line) + a snippet (longest line). Structure varies, so this
        // may need locale/UI tuning — the counts above are the reliable signal.
        "var pending=[];replyBtns.slice(0,8).forEach(function(btn){var n=btn.parentElement,card=null;" +
        "for(var i=0;i<8&&n;i++){var t=(n.innerText||'').trim();if(t.length>=25&&t.length<=700){card=n;break;}n=n.parentElement;}" +
        "var lines=((card&&card.innerText)||'').split('\\n').map(function(s){return s.trim();})" +
        ".filter(function(s){return s.length>1&&!/^(reply|edit|share|read more|like|helpful|\\d+ (day|week|month|year)s? ago)$/i.test(s);});" +
        "var name=lines[0]||'Reviewer';var snip='';lines.forEach(function(l){if(l!==name&&l.length>snip.length)snip=l;});" +
        "pending.push({reviewer:name.slice(0,60),snippet:snip.slice(0,140)});});" +
        "window.__umGR={state:'done',unanswered:reply,answered:edit,pending:pending};" +
        "}catch(e){window.__umGR={state:'error'};}})()";

    private const string ReadScript = "(window.__umGR?JSON.stringify(window.__umGR):'{\"state\":\"none\"}')";

    // Scrapes the profile's official rating + lifetime review count. These live ONLY on the Google Search
    // merchant view — business.google.com/reviews has neither. Verified live on that page:
    //   • rating  → an aria-label reading exactly "Rated 4.6 out of 5,"  (cleanest, locale-stable-ish source)
    //   • total   → body text "239 Google reviews"
    // NOTE: innerText renders them CONCATENATED ("4.6239 Google reviews"), which is why a \b-anchored number
    // regex finds nothing — hence the aria-label for the rating rather than parsing the run-together text.
    // business.google.com/ (root) redirects a single-location profile to that view, so we use Google's own
    // redirect instead of guessing a search URL. Navigation is allowed on the first attempt only.
    private const string RatingKickoff =
        "(function(){try{" +
        "var a=[].slice.call(document.querySelectorAll('[aria-label]'));var r=null;" +
        "for(var i=0;i<a.length;i++){var m=/Rated\\s+([0-5][.,]\\d)\\s+out\\s+of\\s+5/i.exec(a[i].getAttribute('aria-label')||'');" +
        "if(m){r=m[1].replace(',','.');break;}}" +
        "var t=(document.body&&document.body.innerText)||'';" +
        // innerText renders the rating and count RUN TOGETHER ("4.6239 Google reviews"), so a bare ([\d,]+)
        // before "Google reviews" swallows the rating's decimal digit -> 6239 instead of 239 (and "4.81,234"
        // -> 81234). Anchor on the rating so the two split correctly; the [^\d]{0,6} also tolerates a layout
        // that separates them ("4.6 ★ 239 Google reviews").
        "var c=/([0-5][.,]\\d)[^\\d]{0,6}([\\d,]+)\\s+Google\\s+reviews/i.exec(t);" +
        "var tot=c?c[2].replace(/,/g,''):null;" +
        // Fallback for a layout with no rating next to the count: require a non-digit/dot before it so we
        // still can't slice a number out of the middle of another one.
        "if(!tot){var c2=/(?:^|[^\\d.,])([\\d,]{1,7})\\s+Google\\s+reviews/i.exec(t);tot=c2?c2[1].replace(/,/g,''):null;}" +
        "if(!r&&c){r=c[1].replace(',','.');}" +
        "if(r||tot){window.__umGRate={state:'done',rating:r,total:tot};return;}" +
        "if(window.__umGRateAllowNav){location.href='https://business.google.com/';window.__umGRate={state:'navigating'};return;}" +
        "window.__umGRate={state:'loading'};" +
        "}catch(e){window.__umGRate={state:'error'};}})()";

    private const string RatingReadScript = "(window.__umGRate?JSON.stringify(window.__umGRate):'{\"state\":\"none\"}')";

    private static readonly Lazy<GoogleReviewSnapshotService> LazyInstance = new(() => new GoogleReviewSnapshotService());

    public static GoogleReviewSnapshotService Instance => LazyInstance.Value;

    private readonly ConcurrentDictionary<string, ReviewHealth> _byInstance =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, ProfileRating> _ratingByInstance =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A profile rating barely moves, and each scrape costs a visible round-trip to the Search view
    /// and back — so re-scrape at most this often.</summary>
    public static readonly TimeSpan RatingRefreshInterval = TimeSpan.FromHours(6);

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
    public async Task<ProfileRating?> ScrapeRatingAsync(string instanceId, bool force = false)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        var id = instanceId.Trim();
        if (!force && _ratingByInstance.TryGetValue(id, out var cached) &&
            DateTimeOffset.UtcNow - cached.CapturedAtUtc < RatingRefreshInterval)
        {
            return cached;
        }

        var connection = InstanceConnection.Current;
        for (var attempt = 0; attempt < 24; attempt++)
        {
            // Only the first attempt may navigate; later ones just poll the redirected page.
            var kickoff = $"window.__umGRateAllowNav={(attempt == 0 ? "true" : "false")};" + RatingKickoff;
            try
            {
                await connection.ExecuteScriptAsync(id, kickoff).ConfigureAwait(true);
            }
            catch
            {
                return null;
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
                if ((root.TryGetProperty("state", out var s) ? s.GetString() : null) != "done")
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
                    return null;
                }

                var result = new ProfileRating(rating ?? string.Empty, total, DateTimeOffset.UtcNow);
                _ratingByInstance[id] = result;
                return result;
            }
            catch
            {
                // transient parse race — keep polling.
            }
        }

        return null;
    }

    /// <summary>The most recent capture time across all accounts — the "as of" stamp for the Reviews section.</summary>
    public DateTimeOffset? LastCapturedUtc =>
        _byInstance.IsEmpty ? null : _byInstance.Values.Where(v => v.HasData).Select(v => (DateTimeOffset?)v.CapturedAtUtc).Max();

    /// <summary>
    /// Scrapes the account's reviews page (navigating to it if needed) and stores the result. Returns null
    /// when the webview isn't loaded, isn't a Google Business page, or the reviews list never renders.
    /// </summary>
    public async Task<ReviewHealth?> ScrapeAsync(string instanceId, bool allowNavigate = true)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        // The kickoff only navigates to /reviews when this is set — background refreshes pass false.
        var kickoff = $"window.__umGRAllowNav={(allowNavigate ? "true" : "false")};" + KickoffScript;

        var connection = InstanceConnection.Current;
        for (var attempt = 0; attempt < 24; attempt++)
        {
            try
            {
                await connection.ExecuteScriptAsync(instanceId, kickoff).ConfigureAwait(true);
            }
            catch
            {
                return null;
            }

            await Task.Delay(350).ConfigureAwait(true);

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
                            var snippet = item.TryGetProperty("snippet", out var sn) ? sn.GetString() ?? "" : "";
                            if (!string.IsNullOrWhiteSpace(reviewer) || !string.IsNullOrWhiteSpace(snippet))
                            {
                                pending.Add(new PendingReview(
                                    string.IsNullOrWhiteSpace(reviewer) ? "Reviewer" : reviewer, snippet));
                            }
                        }
                    }

                    var health = new ReviewHealth(unanswered, answered, DateTimeOffset.UtcNow, true, pending);
                    _byInstance[instanceId.Trim()] = health;
                    return health;
                }
                if (state is "notreviews" or "error")
                {
                    return null;
                }
                // navigating / loading / none → keep polling.
            }
            catch
            {
                // transient parse race — keep polling.
            }
        }

        return null;
    }
}
