namespace UnifiedMessenger.Services;

/// <summary>
/// Lightweight bridge so pages like Dashboard can request shell navigation without tight coupling to MainWindow.
/// </summary>
public sealed class ShellNavigationService
{
    private static readonly Lazy<ShellNavigationService> LazyInstance = new(() => new ShellNavigationService());

    public static ShellNavigationService Instance => LazyInstance.Value;

    public event EventHandler<string>? InstanceLaunchRequested;

    public event EventHandler? DashboardRefreshRequested;

    public event EventHandler<string>? ArchivedInstanceRestoreRequested;

    public event EventHandler? LayoutRefreshRequested;

    public event EventHandler? InstanceRegistryRefreshRequested;

    internal static ShellNavigationService CreateForTests() => new();

    internal static bool IsValidInstanceId(string? instanceId) =>
        !string.IsNullOrWhiteSpace(instanceId);

    public void RequestInstance(string instanceId)
    {
        if (!IsValidInstanceId(instanceId))
        {
            return;
        }

        InstanceLaunchRequested?.Invoke(this, instanceId.Trim());
    }

    public void RequestDashboardRefresh()
    {
        DashboardRefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    public void RequestArchivedInstanceRestore(string instanceId)
    {
        if (!IsValidInstanceId(instanceId))
        {
            return;
        }

        ArchivedInstanceRestoreRequested?.Invoke(this, instanceId.Trim());
    }

    public void RequestLayoutRefresh()
    {
        LayoutRefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    public void RequestInstanceRegistryRefresh()
    {
        InstanceRegistryRefreshRequested?.Invoke(this, EventArgs.Empty);
    }
}
