using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class PersonalOverviewLayoutService
{
    public static readonly IReadOnlyList<string> DefaultSectionOrder =
        PersonalOverviewLayoutDefaults.SectionOrder;

    public static IReadOnlyList<string> Resolve(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return SanitizeOrder(settings.PersonalOverviewSectionOrder);
    }

    public static void ApplyDefaults(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.PersonalOverviewSectionOrder = DefaultSectionOrder.ToList();
    }

    public static void Normalize(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.PersonalOverviewSectionOrder = SanitizeOrder(settings.PersonalOverviewSectionOrder).ToList();
    }

    private static IReadOnlyList<string> SanitizeOrder(IReadOnlyList<string>? stored)
    {
        var results = new List<string>();
        if (stored is not null)
        {
            foreach (var sectionId in stored)
            {
                if (string.IsNullOrWhiteSpace(sectionId))
                {
                    continue;
                }

                var normalized = sectionId.Trim();
                if (DefaultSectionOrder.Contains(normalized, StringComparer.OrdinalIgnoreCase) &&
                    !results.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    results.Add(normalized);
                }
            }
        }

        foreach (var sectionId in DefaultSectionOrder)
        {
            if (!results.Contains(sectionId, StringComparer.OrdinalIgnoreCase))
            {
                results.Add(sectionId);
            }
        }

        return results;
    }
}
