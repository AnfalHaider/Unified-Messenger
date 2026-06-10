namespace UnifiedMessenger.Services;

/// <summary>
/// Lightweight bridge so pages like Dashboard can request shell navigation without tight coupling to MainWindow.
/// </summary>
public sealed class ShellNavigationService : INavigationService
{
    private static readonly Lazy<ShellNavigationService> LazyInstance = new(() => new ShellNavigationService());

    public static ShellNavigationService Instance => LazyInstance.Value;

    public event EventHandler<InstanceNavigationRequest>? InstanceNavigationRequested;

    public event EventHandler? DashboardRefreshRequested;

    public event EventHandler<string>? ArchivedInstanceRestoreRequested;

    public event EventHandler? LayoutRefreshRequested;

    public event EventHandler? InstanceRegistryRefreshRequested;

    public event EventHandler? AddInstanceRequested;

    public event EventHandler<string?>? OccBranchFilterRequested;

    public event EventHandler? OccImmediateLaneFocusRequested;

    public event EventHandler? OccSnapshotExportRequested;

    internal static ShellNavigationService CreateForTests() => new();

    internal static bool IsValidInstanceId(string? instanceId) =>
        !string.IsNullOrWhiteSpace(instanceId);

    public void RequestInstance(string instanceId) =>
        OpenInstance(instanceId);

    public void RequestInstance(string instanceId, string? conversationKey, string? customerName = null) =>
        OpenInstance(instanceId, conversationKey, customerName);

    public void OpenInstance(string instanceId) =>
        OpenInstance(instanceId, conversationKey: null, customerName: null);

    public void OpenInstance(string instanceId, string? conversationKey, string? customerName = null)
    {
        if (!IsValidInstanceId(instanceId))
        {
            return;
        }

        InstanceNavigationRequested?.Invoke(this, new InstanceNavigationRequest
        {
            InstanceId = instanceId.Trim(),
            ConversationKey = string.IsNullOrWhiteSpace(conversationKey) ? null : conversationKey.Trim(),
            CustomerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim()
        });
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

    public void RequestAddInstance()
    {
        AddInstanceRequested?.Invoke(this, EventArgs.Empty);
    }

    public void RequestOccBranchFilter(string? branchKey) =>
        OccBranchFilterRequested?.Invoke(this, branchKey);

    public void RequestOccImmediateLaneFocus() =>
        OccImmediateLaneFocusRequested?.Invoke(this, EventArgs.Empty);

    public void RequestOccSnapshotExport() =>
        OccSnapshotExportRequested?.Invoke(this, EventArgs.Empty);
}
