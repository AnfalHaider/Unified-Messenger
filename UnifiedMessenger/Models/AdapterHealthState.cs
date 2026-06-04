using System.Text.Json.Serialization;

namespace UnifiedMessenger.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
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

    public string Description => GetDescription(State);

    public void Normalize()
    {
        if (!Enum.IsDefined(State))
        {
            State = AdapterHealthState.Unknown;
        }

        AdapterId = string.IsNullOrWhiteSpace(AdapterId) ? null : AdapterId.Trim();
    }

    public AdapterHealthStatus Clone() =>
        new()
        {
            State = State,
            AdapterId = AdapterId,
            LastHeartbeat = LastHeartbeat
        };

    public static string GetDescription(AdapterHealthState state) =>
        state switch
        {
            AdapterHealthState.Ready => "Adapter loaded, waiting for first heartbeat",
            AdapterHealthState.Healthy => "Monitoring active",
            AdapterHealthState.Stale => "No recent heartbeat — page may be idle or logged out",
            AdapterHealthState.NoAdapter => "No adapter for this platform",
            _ => "Initializing..."
        };
}
