namespace UnifiedMessenger.ViewModels;

public sealed class ArchivedAccountRowViewModel
{
    public required string InstanceId { get; init; }

    public required string DisplayName { get; init; }

    public required string PlatformLabel { get; init; }

    public required string ProfileLine { get; init; }

    public required string AccentColorHex { get; init; }

    public required string IconGlyph { get; init; }

    /// <summary>
    /// Per-row accessible names, so the buttons say WHICH account they act on.
    /// </summary>
    /// <remarks>
    /// These lists render one identical button per row, so Narrator read "Restore button, Delete
    /// permanently button, Restore button, Delete permanently button…" with nothing to tell them apart —
    /// on an action that permanently destroys an account's data. The visible label can stay short because
    /// the row is visually adjacent; the spoken name cannot rely on that.
    /// </remarks>
    public string RestoreAccessibleName => $"Restore {DisplayName}";

    public string DeletePermanentlyAccessibleName => $"Permanently delete {DisplayName}";

    public string ChangeIconAccessibleName => $"Change icon for {DisplayName}";
}
