using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class DeleteInstanceDialogHelper
{
    public const string FallbackDisplayName = "this account";

    public static string NormalizeDisplayName(string? displayName) =>
        string.IsNullOrWhiteSpace(displayName) ? FallbackDisplayName : displayName.Trim();

    public static string BuildDescription(string? displayName) =>
        $"How would you like to remove \"{NormalizeDisplayName(displayName)}\"?";

    public static bool IsDestructiveChoice(DeleteInstanceChoice choice) =>
        choice == DeleteInstanceChoice.PermanentDelete;

    public static bool ShouldArchiveInstance(DeleteInstanceChoice choice) =>
        choice == DeleteInstanceChoice.RemoveFromSidebar;

    public static bool WasConfirmed(DeleteInstanceChoice choice) =>
        choice is DeleteInstanceChoice.RemoveFromSidebar or DeleteInstanceChoice.PermanentDelete;
}
