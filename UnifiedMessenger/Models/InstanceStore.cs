namespace UnifiedMessenger.Models;

/// <summary>
/// Root object serialized to instances.json in local app data.
/// </summary>
public sealed class InstanceStore
{
    public const int CurrentVersion = 5;

    public int Version { get; set; } = CurrentVersion;

    public List<MessengerInstance> Instances { get; set; } = [];

    /// <summary>
    /// Instances removed from the sidebar but whose WebView2 profile folders are retained.
    /// </summary>
    public List<MessengerInstance> ArchivedInstances { get; set; } = [];
}
