using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace UnifiedMessenger.Services;

/// <summary>
/// Singleton manager that owns one shared <see cref="CoreWebView2Environment"/> (single UDF, single browser process)
/// and creates <see cref="WebView2"/> controls bound to isolated profile names.
/// </summary>
public sealed partial class WebViewProfileManager : IWebViewProfileManager
{
    private static readonly Lazy<WebViewProfileManager> LazyInstance = new(() => new WebViewProfileManager());

    private readonly SemaphoreSlim _environmentLock = new(1, 1);

    private CoreWebView2Environment? _sharedEnvironment;

    private WebViewProfileManager()
    {
        UserDataFolder = Path.Combine(ApplicationPaths.UserDataRoot, "WebView2");
    }

    public static WebViewProfileManager Instance => LazyInstance.Value;

    /// <summary>
    /// Shared user data folder path used by every profile in this application.
    /// </summary>
    public string UserDataFolder { get; }

    public CoreWebView2Environment? SharedEnvironment => _sharedEnvironment;

    /// <summary>
    /// Initializes the shared WebView2 environment. Safe to call multiple times.
    /// </summary>
    public async Task<CoreWebView2Environment> EnsureEnvironmentAsync(
        CancellationToken cancellationToken = default)
    {
        if (_sharedEnvironment is not null)
        {
            return _sharedEnvironment;
        }

        await _environmentLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_sharedEnvironment is not null)
            {
                return _sharedEnvironment;
            }

            Directory.CreateDirectory(UserDataFolder);

            // Shared browser process flags — see WebView2 browser feature flags (V8 scavenger cap).
            // Documented in STAGE3-Core-Architect-changelog.md.
            var environmentOptions = new CoreWebView2EnvironmentOptions
            {
                AdditionalBrowserArguments =
                    "--js-flags=--scavenger_max_new_space_capacity_mb=32"
            };

            _sharedEnvironment = await CoreWebView2Environment
                .CreateWithOptionsAsync(
                    browserExecutableFolder: null,
                    userDataFolder: UserDataFolder,
                    options: environmentOptions)
                .AsTask()
                .ConfigureAwait(false);

