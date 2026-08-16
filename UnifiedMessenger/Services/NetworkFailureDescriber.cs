namespace UnifiedMessenger.Services;

/// <summary>
/// Turns the machine-readable failures that come back from a dropped connection into something the owner
/// of a salon can act on.
///
/// <para>
/// Two sources feed this. WebView2 reports a <c>CoreWebView2WebErrorStatus</c>, and the app used to store
/// <c>status.ToString()</c> as the account's connection detail — so an owner whose wifi had dropped saw
/// their WhatsApp account labelled <b>"HostNameNotResolved"</b>. The updater reports an
/// <see cref="System.Net.Http.HttpRequestException"/>, whose message is a Winsock diagnostic like
/// <b>"No such host is known. (api.github.com:443)"</b>. Neither says the one useful thing, which is that
/// the internet is down and this is not the product breaking.
/// </para>
/// <para>
/// The buckets are deliberately coarse. There is exactly one action behind most of these — check the
/// connection — and inventing a distinct sentence per error code would be precision the reader cannot use.
/// Certificate and proxy failures are separated out because those need a different person to fix them.
/// </para>
/// </summary>
public static class NetworkFailureDescriber
{
    /// <summary>Shown as an account's connection detail when the machine cannot reach the network.</summary>
    public const string AccountOffline = "No internet connection";

    /// <summary>Shown when the connection works but the certificate does not validate.</summary>
    public const string AccountSecureConnectionProblem = "Secure connection problem";

    /// <summary>Shown when a proxy stands between the app and the site and wants credentials.</summary>
    public const string AccountProxyProblem = "Blocked by a network proxy";

    /// <summary>Shown when an update check cannot reach GitHub.</summary>
    public const string UpdateCheckOffline =
        "Could not reach the update server. Check your internet connection and try again.";

    // CoreWebView2WebErrorStatus names that mean "the machine could not get to the network". Matched by
    // name rather than by enum value so this helper stays free of a WebView2 reference and is unit-testable.
    private static readonly HashSet<string> ConnectivityStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "HostNameNotResolved",
        "ServerUnreachable",
        "CannotConnect",
        "Disconnected",
        "ConnectionAborted",
        "ConnectionReset",
        "Timeout"
    };

    private static readonly HashSet<string> CertificateStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "CertificateCommonNameIsIncorrect",
        "CertificateExpired",
        "CertificateRevoked",
        "CertificateIsInvalid",
        "ClientCertificateContainsErrors"
    };

    private static readonly HashSet<string> ProxyStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ValidProxyAuthenticationRequired",
        "ValidAuthenticationCredentialsRequired"
    };

    // Fragments that appear in the exception messages .NET produces with no network. Used only as a
    // backstop in the presenter — the updater classifies by exception TYPE, which is more reliable.
    private static readonly string[] NetworkExceptionFragments =
    [
        "no such host is known",
        "unreachable network",
        "unreachable host",
        "actively refused",
        "connection attempt failed",
        "ssl connection could not be established",
        "a task was canceled",
        "the operation was canceled",
        "connection was closed",
        "name or service not known",
        "network is unreachable",
        "no connection could be made"
    ];

    /// <summary>
    /// Plain-English text for a stored connection detail, or <see langword="null"/> when the detail is not
    /// a WebView2 error code and should be shown as-is. Returning null rather than a fallback string is
    /// what lets callers pass through details that were already readable.
    /// </summary>
    public static string? DescribeWebViewStatus(string? webErrorStatus)
    {
        if (string.IsNullOrWhiteSpace(webErrorStatus))
        {
            return null;
        }

        var status = webErrorStatus.Trim();

        if (ConnectivityStatuses.Contains(status))
        {
            return AccountOffline;
        }

        if (CertificateStatuses.Contains(status))
        {
            return AccountSecureConnectionProblem;
        }

        if (ProxyStatuses.Contains(status))
        {
            return AccountProxyProblem;
        }

        return null;
    }

    /// <summary>True when an exception message reads like a connectivity failure rather than a real fault.</summary>
    public static bool LooksLikeConnectivityFailure(string? exceptionMessage)
    {
        if (string.IsNullOrWhiteSpace(exceptionMessage))
        {
            return false;
        }

        var message = exceptionMessage.Trim();
        return NetworkExceptionFragments.Any(fragment =>
            message.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }
}
