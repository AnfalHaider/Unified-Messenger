using System.Text.Json;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Imports an account's own profile photo from its live web session into the avatar cache. Uses a
/// start/poll bridge (ExecuteScriptAsync does not await promises): a kickoff script locates the self-avatar
/// and captures it — first via canvas, then, if the canvas is cross-origin-tainted (e.g. pps.whatsapp.net),
/// via fetch → blob → data URL — storing the result on <c>window.__umAv</c>; a read script polls it.
/// Best-effort: DOM/CORS vary per platform and the account must be loaded with its photo visible.
/// </summary>
public static class AvatarImportService
{
    public enum ImportResult
    {
        Imported,
        NotLoadedOrNotFound,
        Failed
    }

    private const string ReadScript = "(window.__umAv?JSON.stringify(window.__umAv):'{\"state\":\"none\"}')";

    public static async Task<ImportResult> TryImportProfilePhotoAsync(string instanceId, string? platform)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return ImportResult.Failed;
        }

        var connection = InstanceConnection.Current;
        try
        {
            await connection.ExecuteScriptAsync(instanceId, BuildKickoffScript(platform)).ConfigureAwait(true);
        }
        catch
        {
            return ImportResult.Failed;
        }

        string? dataUrl = null;
        for (var attempt = 0; attempt < 30; attempt++)
        {
            await Task.Delay(150).ConfigureAwait(true);

            string? raw;
            try
            {
                raw = await connection.ExecuteScriptAsync(instanceId, ReadScript).ConfigureAwait(true);
            }
            catch
            {
                return ImportResult.Failed;
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            // ExecuteScriptAsync returns the JS string JSON-encoded; unwrap to the inner JSON object.
            string inner;
            try
            {
                inner = JsonSerializer.Deserialize<string>(raw) ?? raw.Trim('"');
            }
            catch
            {
                inner = raw.Trim('"');
            }

            string? state;
            try
            {
                using var doc = JsonDocument.Parse(inner);
                state = doc.RootElement.TryGetProperty("state", out var s) ? s.GetString() : null;
                if (state == "done" && doc.RootElement.TryGetProperty("data", out var d))
                {
                    dataUrl = d.GetString();
                }
            }
            catch
            {
                continue;
            }

            if (state == "done")
            {
                break;
            }
            if (state is "notfound")
            {
                return ImportResult.NotLoadedOrNotFound;
            }
            if (state is "error")
            {
                return ImportResult.Failed;
            }
            // "working" / "none" → keep polling.
        }

        if (string.IsNullOrWhiteSpace(dataUrl) || !dataUrl.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
        {
            return ImportResult.NotLoadedOrNotFound;
        }

        var comma = dataUrl.IndexOf(',');
        if (comma < 0 || comma + 1 >= dataUrl.Length)
        {
            return ImportResult.Failed;
        }

        try
        {
            var bytes = Convert.FromBase64String(dataUrl[(comma + 1)..]);
            if (bytes.Length == 0)
            {
                return ImportResult.NotLoadedOrNotFound;
            }

            await ProfileAvatarService.SaveAvatarAsync(instanceId, bytes).ConfigureAwait(true);
            return ImportResult.Imported;
        }
        catch
        {
            return ImportResult.Failed;
        }
    }

    /// <summary>
    /// Kickoff script: find the self-avatar element, then capture it to <c>window.__umAv</c> (canvas first,
    /// fetch fallback for tainted cross-origin images). Returns synchronously after starting the work.
    /// </summary>
    private static string BuildKickoffScript(string? platform)
    {
        var normalized = PlatformDefinition.NormalizePlatformId(platform);
        var isWhatsApp = normalized is "whatsapp" or "whatsappbusiness";

        // Target selection. WhatsApp: prefer the self-chat ("(You)") avatar, then the bottom of the left nav
        // rail (the settings/profile avatar) — both are usually same-origin blobs, unlike the cross-origin
        // pps.whatsapp.net header photo. Then the largest photo (open Profile panel). Others: largest photo.
        var pick = isWhatsApp
            ? "var youRow=cands.filter(function(c){var t=(c.el.closest('[role=\\'row\\'],[role=\\'listitem\\'],div')||{}).innerText||'';return /\\(You\\)|\\(you\\)/.test(t);});" +
              "if(youRow.length){target=youRow[0];}" +
              "if(!target){var rail=cands.filter(function(c){return c.left<96;});if(rail.length){rail.sort(function(a,b){return b.top-a.top;});target=rail[0];}}" +
              "if(!target){cands.sort(function(a,b){return b.size-a.size;});target=cands[0];}"
            : "if(cands.length){cands.sort(function(a,b){return b.size-a.size;});target=cands[0];}";

        return
            "(function(){try{" +
            "window.__umAv={state:'working'};" +
            "function rect(m){return m.getBoundingClientRect();}" +
            "function src(m){return m.currentSrc||m.src||'';}" +
            "function isPhoto(s){return /pps\\.whatsapp\\.net|fbcdn|cdninstagram|telesco\\.pe|^blob:|^data:image|^https?:\\/\\//i.test(s)&&!/\\.svg(\\?|$)/i.test(s);}" +
            "var cands=[];" +
            "Array.prototype.slice.call(document.images).forEach(function(m){var r=rect(m);var s=src(m);" +
            "if(r.width>=20&&r.height>=20&&m.naturalWidth>=24&&Math.abs(m.naturalWidth-m.naturalHeight)<=12&&isPhoto(s))" +
            "cands.push({el:m,src:s,left:r.left,top:r.top,size:m.naturalWidth});});" +
            "var target=null;" +
            pick +
            "if(!target){window.__umAv={state:'notfound'};return 'nf';}" +
            "var el=target.el,url=target.src;" +
            "try{var z=Math.min(el.naturalWidth||256,256);var c=document.createElement('canvas');c.width=z;c.height=z;" +
            "c.getContext('2d').drawImage(el,0,0,z,z);window.__umAv={state:'done',data:c.toDataURL('image/png')};}" +
            "catch(e){fetch(url).then(function(r){return r.blob();}).then(function(b){var fr=new FileReader();" +
            "fr.onloadend=function(){window.__umAv={state:'done',data:fr.result};};fr.onerror=function(){window.__umAv={state:'error'};};fr.readAsDataURL(b);})" +
            ".catch(function(){window.__umAv={state:'error'};});}" +
            "return 'started';" +
            "}catch(e){window.__umAv={state:'error'};return 'err';}})()";
    }
}
