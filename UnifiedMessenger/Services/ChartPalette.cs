namespace UnifiedMessenger.Services;

/// <summary>
/// Resolves per-series colours for the stacked activity chart. Accounts on the same platform share the same
/// brand accent (e.g. three WhatsApp accounts are all the same green), which makes a stacked chart look like
/// one solid bar — so when any accent is duplicated, EVERY series gets a colour from a qualitative palette
/// instead (assigned stably by instance id, so colours don't shuffle between renders). Distinct accents are
/// kept as-is, since they already identify the accounts elsewhere in the app.
/// </summary>
public static class ChartPalette
{
    // Qualitative palette — distinguishable hues that hold up on light and dark surfaces.
    internal static readonly string[] Palette =
    [
        "#1565C0", // blue
        "#EF6C00", // orange
        "#2E7D32", // green
        "#8E24AA", // purple
        "#C62828", // red
        "#00838F", // teal
        "#F9A825", // amber
        "#5D4037", // brown
    ];

    /// <summary>Instance-id → colour hex for the given series set.</summary>
    public static IReadOnlyDictionary<string, string> ResolveSeriesColors(IReadOnlyList<ActivityAccountSeries> series)
    {
        ArgumentNullException.ThrowIfNull(series);

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (series.Count == 0)
        {
            return map;
        }

        var accentsDistinct = series
            .Select(s => (s.AccentColor ?? string.Empty).Trim())
            .Where(a => a.Length > 0)
            .GroupBy(a => a, StringComparer.OrdinalIgnoreCase)
            .All(g => g.Count() == 1);

        if (accentsDistinct && series.All(s => !string.IsNullOrWhiteSpace(s.AccentColor)))
        {
            foreach (var s in series)
            {
                map[s.InstanceId] = s.AccentColor;
            }

            return map;
        }

        // Duplicated (or missing) accents → palette for all, stable by instance id.
        var ordered = series
            .Select(s => s.InstanceId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            map[ordered[i]] = Palette[i % Palette.Length];
        }

        return map;
    }
}
