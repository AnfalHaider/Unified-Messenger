using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class WorkspaceSidebarHelper
{
    public const int MaxBadgeValue = 99;

    public const string DashboardSelectionKey = "dashboard";

    public const string AnalyticsSelectionKey = "analytics";

    public const string ReviewsSelectionKey = "reviews";

    public const string ReportsSelectionKey = "reports";

    public const string SettingsSelectionKey = "settings";

    public const string NotificationHubSelectionKey = "notifications";

    /// <summary>The sidebar row key for a navigable section.</summary>
    public static string SectionSelectionKey(ShellSection section) => section switch
    {
        ShellSection.Analytics => AnalyticsSelectionKey,
        ShellSection.Reviews => ReviewsSelectionKey,
        ShellSection.Reports => ReportsSelectionKey,
        ShellSection.Settings => SettingsSelectionKey,
        _ => DashboardSelectionKey
    };

    /// <summary>Parses a persisted section key back to a <see cref="ShellSection"/>, defaulting to Dashboard.</summary>
    public static ShellSection ParseSection(string? key) => (key ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        AnalyticsSelectionKey => ShellSection.Analytics,
        ReviewsSelectionKey => ShellSection.Reviews,
        ReportsSelectionKey => ShellSection.Reports,
        SettingsSelectionKey => ShellSection.Settings,
        _ => ShellSection.Dashboard
    };

    /// <summary>
    /// Which sidebar row should render as selected. The notification dock wins when open (it overlays the
    /// current destination without replacing it); otherwise an open account beats the section, because the
    /// account's WebView is what is actually on screen.
    /// </summary>
    public static string ResolveSelectionKey(
        ShellSection section,
        string? instanceId,
        bool notificationHubSelected = false)
    {
        if (notificationHubSelected)
        {
            return NotificationHubSelectionKey;
        }

        if (!string.IsNullOrWhiteSpace(instanceId))
        {
            return instanceId.Trim();
        }

        return SectionSelectionKey(section);
    }

    public static bool IsSelectionMatch(string? selectedKey, string rowKey) =>
        string.Equals(selectedKey, rowKey, StringComparison.OrdinalIgnoreCase);

    public static int ClampBadgeCount(int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        return count > MaxBadgeValue ? MaxBadgeValue : count;
    }

    public static (IReadOnlyList<MessengerInstance> Professional, IReadOnlyList<MessengerInstance> Personal)
        PartitionInstances(IEnumerable<MessengerInstance> instances)
    {
        ArgumentNullException.ThrowIfNull(instances);

        var validInstances = instances
            .Where(instance => !string.IsNullOrWhiteSpace(instance.Id))
            .GroupBy(instance => instance.Id.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        var professional = validInstances
            .Where(instance => instance.IsProfessional)
            .OrderBy(instance => instance.SortOrder)
            .ThenBy(instance => instance.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var personal = validInstances
            .Where(instance => !instance.IsProfessional)
            .OrderBy(instance => instance.SortOrder)
            .ThenBy(instance => instance.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (professional, personal);
    }

    public static string FormatMemoryTierLabel(MemoryTierPreference tier) =>
        tier switch
        {
            MemoryTierPreference.Low => "Low",
            MemoryTierPreference.High => "High",
            _ => "Normal"
        };

    public static string AppendMemoryTierHint(string subtitle, MemoryTierPreference tier)
    {
        ArgumentNullException.ThrowIfNull(subtitle);

        if (tier == MemoryTierPreference.Normal)
        {
            return subtitle;
        }

        return $"{subtitle} · Memory: {FormatMemoryTierLabel(tier)}";
    }

    public static string ComposeInstanceTooltip(
        string displayName,
        WorkspaceCategory category,
        string statusSubtitle,
        string adapterDescription,
        MemoryTierPreference memoryTier,
        string? connectionDetail = null)
    {
        var detailLine = string.IsNullOrWhiteSpace(connectionDetail) ? string.Empty : $"\n{connectionDetail}";
        return
            $"{displayName}\nWorkspace: {category}\n{statusSubtitle}{detailLine}\nMemory tier: {FormatMemoryTierLabel(memoryTier)}\nAdapter: {adapterDescription}";
    }

    public static string ResolveStatusSubtitle(
        InstanceConnectionStatus connectionStatus,
        AdapterHealthState adapterState,
        bool notificationsMuted,
        string? connectionDetail = null,
        ReconnectState reconnect = ReconnectState.None)
    {
        if (notificationsMuted)
        {
            return "Notifications muted";
        }

        if (connectionStatus == InstanceConnectionStatus.Connected)
        {
            return connectionStatus switch
            {
                _ when adapterState == AdapterHealthState.Healthy => "Connected",
                _ => "Connected · syncing"
            };
        }

        return connectionStatus switch
        {
            InstanceConnectionStatus.LoggedOut => "Signed out",
            // `connectionDetail` was accepted and then never read, so every failure — no internet, a bad
            // certificate, a proxy — collapsed to the same three words with no way to tell them apart.
            // The describer returns null for details that are not error codes, and those stay behind the
            // generic label rather than putting raw text in the rail.
            //
            // `reconnect` overrides it because the raw status is not stable across a retry: reloading
            // cancels the in-flight navigation and reports a status the describer does not recognise, so
            // an account that correctly read "No internet" reverted to the generic label the instant the
            // first retry fired.
            InstanceConnectionStatus.Error when reconnect != ReconnectState.None =>
                NetworkFailureDescriber.AccountOffline,
            InstanceConnectionStatus.Error =>
                NetworkFailureDescriber.DescribeWebViewStatus(connectionDetail) ?? "Connection error",
            InstanceConnectionStatus.Initializing => "Connecting…",
            _ => "Connecting…"
        };
    }

    /// <summary>
    /// The row subtitle for the redesigned sidebar: the channel/platform name when the account is healthy
    /// (far more useful at a glance than a repeated "Connected · syncing"), and the problem state only when
    /// there is one to surface (signed out, connection error, muted). Transient connecting/syncing is left to
    /// the status dot's colour.
    /// </summary>
    public static string ComposeRowSubtitle(
        string? platformId,
        InstanceConnectionStatus connectionStatus,
        bool notificationsMuted,
        string? connectionDetail = null,
        ReconnectState reconnect = ReconnectState.None)
    {
        if (notificationsMuted)
        {
            return "Notifications muted";
        }

        if (connectionStatus == InstanceConnectionStatus.LoggedOut)
        {
            return "Signed out — tap to reconnect";
        }

        if (connectionStatus == InstanceConnectionStatus.Error)
        {
            // "Connection error" is what a UI Automation capture of the live app read back for both
            // WhatsApp accounts while their web clients could not reach the network — accurate, and
            // useless. It does not say the machine is offline, and unlike the signed-out case beside it
            // ("tap to reconnect") it offers nothing to do. The offline wording says which of the two it
            // is, and that the app is retrying on its own so there is nothing to click.
            // A pending reconnect is authoritative — see ResolveStatusSubtitle for why the raw status
            // cannot be trusted once a retry has fired. Once the backoff is exhausted the app has stopped
            // retrying, and must stop saying it is.
            if (reconnect == ReconnectState.Retrying)
            {
                return "No internet — reconnecting…";
            }

            if (reconnect == ReconnectState.GaveUp)
            {
                return "No internet — tap to retry";
            }

            return NetworkFailureDescriber.DescribeWebViewStatus(connectionDetail) switch
            {
                NetworkFailureDescriber.AccountOffline => "No internet — reconnecting…",
                { } described => described,
                _ => "Connection error"
            };
        }

        return PlatformDefinition.FindById(platformId)?.DisplayName ?? "Account";
    }

    internal static string FormatConnectedDetailSubtitle(string connectionDetail)
    {
        var detail = connectionDetail.Trim();
        if (detail.StartsWith("Status:", StringComparison.OrdinalIgnoreCase))
        {
            return detail;
        }

        return detail.StartsWith("Connected", StringComparison.OrdinalIgnoreCase)
            ? $"Status: {detail}"
            : $"Status: Connected · {detail}";
    }

    /// <summary>
    /// The theme token for an account's status dot.
    /// </summary>
    /// <remarks>
    /// This returned nine hardcoded <c>Color.FromArgb</c> literals until now — <c>#107C10</c> green,
    /// <c>#C42B1C</c> red, <c>#0063B1</c> blue, <c>#808080</c> grey — which is exactly the defect the
    /// contrast pass fixed everywhere else in v4.99.26 and then missed here. One shared value cannot serve
    /// both themes: a colour readable on white is not readable on near-black, and the dot is the primary
    /// signal for "is this account working".
    ///
    /// <para>
    /// A token key rather than a <c>Color</c>, because resolving a themed brush needs the element it will be
    /// drawn on — see <see cref="UmSemanticBrushes.Get(string, FrameworkElement?)"/>.
    /// </para>
    /// </remarks>
    public static string ResolveConnectionIndicatorBrushKey(
        InstanceConnectionStatus connectionStatus,
        AdapterHealthState adapterState) =>
        connectionStatus switch
        {
            InstanceConnectionStatus.Connected => UmSemanticBrushes.StatusSuccessBrushKey,
            InstanceConnectionStatus.LoggedOut => UmSemanticBrushes.StatusWarningBrushKey,
            InstanceConnectionStatus.Error => UmSemanticBrushes.StatusDangerBrushKey,
            InstanceConnectionStatus.Initializing => UmSemanticBrushes.StatusInfoBrushKey,
            _ => adapterState switch
            {
                AdapterHealthState.Healthy => UmSemanticBrushes.StatusSuccessBrushKey,
                AdapterHealthState.Ready => UmSemanticBrushes.StatusInfoBrushKey,
                AdapterHealthState.Stale => UmSemanticBrushes.StatusWarningBrushKey,
                AdapterHealthState.NoAdapter => UmSemanticBrushes.StatusNeutralBrushKey,
                _ => UmSemanticBrushes.StatusMutedBrushKey
            }
        };

    /// <summary>
    /// The status-dot brush, resolved for the theme the element is actually drawn in.
    /// </summary>
    /// <remarks>
    /// UI thread only — it creates a <see cref="SolidColorBrush"/>. Background callers that just need the
    /// colour must use <see cref="ResolveConnectionIndicatorHex"/>.
    /// </remarks>
    public static SolidColorBrush ResolveConnectionIndicatorBrush(
        InstanceConnectionStatus connectionStatus,
        AdapterHealthState adapterState,
        FrameworkElement? element = null) =>
        UmSemanticBrushes.Get(
            ResolveConnectionIndicatorBrushKey(connectionStatus, adapterState), element);

    /// <summary>
    /// The status-dot colour as a hex string, safe to call from any thread.
    /// </summary>
    /// <remarks>
    /// Separate from the brush overload because the two have different threading rules and the difference
    /// is invisible at the call site. <c>PersonalDashboardService.BuildSnapshot</c> builds tile data on a
    /// thread-pool thread; routing it through the brush overload read
    /// <c>Application.Current.RequestedTheme</c> off the UI thread and took the whole process down with an
    /// AccessViolationException on launch. This path never touches a WinRT UI object.
    /// </remarks>
    public static string ResolveConnectionIndicatorHex(
        InstanceConnectionStatus connectionStatus,
        AdapterHealthState adapterState) =>
        UmSemanticBrushes.ResolvePaletteHex(
            ResolveConnectionIndicatorBrushKey(connectionStatus, adapterState));
}
