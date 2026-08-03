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

    public string? ConversationKey { get; init; }

    public string? CustomerName { get; init; }

    /// <summary>Target for <see cref="CommandPaletteAction.OpenSection"/>.</summary>
    public ShellSection? Section { get; init; }
}

public enum CommandPaletteAction
{
    OpenInstance,
    OpenDashboard,

    /// <summary>Navigate to a left-nav section; the target is <c>CommandPaletteSelection.Section</c>.</summary>
    OpenSection,

    OpenSettings,
    OpenSettingsSection,
    OpenAlert,
    ToggleNotifications,
    ClearNotifications,
    MarkAllRead,
    RefreshOcc,
    FilterBranch,
    OpenImmediateQueue,
    OpenThread,
    ManageWorkspaces
}
