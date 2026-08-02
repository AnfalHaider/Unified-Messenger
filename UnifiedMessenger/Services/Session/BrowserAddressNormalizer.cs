namespace UnifiedMessenger.Services;

/// <summary>
/// Turns whatever the owner typed into the address bar into a URL we're willing to navigate to.
/// </summary>
/// <remarks>
/// Deliberately strict about two things.
/// <para>
/// <b>Only http/https.</b> The webview is a real browser with the account's live session in it;
/// <c>file:</c> would expose the local disk to a page, and <c>javascript:</c> / <c>data:</c> are how you
/// smuggle script into someone else's origin. Everything that isn't plain web navigation is refused.
/// </para>
/// <para>
/// <b>No search fallback.</b> Typing something that isn't a URL does NOT get handed to a search engine.
/// This app's whole premise is that nothing leaves the machine unasked; quietly turning a typo into a
/// Google query would break that. Non-URL input is rejected with a reason instead.
/// </para>
/// </remarks>
public static class BrowserAddressNormalizer
{
    public readonly record struct Result(bool IsValid, string Url, string? Error)
    {
        public static Result Ok(string url) => new(true, url, null);

        public static Result Fail(string error) => new(false, string.Empty, error);
    }

    public static Result Normalize(string? input)
    {
        var text = input?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return Result.Fail("Enter a web address.");
        }

        // Reject anything with a scheme we don't allow BEFORE the https:// guess, so "file:///c:/…"
        // can't slip through as "https://file:///c:/…".
        var schemeSeparator = text.IndexOf("://", StringComparison.Ordinal);
        var colon = text.IndexOf(':');
        if (schemeSeparator > 0)
        {
            var scheme = text[..schemeSeparator];
            if (!IsAllowedScheme(scheme))
            {
                return Result.Fail($"Only web addresses (http or https) can be opened here — not {scheme}.");
            }
        }
        else if (colon > 0 && !LooksLikePortSuffix(text, colon))
        {
            // Scheme-like prefix with no "//" — javascript:, data:, mailto:, about: and friends.
            return Result.Fail($"Only web addresses (http or https) can be opened here — not {text[..colon]}.");
        }

        var candidate = schemeSeparator > 0 ? text : "https://" + text;

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            return Result.Fail("That doesn't look like a web address.");
        }

        if (!IsAllowedScheme(uri.Scheme))
        {
            return Result.Fail($"Only web addresses (http or https) can be opened here — not {uri.Scheme}.");
        }

        if (string.IsNullOrWhiteSpace(uri.Host) || !uri.Host.Contains('.'))
        {
            // "localhost" is the one dotless host worth allowing — it's how someone would point this at a
            // local dashboard, which is squarely in keeping with a local-only app.
            if (!uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Fail("That doesn't look like a web address. Try something like example.com.");
            }
        }

        return Result.Ok(uri.ToString());
    }

    /// <summary>Short display form for the address bar — the host and path, without the noisy scheme.</summary>
    public static string ToDisplayForm(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? (uri.Host + uri.PathAndQuery).TrimEnd('/')
            : url;
    }

    /// <summary>A sensible default account name for a site being saved — its host, minus "www.".</summary>
    public static string SuggestDisplayName(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "Web page";
        }

        var host = uri.Host;
        if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            host = host[4..];
        }

        if (host.Length == 0)
        {
            return "Web page";
        }

        // "docs.example.com" → "Docs.example.com" reads better than a bare lowercase host in a sidebar.
        return char.ToUpperInvariant(host[0]) + host[1..];
    }

    private static bool IsAllowedScheme(string scheme) =>
        scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
        scheme.Equals("https", StringComparison.OrdinalIgnoreCase);

    // "example.com:8080/path" — a colon followed by digits is a port, not a scheme.
    private static bool LooksLikePortSuffix(string text, int colonIndex)
    {
        var after = text[(colonIndex + 1)..];
        if (after.Length == 0)
        {
            return false;
        }

        var digits = 0;
        foreach (var c in after)
        {
            if (char.IsDigit(c))
            {
                digits++;
                continue;
            }

            return c == '/' && digits > 0;
        }

        return digits > 0;
    }
}
