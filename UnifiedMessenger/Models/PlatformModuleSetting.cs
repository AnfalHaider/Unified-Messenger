namespace UnifiedMessenger.Models;

/// <summary>
/// Per-platform module enablement persisted in <c>settings.json</c>.
/// </summary>
public sealed class PlatformModuleSetting
{
    public string PlatformId { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;
}
