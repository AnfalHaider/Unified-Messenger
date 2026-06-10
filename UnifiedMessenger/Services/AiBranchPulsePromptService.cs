using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Builds batch LLM prompts that summarize unresolved thread themes for a branch workspace.
/// </summary>
public static class AiBranchPulsePromptService
{
    public const int MaxThreadsInBatch = 20;

    public const string SystemPrompt =
        """
        You are the operations intelligence assistant for a multi-branch beauty salon group in Pakistan.
        Summarize open WhatsApp customer threads for branch managers. Use plain text only — no JSON or markdown fences.
        Respond in this exact structure:

        THEMES:
        1. <short theme>
        2. <short theme>
        (up to five numbered themes)

        SUMMARY:
        <two or three sentences with manager-level guidance>
        """;

    public static string BuildUserPrompt(
        string scopeLabel,
        string? branchKey,
        IReadOnlyList<ThreadData> openThreads)
    {
        var branchProfile = ResolveBranchProfile(branchKey, scopeLabel);
        var threadBlock = BuildThreadBlock(openThreads);

        return $"""
            Branch scope: {scopeLabel}
            Branch catalog: {branchProfile.BranchKey}
            Services: {string.Join(", ", branchProfile.Services)}
            Booking rules: {branchProfile.BookingRules}

            Open unresolved threads ({openThreads.Count}):
            {threadBlock}

            Identify the top unresolved themes and revenue risks. Prioritize SLA breaches, bridal bookings, and frustrated clients.
            """;
    }

    internal static IReadOnlyList<ThreadData> SelectThreadsForBatch(IEnumerable<ThreadData> threads)
    {
        return threads
            .Where(thread => !thread.IsReplied && !thread.IsSpamOrPromo)
            .OrderByDescending(thread => thread.UrgencyScore)
            .ThenByDescending(thread => thread.LatencyMinutes)
            .ThenByDescending(thread => thread.EstimatedValue)
            .Take(MaxThreadsInBatch)
            .ToList();
    }

    internal static BranchPulseSnapshot ParseResponse(
        string scopeLabel,
        string? branchKey,
        int openThreadCount,
        string? rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return new BranchPulseSnapshot
            {
                ScopeLabel = scopeLabel,
                BranchKey = branchKey,
                OpenThreadCount = openThreadCount,
                State = BranchPulseState.Error,
                StatusMessage = "Branch pulse returned an empty response."
            };
        }

        var themes = new List<string>();
        var summaryLines = new List<string>();
        var section = PulseParseSection.Themes;

        foreach (var line in rawResponse.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (trimmed.StartsWith("SUMMARY:", StringComparison.OrdinalIgnoreCase))
            {
                var inline = trimmed["SUMMARY:".Length..].Trim();
                if (inline.Length > 0)
                {
                    summaryLines.Add(inline);
                }

                section = PulseParseSection.Summary;
                continue;
            }

            if (trimmed.StartsWith("THEMES:", StringComparison.OrdinalIgnoreCase))
            {
                section = PulseParseSection.Themes;
                continue;
            }

            if (section == PulseParseSection.Themes)
            {
                var theme = StripNumberPrefix(trimmed);
                if (theme.Length > 0)
                {
                    themes.Add(theme);
                }

                continue;
            }

            summaryLines.Add(trimmed);
        }

        var summary = string.Join(' ', summaryLines).Trim();
        if (themes.Count == 0 && summary.Length == 0)
        {
            summary = rawResponse.Trim();
        }

        return new BranchPulseSnapshot
        {
            ScopeLabel = scopeLabel,
            BranchKey = branchKey,
            OpenThreadCount = openThreadCount,
            Themes = themes,
            Summary = summary,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            State = BranchPulseState.Ready,
            StatusMessage = themes.Count == 0
                ? "Pulse ready"
                : $"{themes.Count} theme{(themes.Count == 1 ? "" : "s")} identified"
        };
    }

    private static string BuildThreadBlock(IReadOnlyList<ThreadData> openThreads)
    {
        if (openThreads.Count == 0)
        {
            return "(none)";
        }

        var lines = openThreads.Select(thread =>
            $"- {thread.CustomerName} | intent={thread.AiIntentCategory} | urgency={thread.UrgencyScore} | " +
            $"sentiment={thread.ClientSentiment} | latency={thread.LatencyMinutes:F0}m | " +
            $"value=PKR {thread.EstimatedValue:F0} | action={TrimForPrompt(thread.NextActionSummary)}");

        return string.Join(Environment.NewLine, lines);
    }

    private static BranchOperationalProfile ResolveBranchProfile(string? branchKey, string scopeLabel)
    {
        var resolved = string.IsNullOrWhiteSpace(branchKey)
            ? scopeLabel
            : branchKey.Trim();
        var catalog = AppSettingsService.Instance.Settings.BranchOperationalCatalog;
        var profile = catalog.FirstOrDefault(entry =>
            entry.BranchKey.Equals(resolved, StringComparison.OrdinalIgnoreCase));
        return profile ?? BranchOperationalCatalogDefaults.CreateFallbackProfile(resolved);
    }

    private static string TrimForPrompt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "follow up";
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 120 ? trimmed : trimmed[..117] + "...";
    }

    private static string StripNumberPrefix(string line)
    {
        var trimmed = line.Trim();
        var index = 0;
        while (index < trimmed.Length && (char.IsDigit(trimmed[index]) || trimmed[index] == '.' || trimmed[index] == ')'))
        {
            index++;
        }

        return index < trimmed.Length ? trimmed[index..].Trim() : trimmed;
    }

    private enum PulseParseSection
    {
        Themes,
        Summary
    }
}
