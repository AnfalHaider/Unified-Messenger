namespace UnifiedMessenger.Models;

/// <summary>
/// Root object serialized to instances.json in local app data.
/// </summary>
public sealed class InstanceStore
{
    public int Version { get; set; } = 3;

    public List<MessengerInstance> Instances { get; set; } = [];

    /// <summary>
    /// Instances removed from the sidebar but whose WebView2 profile folders are retained.
    /// </summary>
    public List<MessengerInstance> ArchivedInstances { get; set; } = [];
}
