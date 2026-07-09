namespace UnifiedMessenger.Services;

/// <summary>
/// Tracks the instance WebView currently shown in the main shell (not dashboard/settings).
/// </summary>
public static class ActiveWorkspaceContext
{
    private static readonly object Gate = new();

    private static string? _currentInstanceId;

    private static bool _isDashboardVisible;

    public static event EventHandler? Changed;

    public static string? CurrentInstanceId
    {
        get
        {
            lock (Gate)
            {
                return _currentInstanceId;
            }
        }
    }

    public static bool IsDashboardVisible
    {
        get
        {
            lock (Gate)
            {
                return _isDashboardVisible;
            }
        }
    }

    public static void SetDashboardVisible()
    {
        lock (Gate)
        {
            _currentInstanceId = null;
            _isDashboardVisible = true;
        }

        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static void SetSettingsVisible()
    {
        lock (Gate)
        {
            _currentInstanceId = null;
            _isDashboardVisible = false;
        }

        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static void SetActiveInstance(string? instanceId)
    {
        lock (Gate)
        {
            _currentInstanceId = string.IsNullOrWhiteSpace(instanceId) ? null : instanceId.Trim();
            _isDashboardVisible = false;
        }

        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static bool IsInstanceActive(string instanceId) =>
        !string.IsNullOrWhiteSpace(instanceId) &&
        string.Equals(CurrentInstanceId, instanceId.Trim(), StringComparison.OrdinalIgnoreCase);
}
