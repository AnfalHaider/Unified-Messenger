using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed class AddInstanceDialogSubmission
{
    public bool IsValid { get; init; }

    public string? ValidationMessage { get; init; }

    public string? RestoreInstanceId { get; init; }

    public string? DisplayName { get; init; }

    public string? PlatformId { get; init; }

    public string? CustomUrl { get; init; }

    public WorkspaceCategory Category { get; init; } = WorkspaceCategory.Personal;
}

public static class AddInstanceDialogHelper
{
    public const string GenericPlatformId = "generic";

    public static bool IsRestoreMode(MessengerInstance? selectedArchivedInstance) =>
        selectedArchivedInstance is not null;

    public static bool ShouldShowCustomUrlField(string? platformId) =>
        string.Equals(platformId, GenericPlatformId, StringComparison.OrdinalIgnoreCase);

    public static bool ShouldEnableNewInstanceFields(bool isRestoreMode) => !isRestoreMode;

    public static bool ShouldShowRestorePicker(int archivedInstanceCount) => archivedInstanceCount > 0;

    public static bool TryNormalizeCustomUrl(
        string? customUrl,
        out string? normalizedUrl,
        out string? errorMessage)
    {
        normalizedUrl = null;
        if (string.IsNullOrWhiteSpace(customUrl))
        {
            errorMessage = null;
            return true;
        }

        normalizedUrl = customUrl.Trim();
        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http"))
        {
            errorMessage = "Custom URL must be a valid http or https address.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    public static AddInstanceDialogSubmission ValidatePrimaryAction(
        MessengerInstance? selectedArchivedInstance,
        string? displayName,
        PlatformDefinition? platform,
        string? customUrl,
        WorkspaceCategory category)
    {
        if (selectedArchivedInstance is not null)
        {
            if (!ShellNavigationService.IsValidInstanceId(selectedArchivedInstance.Id))
            {
                return Invalid("Select a valid archived account to restore.");
            }

            return new AddInstanceDialogSubmission
            {
                IsValid = true,
                RestoreInstanceId = selectedArchivedInstance.Id.Trim()
            };
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Invalid("Display name is required.");
        }

        if (platform is null)
        {
            return Invalid("Select a platform.");
        }

        var platformId = PlatformDefinition.NormalizePlatformId(platform.Id);
        if (ShouldShowCustomUrlField(platformId) && string.IsNullOrWhiteSpace(customUrl))
        {
            return Invalid("Enter a custom URL for this platform.");
        }

        if (!TryNormalizeCustomUrl(customUrl, out var normalizedUrl, out var urlError))
        {
            return Invalid(urlError!);
        }

        return new AddInstanceDialogSubmission
        {
            IsValid = true,
            DisplayName = displayName.Trim(),
            PlatformId = platformId,
            CustomUrl = normalizedUrl,
            Category = category
        };
    }

    private static AddInstanceDialogSubmission Invalid(string message) =>
        new() { IsValid = false, ValidationMessage = message };
}
