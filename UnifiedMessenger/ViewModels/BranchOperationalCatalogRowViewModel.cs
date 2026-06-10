namespace UnifiedMessenger.ViewModels;

public sealed class BranchOperationalCatalogRowViewModel
{
    public required string BranchKey { get; init; }

    public string ServicesText { get; set; } = string.Empty;

    public string StandardPackagesText { get; set; } = string.Empty;

    public string BookingRulesText { get; set; } = string.Empty;
}
