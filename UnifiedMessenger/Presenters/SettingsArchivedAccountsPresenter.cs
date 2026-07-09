using UnifiedMessenger.Models;
using UnifiedMessenger.ViewModels;

namespace UnifiedMessenger.Presenters;

public static class SettingsArchivedAccountsPresenter
{
    public static IReadOnlyList<ArchivedAccountRowViewModel> BuildRows(
        IEnumerable<MessengerInstance> archivedInstances)
    {
        ArgumentNullException.ThrowIfNull(archivedInstances);

        return archivedInstances
            .Where(instance => !string.IsNullOrWhiteSpace(instance.Id))
            .OrderBy(instance => instance.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(instance =>
            {
                instance.ApplyPlatformBranding();
                var platform = PlatformDefinition.FindById(instance.Platform);
                return new ArchivedAccountRowViewModel
                {
                    InstanceId = instance.Id.Trim(),
                    DisplayName = instance.DisplayName,
                    PlatformLabel = platform?.DisplayName ?? instance.Platform,
                    ProfileLine = BuildProfileLine(instance.ProfileName),
                    AccentColorHex = string.IsNullOrWhiteSpace(instance.AccentColor)
                        ? platform?.AccentColor ?? "#6B7280"
                        : instance.AccentColor,
                    IconGlyph = string.IsNullOrWhiteSpace(instance.IconGlyph)
                        ? platform?.IconGlyph ?? "\uE8BD"
                        : instance.IconGlyph
                };
            })
            .ToList();
    }

    internal static string BuildProfileLine(string? profileName) =>
        string.IsNullOrWhiteSpace(profileName)
            ? "Profile folder unavailable"
            : $"Profile: {profileName.Trim()}";
}
