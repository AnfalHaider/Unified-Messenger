namespace UnifiedMessenger.ViewModels;

public sealed class ArchivedAccountRowViewModel
{
    public required string InstanceId { get; init; }

    public required string DisplayName { get; init; }

    public required string PlatformLabel { get; init; }

    public required string ProfileLine { get; init; }

    public required string AccentColorHex { get; init; }

    public required string IconGlyph { get; init; }
}
