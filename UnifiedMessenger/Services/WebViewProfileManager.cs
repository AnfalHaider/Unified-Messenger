using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace UnifiedMessenger.Services;

/// <summary>
/// Singleton manager that owns one shared <see cref="CoreWebView2Environment"/> (single UDF, single browser process)
/// and creates <see cref="WebView2"/> controls bound to isolated profile names.
/// </summary>
public sealed partial class WebViewProfileManager
{
    private static readonly Lazy<WebViewProfileManager> LazyInstance = new(() => new WebViewProfileManager());

    private readonly SemaphoreSlim _environmentLock = new(1, 1);

    private CoreWebView2Environment? _sharedEnvironment;

    private WebViewProfileManager()
    {
        UserDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UnifiedMessenger",
            "WebView2");
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

            _sharedEnvironment = await CoreWebView2Environment
                .CreateWithOptionsAsync(
                    browserExecutableFolder: null,
                    userDataFolder: UserDataFolder,
                    options: null)
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
    public async Task<WebView2> CreateWebViewAsync(
        string profileName,
        CancellationToken cancellationToken = default)
    {
        profileName = NormalizeProfileName(profileName);
        ValidateProfileName(profileName);

        // Do not ConfigureAwait(false) here — WebView2 is a UI control and must stay on the UI thread.
        var environment = await EnsureEnvironmentAsync(cancellationToken);

        WebView2? webView = null;
        try
        {
            webView = new WebView2();
            var options = environment.CreateCoreWebView2ControllerOptions();
            options.ProfileName = profileName;

            await webView.EnsureCoreWebView2Async(environment, options);

            var actualProfile = webView.CoreWebView2?.Profile.ProfileName;
            if (actualProfile is null ||
                !actualProfile.Equals(profileName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Profile mismatch. Expected \"{profileName}\" but got \"{actualProfile ?? "null"}\".");
            }

            return webView;
        }
        catch
        {
            webView?.Close();
            throw;
        }
    }

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

        if (activeWebView?.CoreWebView2 is not null)
        {
            await WipeProfileAsync(activeWebView.CoreWebView2.Profile, cancellationToken);
            activeWebView.Close();
            InstanceWebViewRegistry.Instance.ReleaseProfile(profileName);
            return;
        }

        var ephemeralWebView = await CreateWebViewAsync(profileName, cancellationToken);
        try
        {
            if (ephemeralWebView.CoreWebView2 is not null)
            {
                await WipeProfileAsync(ephemeralWebView.CoreWebView2.Profile, cancellationToken);
            }
        }
        finally
        {
            ephemeralWebView.Close();
            InstanceWebViewRegistry.Instance.ReleaseProfile(profileName);
        }
    }

    private static async Task WipeProfileAsync(
        CoreWebView2Profile profile,
        CancellationToken cancellationToken)
    {
        await profile.ClearBrowsingDataAsync().AsTask().WaitAsync(cancellationToken);
        profile.Delete();
    }

    /// <summary>
    /// Applies background memory policy across active WebView instances.
    /// Uses <see cref="CoreWebView2.MemoryUsageTargetLevel"/> so WebSockets and scripts keep running.
    /// </summary>
    public void ApplyBackgroundMemoryPolicy(IEnumerable<WebView2> webViews, bool isBackground)
    {
        var targetLevel = isBackground
            ? CoreWebView2MemoryUsageTargetLevel.Low
            : CoreWebView2MemoryUsageTargetLevel.Normal;

        foreach (var webView in webViews)
        {
            if (webView.CoreWebView2 is null)
            {
                continue;
            }

            webView.Visibility = isBackground
                ? Microsoft.UI.Xaml.Visibility.Collapsed
                : Microsoft.UI.Xaml.Visibility.Visible;

            webView.CoreWebView2.MemoryUsageTargetLevel = targetLevel;
        }
    }

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
