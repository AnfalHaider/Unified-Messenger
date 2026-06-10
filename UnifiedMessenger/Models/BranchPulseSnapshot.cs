namespace UnifiedMessenger.Models;

public enum BranchPulseState
{
    Idle,
    Generating,
    Ready,
    Disabled,
    Unavailable,
    NoThreads,
    Error
}

public sealed class BranchPulseSnapshot
{
    public static BranchPulseSnapshot Disabled { get; } = new()
    {
        State = BranchPulseState.Disabled,
        StatusMessage = "Enable Local AI and Branch pulse in Settings to generate summaries."
    };

    public string ScopeLabel { get; init; } = "All branches";

    public string? BranchKey { get; init; }

    public int OpenThreadCount { get; init; }

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> Themes { get; init; } = [];

    public DateTimeOffset? GeneratedAtUtc { get; init; }

    public BranchPulseState State { get; init; } = BranchPulseState.Idle;

    public string StatusMessage { get; init; } = string.Empty;
}
