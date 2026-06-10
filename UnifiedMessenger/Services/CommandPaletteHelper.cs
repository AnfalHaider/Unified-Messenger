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

    public static bool EntryMatches(CommandPaletteEntry entry, string query)
    {
        ArgumentNullException.ThrowIfNull(entry);
        query = NormalizeQuery(query);

        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Category.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    public static int ScoreEntry(CommandPaletteEntry entry, string query)
    {
        ArgumentNullException.ThrowIfNull(entry);
        query = NormalizeQuery(query);

        if (string.IsNullOrWhiteSpace(query))
        {
            return 0;
        }

        if (entry.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        if (entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 50;
        }

        if (entry.Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 25;
        }

        return entry.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ? 10 : 0;
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
            CommandPaletteAction.RefreshOcc or CommandPaletteAction.OpenImmediateQueue =>
                true,
            _ => Enum.IsDefined(selection.Action)
        };
    }
}
