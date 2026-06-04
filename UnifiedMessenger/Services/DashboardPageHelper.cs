using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class DashboardPageHelper
{
    public const int ResourceRefreshIntervalSeconds = 30;

    public const int MaxSearchSuggestions = 6;

    public const string AllBranchesOptionId = "";

    public static IReadOnlyList<DashboardBranchOption> BuildBranchOptions(
        IEnumerable<MessengerInstance> professionalInstances)
    {
        var options = new List<DashboardBranchOption>
        {
            new(AllBranchesOptionId, "All Branches")
        };

        foreach (var instance in professionalInstances
                     .Where(instance => !string.IsNullOrWhiteSpace(instance.Id))
                     .OrderBy(instance => instance.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            options.Add(new DashboardBranchOption(instance.Id.Trim(), instance.DisplayName.Trim()));
        }

        return options;
    }

    public static IEnumerable<MessengerInstance> FilterProfessionalInstances(
        IEnumerable<MessengerInstance> professionalInstances,
        string? selectedBranchInstanceId)
    {
        ArgumentNullException.ThrowIfNull(professionalInstances);

        if (string.IsNullOrWhiteSpace(selectedBranchInstanceId))
        {
            return professionalInstances;
        }

        return professionalInstances.Where(instance =>
            instance.Id.Equals(selectedBranchInstanceId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static string BuildWelcomeSubtitle(int professionalCount, int personalCount) =>
        (professionalCount, personalCount) switch
        {
            (0, 0) => "Add an account to start receiving unified notifications.",
            ( > 0, > 0) => $"{professionalCount} professional and {personalCount} personal accounts connected.",
            ( > 0, 0) => $"{professionalCount} professional account{(professionalCount == 1 ? "" : "s")} connected.",
            _ => $"{personalCount} personal account{(personalCount == 1 ? "" : "s")} connected."
        };

    public static string FormatUnrepliedReviewCount(int count) =>
        count == 1 ? "1 unreplied review" : $"{count} unreplied reviews";

    public static string BuildInstanceStatusLine(InstanceResourceTile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        var parts = new List<string>();
        if (tile.IsVisible)
        {
            parts.Add("Visible");
        }

        parts.Add(tile.MemoryTier);
        if (tile.UnreadCount > 0)
        {
            parts.Add($"{tile.UnreadCount} unread");
        }

        parts.Add(tile.HealthState.ToString());
        return string.Join(" · ", parts);
    }

    public static bool ActivityMatches(
        string title,
        string body,
        string instanceDisplayName,
        string? query)
    {
        query = CommandPaletteHelper.NormalizeQuery(query);
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || body.Contains(query, StringComparison.OrdinalIgnoreCase)
            || instanceDisplayName.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    public static string ResolveEmptyActivityMessage(bool hasSearchQuery) =>
        hasSearchQuery
            ? "No personal activity matches your search."
            : "No recent notifications from personal accounts.";

    public static IReadOnlyList<DashboardSearchMatch> FilterPersonalSearchMatches(
        IEnumerable<MessengerInstance> personalInstances,
        string? query,
        int maxResults = MaxSearchSuggestions)
    {
        ArgumentNullException.ThrowIfNull(personalInstances);

        query = CommandPaletteHelper.NormalizeQuery(query);
        if (string.IsNullOrWhiteSpace(query) || maxResults <= 0)
        {
            return [];
        }

        var matches = new List<DashboardSearchMatch>();
        foreach (var instance in personalInstances)
        {
            if (string.IsNullOrWhiteSpace(instance.Id))
            {
                continue;
            }

            var platform = PlatformDefinition.FindById(instance.Platform);
            var platformLabel = platform?.DisplayName ?? instance.Platform;
            if (!instance.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !platformLabel.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matches.Add(new DashboardSearchMatch(
                instance.Id.Trim(),
                instance.DisplayName,
                platformLabel,
                instance.AccentColor));

            if (matches.Count >= maxResults)
            {
                break;
            }
        }

        return matches;
    }
}

public readonly record struct DashboardSearchMatch(
    string InstanceId,
    string Label,
    string SubLabel,
    string AccentColorHex);

public readonly record struct DashboardBranchOption(string InstanceId, string DisplayName);
