using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class CommandPaletteHelper
{
    public const int DefaultMaxResults = 12;

    public static string NormalizeQuery(string? query) => query?.Trim() ?? string.Empty;

    public static IReadOnlyList<CommandPaletteEntry> FilterEntries(
        IReadOnlyList<CommandPaletteEntry> entries,
        string? query,
        int maxResults = DefaultMaxResults)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (maxResults <= 0)
        {
            return [];
        }

        var normalizedQuery = NormalizeQuery(query);
        IEnumerable<CommandPaletteEntry> filtered = entries;

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            filtered = entries
                .Where(entry => EntryMatches(entry, normalizedQuery))
                .OrderByDescending(entry => ScoreEntry(entry, normalizedQuery))
                .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase);
        }

        return filtered.Take(maxResults).ToList();
    }

    private const int FuzzyMatchMinimumScore = 12;

    public static bool EntryMatches(CommandPaletteEntry entry, string query)
    {
        ArgumentNullException.ThrowIfNull(entry);
        query = NormalizeQuery(query);

        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return ScoreEntry(entry, query) >= FuzzyMatchMinimumScore;
    }

    public static int ScoreEntry(CommandPaletteEntry entry, string query)
    {
        ArgumentNullException.ThrowIfNull(entry);
        query = NormalizeQuery(query);

        if (string.IsNullOrWhiteSpace(query))
        {
            return 0;
        }

        var titleScore = ScoreText(entry.Title, query);
        var subtitleScore = ScoreText(entry.Subtitle, query) / 2;
        var categoryScore = ScoreText(entry.Category, query) / 4;
        return Math.Max(titleScore, Math.Max(subtitleScore, categoryScore));
    }

    internal static int ScoreText(string text, string query)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(query))
        {
            return 0;
        }

        if (text.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        if (text.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 50;
        }

        if (IsCharacterSubsequence(text, query))
        {
            return 30 + query.Length;
        }

        var distance = ComputeLevenshteinDistance(text, query);
        var maxDistance = query.Length <= 3 ? 1 : query.Length <= 6 ? 2 : 3;
        if (distance <= maxDistance)
        {
            return 20 - distance;
        }

        return 0;
    }

    internal static bool IsCharacterSubsequence(string text, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var queryIndex = 0;
        for (var textIndex = 0; textIndex < text.Length && queryIndex < query.Length; textIndex++)
        {
            if (char.ToUpperInvariant(text[textIndex]) == char.ToUpperInvariant(query[queryIndex]))
            {
                queryIndex++;
            }
        }

        return queryIndex == query.Length;
    }

    internal static int ComputeLevenshteinDistance(string left, string right)
    {
        if (left.Length == 0)
        {
            return right.Length;
        }

        if (right.Length == 0)
        {
            return left.Length;
        }

        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var column = 0; column <= right.Length; column++)
        {
            previous[column] = column;
        }

        for (var row = 1; row <= left.Length; row++)
        {
            current[0] = row;
            var leftChar = char.ToUpperInvariant(left[row - 1]);

            for (var column = 1; column <= right.Length; column++)
            {
                var substitutionCost = leftChar == char.ToUpperInvariant(right[column - 1]) ? 0 : 1;
                current[column] = Math.Min(
                    Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + substitutionCost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    public static bool IsValidSelection(CommandPaletteSelection? selection)
    {
        if (selection is null)
        {
            return false;
        }

        return selection.Action switch
        {
            CommandPaletteAction.OpenInstance =>
                ShellNavigationService.IsValidInstanceId(selection.InstanceId),
            CommandPaletteAction.OpenAlert =>
                NotificationFeedPanelHelper.IsValidAlertId(selection.AlertId)
                && ShellNavigationService.IsValidInstanceId(selection.InstanceId),
            CommandPaletteAction.FilterBranch =>
                !string.IsNullOrWhiteSpace(selection.BranchKey),
            CommandPaletteAction.OpenSettingsSection =>
                !string.IsNullOrWhiteSpace(selection.SettingsSectionKey),
            CommandPaletteAction.RefreshOcc or CommandPaletteAction.OpenImmediateQueue or
            CommandPaletteAction.ExportOccSnapshot =>
                true,
            _ => Enum.IsDefined(selection.Action)
        };
    }
}
