namespace UnifiedMessenger.Models;

/// <summary>
/// Per-location (workspace) configuration for the Professional scope: a display name, an optional
/// SLA threshold override, and optional business hours. Keyed by the branch/location key that
/// <see cref="ThreadData.BranchName"/> resolves to. Persisted in settings.json. When no profile
/// exists for a location, the app falls back to the global SLA threshold and a 24/7 clock, so this
/// is fully backward-compatible.
/// </summary>
public sealed class WorkspaceProfile
{
    public string LocationKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Per-location SLA threshold in minutes. <c>null</c> uses the global threshold.</summary>
    public int? SlaThresholdMinutes { get; set; }

    public BusinessHours Hours { get; set; } = new();

    public void Normalize()
    {
        LocationKey = LocationKey?.Trim() ?? string.Empty;
        DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? LocationKey : DisplayName.Trim();
        if (SlaThresholdMinutes is int m)
        {
            SlaThresholdMinutes = Math.Clamp(m, AppSettings.MinSlaThresholdMinutes, AppSettings.MaxSlaThresholdMinutes);
        }

        Hours ??= new BusinessHours();
        Hours.Normalize();
    }
}

/// <summary>
/// Working-hours window for a location, expressed in local (oversight-machine) time. The SLA clock
/// only counts time inside these hours on working days; outside them it pauses (standard support-SLA
/// behaviour). Minutes are minutes-from-midnight (e.g. 540 = 09:00). Working days use
/// <see cref="System.DayOfWeek"/> integers (0 = Sunday … 6 = Saturday).
/// </summary>
public sealed class BusinessHours
{
    public bool Enabled { get; set; }

    public int OpenMinutes { get; set; } = 9 * 60;

    public int CloseMinutes { get; set; } = 18 * 60;

    public List<int> WorkingDays { get; set; } = [1, 2, 3, 4, 5, 6];

    public void Normalize()
    {
        OpenMinutes = Math.Clamp(OpenMinutes, 0, 24 * 60);
        CloseMinutes = Math.Clamp(CloseMinutes, 0, 24 * 60);
        WorkingDays = (WorkingDays ?? [])
            .Where(d => d is >= 0 and <= 6)
            .Distinct()
            .ToList();
        if (WorkingDays.Count == 0)
        {
            WorkingDays = [1, 2, 3, 4, 5, 6];
        }
    }
}
