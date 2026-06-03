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

    public void RequestInstance(string instanceId)
    {
        InstanceLaunchRequested?.Invoke(this, instanceId);
    }

    public void RequestDashboardRefresh()
    {
        DashboardRefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    public void RequestArchivedInstanceRestore(string instanceId)
    {
        ArchivedInstanceRestoreRequested?.Invoke(this, instanceId);
    }
}
