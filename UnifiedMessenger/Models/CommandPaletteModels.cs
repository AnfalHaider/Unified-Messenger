namespace UnifiedMessenger.Models;

public sealed class CommandPaletteEntry
{
    public required string Title { get; init; }

    public required string Subtitle { get; init; }

    public required string Category { get; init; }

    public required CommandPaletteSelection Selection { get; init; }

    public string IconGlyph { get; init; } = "\uE721";
}

public sealed class CommandPaletteSelection
{
    public CommandPaletteAction Action { get; init; }

    public string? InstanceId { get; init; }

    public string? AlertId { get; init; }

    public string? BranchKey { get; init; }

    public string? SettingsSectionKey { get; init; }
}

public enum CommandPaletteAction
{
    OpenInstance,
    OpenDashboard,
    OpenSettings,
    OpenSettingsSection,
    OpenAlert,
    ToggleNotifications,
    ClearNotifications,
    MarkAllRead,
    RefreshOcc,
    FilterBranch,
    OpenImmediateQueue
}
