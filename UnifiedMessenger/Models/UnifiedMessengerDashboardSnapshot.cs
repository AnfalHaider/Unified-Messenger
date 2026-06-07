namespace UnifiedMessenger.Models;

public sealed class UnifiedMessengerDashboardSnapshot
{
    public static UnifiedMessengerDashboardSnapshot Empty { get; } = new();

    public IReadOnlyList<UnifiedMessengerBranchMetrics> BranchMetrics { get; init; } = [];

    public double TotalRevenueAtRisk { get; init; }

    public IReadOnlyList<UnifiedMessengerPlatformHealthIndicator> PlatformHealth { get; init; } = [];

    public IReadOnlyList<ThreadData> ImmediateActionQueue { get; init; } = [];

    public IReadOnlyList<ThreadData> AllThreads { get; init; } = [];

    public IReadOnlyList<string> BranchNames { get; init; } = [];

    public int OpenThreadCount { get; init; }

    public int HangingLeadCount { get; init; }

    public int ImmediateActionCount { get; init; }
}

public sealed class UnifiedMessengerBranchMetrics
{
    public required string BranchName { get; init; }

    public double AverageLatencyMinutes { get; init; }

    public int UnresolvedCount { get; init; }

    public double RevenueAtRisk { get; init; }

    public string LatencyColor { get; init; } = "Green";

    public int InboxCount { get; init; }

    public int SlaBreachCount { get; init; }

    public string PlatformBreakdown { get; init; } = string.Empty;
}

public sealed class UnifiedMessengerPlatformHealthIndicator
{
    public required string PlatformId { get; init; }

    public required string DisplayName { get; init; }

    public bool IsSynced { get; init; }

    public string StatusText { get; init; } = "Awaiting data";
}