            return _sharedEnvironment;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WebView2 environment initialization failed: {ex.Message}");
            throw;
        }
        finally
        {
            _environmentLock.Release();
        }
    }

    /// <summary>
    /// Creates and initializes a <see cref="WebView2"/> control mapped to the given profile name.
    /// Must be called from the UI thread. The caller owns the control and must close it when unloaded.
    /// </summary>
    public Task<WebView2> CreateWebViewAsync(
        string profileName,
        string? startUrl = null,
        CancellationToken cancellationToken = default) =>
        UiThreadRunner.RunAsync(() => CreateWebViewCoreAsync(profileName, startUrl, cancellationToken));

    private async Task<WebView2> CreateWebViewCoreAsync(
        string profileName,
        string? startUrl,
        CancellationToken cancellationToken)
    {
        profileName = NormalizeProfileName(profileName);
        ValidateProfileName(profileName);

        var environment = await EnsureEnvironmentAsync(cancellationToken).ConfigureAwait(false);
        await UiThreadRunner.YieldToUiAsync().ConfigureAwait(true);

        return await InitializeWebViewControlAsync(environment, profileName, startUrl, cancellationToken)
            .ConfigureAwait(true);
    }

    private static Task<WebView2> InitializeWebViewControlAsync(
        CoreWebView2Environment environment,
        string profileName,
        string? startUrl,
        CancellationToken cancellationToken) =>
        UiThreadRunner.RunAsync(async () =>
        {
            WebView2? webView = null;
            try
            {
                webView = new WebView2();
                var options = environment.CreateCoreWebView2ControllerOptions();
                options.ProfileName = profileName;

                await WebViewUiAwaiter
                    .AwaitAsync(webView.EnsureCoreWebView2Async(environment, options).AsTask())
                    .ConfigureAwait(true);

                return FinalizeWebViewControlOnUiThread(webView, profileName, startUrl);
            }
            catch
            {
                if (webView is not null)
                {
                    await UiThreadRunner.YieldToUiAsync().ConfigureAwait(true);
                    webView.Close();
                }

                throw;
            }
        });

    private static WebView2 FinalizeWebViewControlOnUiThread(
        WebView2 webView,
        string profileName,
        string? startUrl)
    {
        var actualProfile = webView.CoreWebView2?.Profile.ProfileName;
        if (actualProfile is null ||
            !actualProfile.Equals(profileName, StringComparison.OrdinalIgnoreCase))
        {
            webView.Close();
            throw new InvalidOperationException(
                $"Profile mismatch. Expected \"{profileName}\" but got \"{actualProfile ?? "null"}\".");
        }

        if (webView.CoreWebView2 is not null)
        {
            WebViewNavigationGuard.Attach(
                webView.CoreWebView2,
                WebViewNavigationGuard.ExtractAdditionalHostsFromStartUrl(startUrl));
        }

        return webView;
    }

    private static Task CloseWebViewOnUiThreadAsync(WebView2 webView) =>
        UiThreadRunner.RunAsync(() =>
        {
            webView.Close();
            return Task.CompletedTask;
        });

    /// <summary>
    /// Clears all browsing data and marks the profile for deletion on disk.
    /// Any live WebView2 using this profile will be closed.
    /// </summary>
    public async Task PermanentlyDeleteProfileAsync(
        string profileName,
        WebView2? activeWebView = null,
        CancellationToken cancellationToken = default)
    {
        profileName = NormalizeProfileName(profileName);
        ValidateProfileName(profileName);

        if (activeWebView is not null)
        {
            await CloseWebViewOnUiThreadAsync(activeWebView).ConfigureAwait(true);
        }

        var ephemeralWebView = await CreateWebViewAsync(profileName, startUrl: null, cancellationToken)
            .ConfigureAwait(true);
        try
        {
            if (ephemeralWebView.CoreWebView2 is not null)
            {
                await WipeProfileOnUiThreadAsync(ephemeralWebView.CoreWebView2.Profile, cancellationToken)
                    .ConfigureAwait(true);
            }
        }
        finally
        {
            await CloseWebViewOnUiThreadAsync(ephemeralWebView).ConfigureAwait(true);
            InstanceWebViewRegistry.Instance.ReleaseProfile(profileName);
        }
    }

    private static Task WipeProfileOnUiThreadAsync(
        CoreWebView2Profile profile,
        CancellationToken cancellationToken) =>
        UiThreadRunner.RunAsync(async () =>
        {
            await WebViewUiAwaiter
                .AwaitAsync(profile.ClearBrowsingDataAsync().AsTask().WaitAsync(cancellationToken))
                .ConfigureAwait(true);
            profile.Delete();
        });

    public static string NormalizeProfileName(string profileName) =>
        profileName.Trim();

    public static bool TryValidateProfileName(string profileName)
    {
        try
        {
            ValidateProfileName(profileName);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static void ValidateProfileName(string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

        profileName = NormalizeProfileName(profileName);

        if (profileName.Length > 64)
        {
            throw new ArgumentException("Profile name must be 64 characters or fewer.", nameof(profileName));
        }

        if (profileName.EndsWith('.') || profileName.EndsWith(' '))
        {
            throw new ArgumentException("Profile name must not end with '.' or a space.", nameof(profileName));
        }

        if (!ProfileNamePattern().IsMatch(profileName))
        {
            throw new ArgumentException(
                "Profile name may only contain letters, digits, and #@$()+-_~. characters.",
                nameof(profileName));
        }
    }

    [GeneratedRegex("^[a-zA-Z0-9#@$()\\+\\-_~\\. ]+$")]
    private static partial Regex ProfileNamePattern();
}
