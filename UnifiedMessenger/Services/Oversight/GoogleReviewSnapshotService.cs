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
        "if(!/\\/reviews(\\/|$)/.test(location.pathname)){" +
        "if(/business\\.google\\.com/.test(location.host)){if(!window.__umGRnav){window.__umGRnav=1;location.href='https://business.google.com/reviews';}window.__umGR={state:'navigating'};return;}" +
        "window.__umGR={state:'notreviews'};return;}" +
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

    private static readonly Lazy<GoogleReviewSnapshotService> LazyInstance = new(() => new GoogleReviewSnapshotService());

    public static GoogleReviewSnapshotService Instance => LazyInstance.Value;

    private readonly ConcurrentDictionary<string, ReviewHealth> _byInstance =
        new(StringComparer.OrdinalIgnoreCase);

    public ReviewHealth Get(string instanceId) =>
        !string.IsNullOrWhiteSpace(instanceId) && _byInstance.TryGetValue(instanceId.Trim(), out var health)
            ? health
            : default;

    /// <summary>The most recent capture time across all accounts — the "as of" stamp for the Reviews section.</summary>
    public DateTimeOffset? LastCapturedUtc =>
        _byInstance.IsEmpty ? null : _byInstance.Values.Where(v => v.HasData).Select(v => (DateTimeOffset?)v.CapturedAtUtc).Max();

    /// <summary>
    /// Scrapes the account's reviews page (navigating to it if needed) and stores the result. Returns null
    /// when the webview isn't loaded, isn't a Google Business page, or the reviews list never renders.
    /// </summary>
    public async Task<ReviewHealth?> ScrapeAsync(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        var connection = InstanceConnection.Current;
        for (var attempt = 0; attempt < 24; attempt++)
        {
            try
            {
                await connection.ExecuteScriptAsync(instanceId, KickoffScript).ConfigureAwait(true);
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
