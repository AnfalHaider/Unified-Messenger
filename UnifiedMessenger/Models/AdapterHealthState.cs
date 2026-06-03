namespace UnifiedMessenger.Models;

public enum AdapterHealthState
{
    Unknown,
    Ready,
    Healthy,
    Stale,
    NoAdapter
}

public sealed class AdapterHealthStatus
{
    public AdapterHealthState State { get; set; } = AdapterHealthState.Unknown;

    public string? AdapterId { get; set; }

    public DateTimeOffset? LastHeartbeat { get; set; }

    public string Description => State switch
    {
        AdapterHealthState.Ready => "Adapter loaded, waiting for first heartbeat",
        AdapterHealthState.Healthy => "Monitoring active",
        AdapterHealthState.Stale => "No recent heartbeat — page may be idle or logged out",
        AdapterHealthState.NoAdapter => "No adapter for this platform",
        _ => "Initializing..."
    };
}
