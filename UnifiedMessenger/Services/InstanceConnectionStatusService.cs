using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed class InstanceConnectionStatusChangedEventArgs(
    string instanceId,
    InstanceConnectionStatus status,
    string? detail) : EventArgs
{
    public string InstanceId { get; } = instanceId;

    public InstanceConnectionStatus Status { get; } = status;

    public string? Detail { get; } = detail;
}

public sealed class InstanceConnectionStatusService
{
    private static readonly Lazy<InstanceConnectionStatusService> LazyInstance =
        new(() => new InstanceConnectionStatusService());

    private readonly Dictionary<string, InstanceConnectionStatusEntry> _states =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly object _gate = new();

    public static InstanceConnectionStatusService Instance => LazyInstance.Value;

    public event EventHandler<InstanceConnectionStatusChangedEventArgs>? Changed;

    public InstanceConnectionStatus GetStatus(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return InstanceConnectionStatus.Initializing;
        }

        lock (_gate)
        {
            return _states.TryGetValue(instanceId.Trim(), out var entry)
                ? entry.Status
                : InstanceConnectionStatus.Initializing;
        }
    }

    public string? GetDetail(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        lock (_gate)
        {
            return _states.TryGetValue(instanceId.Trim(), out var entry) ? entry.Detail : null;
        }
    }

    public void SetInitializing(string instanceId, string? detail = null) =>
        Transition(instanceId, InstanceConnectionStatus.Initializing, detail);

    public void SetLoggedOut(string instanceId, string? detail = null) =>
        Transition(instanceId, InstanceConnectionStatus.LoggedOut, detail);

    public void SetConnected(string instanceId, string? detail = null) =>
        Transition(instanceId, InstanceConnectionStatus.Connected, detail);

    public void SetError(string instanceId, string? detail = null) =>
        Transition(instanceId, InstanceConnectionStatus.Error, detail);

    public void Remove(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        lock (_gate)
        {
            _states.Remove(instanceId.Trim());
        }
    }

    internal static bool IsValidTransition(
        InstanceConnectionStatus current,
        InstanceConnectionStatus next) =>
        current == next ||
        next switch
        {
            InstanceConnectionStatus.Initializing => true,
            InstanceConnectionStatus.Error => true,
            InstanceConnectionStatus.LoggedOut => current != InstanceConnectionStatus.Error,
            InstanceConnectionStatus.Connected => current is InstanceConnectionStatus.Initializing
                or InstanceConnectionStatus.LoggedOut
                or InstanceConnectionStatus.Connected,
            _ => false
        };

    internal static InstanceConnectionStatus ParseStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return InstanceConnectionStatus.Initializing;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "connected" => InstanceConnectionStatus.Connected,
            "loggedout" => InstanceConnectionStatus.LoggedOut,
            "error" => InstanceConnectionStatus.Error,
            "initializing" => InstanceConnectionStatus.Initializing,
            _ => InstanceConnectionStatus.Initializing
        };
    }

    public void ApplyHandshakeStatus(MessengerInstance instance, string? statusRaw, string? detail)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ApplyConnectionStatus(instance.Id, statusRaw, detail);
        instance.Status = GetStatus(instance.Id);
    }

    public void MirrorStatusToInstance(MessengerInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        instance.Status = GetStatus(instance.Id);
    }

    private void Transition(string instanceId, InstanceConnectionStatus status, string? detail)
    {
        if (!ShellNavigationService.IsValidInstanceId(instanceId))
        {
            return;
        }

        var normalizedId = instanceId.Trim();
        InstanceConnectionStatus previous;

        lock (_gate)
        {
            if (!_states.TryGetValue(normalizedId, out var entry))
            {
                entry = new InstanceConnectionStatusEntry();
                _states[normalizedId] = entry;
            }

            previous = entry.Status;
            if (!IsValidTransition(previous, status))
            {
                return;
            }

            entry.Status = status;
            entry.Detail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();
            entry.UpdatedUtc = DateTimeOffset.UtcNow;
        }

        if (previous != status)
        {
            Changed?.Invoke(this, new InstanceConnectionStatusChangedEventArgs(normalizedId, status, detail));
        }
    }

    internal void ApplyConnectionStatus(string instanceId, string? statusRaw, string? detail)
    {
        var status = ParseStatus(statusRaw);
        switch (status)
        {
            case InstanceConnectionStatus.Connected:
                SetConnected(instanceId, detail);
                break;
            case InstanceConnectionStatus.LoggedOut:
                SetLoggedOut(instanceId, detail);
                break;
            case InstanceConnectionStatus.Error:
                SetError(instanceId, detail);
                break;
            default:
                SetInitializing(instanceId, detail);
                break;
        }
    }

    private sealed class InstanceConnectionStatusEntry
    {
        public InstanceConnectionStatus Status { get; set; } = InstanceConnectionStatus.Initializing;

        public string? Detail { get; set; }

        public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}
