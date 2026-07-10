using Microsoft.UI.Xaml.Controls;

namespace UnifiedMessenger.Services;

/// <summary>
/// Tracks live WebView2 controls keyed by instance id and enforces one profile per instance.
/// </summary>
public sealed class InstanceWebViewRegistry
{
    private static readonly Lazy<InstanceWebViewRegistry> LazyInstance = new(() => new InstanceWebViewRegistry());

    private readonly object _gate = new();
    private readonly Dictionary<string, WebView2> _webViews = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _instanceProfiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _profileOwners = new(StringComparer.OrdinalIgnoreCase);

    public static InstanceWebViewRegistry Instance => LazyInstance.Value;

    internal InstanceWebViewRegistry()
    {
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _webViews.Count;
            }
        }
    }

    public IEnumerable<WebView2> All
    {
        get
        {
            lock (_gate)
            {
                return _webViews.Values.ToList();
            }
        }
    }

    public void Register(string instanceId, string profileName, WebView2 webView)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        ArgumentNullException.ThrowIfNull(webView);

        profileName = WebViewProfileManager.NormalizeProfileName(profileName);
        WebViewProfileManager.ValidateProfileName(profileName);

        lock (_gate)
        {
            ClaimProfileOwnership(instanceId, profileName);

            if (_webViews.TryGetValue(instanceId, out var existing) && !ReferenceEquals(existing, webView))
            {
                throw new InvalidOperationException(
                    $"Instance \"{instanceId}\" already has a registered WebView. Unregister it before replacing.");
            }

            _webViews[instanceId] = webView;
        }
    }

    internal void TrackProfileForTests(string instanceId, string profileName)
    {
        profileName = WebViewProfileManager.NormalizeProfileName(profileName);
        WebViewProfileManager.ValidateProfileName(profileName);

        lock (_gate)
        {
            ClaimProfileOwnership(instanceId, profileName);
        }
    }

    private void ClaimProfileOwnership(string instanceId, string profileName)
    {
        if (_profileOwners.TryGetValue(profileName, out var ownerId) &&
            !ownerId.Equals(instanceId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Profile \"{profileName}\" is already assigned to instance \"{ownerId}\".");
        }

        _instanceProfiles[instanceId] = profileName;
        _profileOwners[profileName] = instanceId;
    }

    public void Unregister(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        lock (_gate)
        {
            _webViews.Remove(instanceId);

            if (_instanceProfiles.Remove(instanceId, out var profileName) &&
                _profileOwners.TryGetValue(profileName, out var ownerId) &&
                ownerId.Equals(instanceId, StringComparison.OrdinalIgnoreCase))
            {
                _profileOwners.Remove(profileName);
            }
        }
    }

    public void ReleaseProfile(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return;
        }

        profileName = WebViewProfileManager.NormalizeProfileName(profileName);

        lock (_gate)
        {
            if (!_profileOwners.TryGetValue(profileName, out var ownerId))
            {
                return;
            }

            _profileOwners.Remove(profileName);
            _instanceProfiles.Remove(ownerId);
            _webViews.Remove(ownerId);
        }
    }

    public bool IsProfileOwnedByOther(string profileName, string instanceId)
    {
        profileName = WebViewProfileManager.NormalizeProfileName(profileName);

        lock (_gate)
        {
            return _profileOwners.TryGetValue(profileName, out var ownerId) &&
                   !ownerId.Equals(instanceId, StringComparison.OrdinalIgnoreCase);
        }
    }

    public string? GetProfileName(string instanceId)
    {
        lock (_gate)
        {
            return _instanceProfiles.TryGetValue(instanceId, out var profileName)
                ? profileName
                : null;
        }
    }

    public string? GetOwnerInstanceId(string profileName)
    {
        profileName = WebViewProfileManager.NormalizeProfileName(profileName);

        lock (_gate)
        {
            return _profileOwners.TryGetValue(profileName, out var ownerId)
                ? ownerId
                : null;
        }
    }

    public WebView2? TryGet(string instanceId)
    {
        lock (_gate)
        {
            _webViews.TryGetValue(instanceId, out var webView);
            return webView;
        }
    }

    public bool Contains(string instanceId)
    {
        lock (_gate)
        {
            return _webViews.ContainsKey(instanceId);
        }
    }
}
