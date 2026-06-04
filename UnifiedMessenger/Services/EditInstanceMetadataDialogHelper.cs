using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed class EditInstanceMetadataFormState
{
    public string DisplayName { get; init; } = string.Empty;

    public string StartUrl { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;

    public string PlatformId { get; init; } = string.Empty;
}

public sealed class EditInstanceMetadataDialogSubmission
{
    public bool IsValid { get; init; }

    public string? ValidationMessage { get; init; }

    public string? DisplayName { get; init; }

    public string? PlatformId { get; init; }

    public string? StartUrl { get; init; }

    public string? Notes { get; init; }

    public bool IsUnchanged { get; init; }
}

public static class EditInstanceMetadataDialogHelper
{
    public const string RequiredDisplayNameMessage = "Display name is required.";

    public const string RequiredPlatformMessage = "Select a platform.";

    public const string RequiredStartUrlMessage = "Enter a start URL for this instance.";

    public const string InvalidStartUrlMessage = "Start URL must be a valid http or https address.";

    public static EditInstanceMetadataFormState CreateInitialFormState(MessengerInstance instance)
    {
        var platform = ResolveInitialPlatform(instance);

        return new EditInstanceMetadataFormState
        {
            DisplayName = RenameInstanceDialogHelper.NormalizeInitialDisplayName(instance.DisplayName),
            StartUrl = string.IsNullOrWhiteSpace(instance.StartUrl) ? string.Empty : instance.StartUrl.Trim(),
            Notes = string.IsNullOrWhiteSpace(instance.Notes) ? string.Empty : instance.Notes.Trim(),
            PlatformId = platform.Id
        };
    }

    public static PlatformDefinition ResolveInitialPlatform(MessengerInstance instance) =>
        PlatformDefinition.FindById(instance.Platform) ?? PlatformDefinition.All[0];

    public static bool ShouldShowNotesField(bool enableInstanceNotesTags) => enableInstanceNotesTags;

    public static string? TryResolveCustomUrlPlaceholder(
        PlatformDefinition platform,
        string? currentUrlText,
        string originalStartUrl)
    {
        if (AddInstanceDialogHelper.ShouldShowCustomUrlField(platform.Id))
        {
            return "https://";
        }

        if (string.IsNullOrWhiteSpace(currentUrlText) ||
            currentUrlText.Equals(originalStartUrl, StringComparison.OrdinalIgnoreCase))
        {
            return platform.DefaultUrl;
        }

        return null;
    }

    public static EditInstanceMetadataDialogSubmission ValidatePrimaryAction(
        EditInstanceMetadataFormState initialState,
        string? displayName,
        PlatformDefinition? platform,
        string? customUrlText,
        string? notesText)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Invalid(RequiredDisplayNameMessage);
        }

        if (platform is null)
        {
            return Invalid(RequiredPlatformMessage);
        }

        var platformId = PlatformDefinition.NormalizePlatformId(platform.Id);
        var startUrl = string.IsNullOrWhiteSpace(customUrlText)
            ? platform.DefaultUrl
            : customUrlText.Trim();

        if (AddInstanceDialogHelper.ShouldShowCustomUrlField(platformId) && string.IsNullOrWhiteSpace(startUrl))
        {
            return Invalid(RequiredStartUrlMessage);
        }

        if (!AddInstanceDialogHelper.TryNormalizeCustomUrl(startUrl, out var normalizedStartUrl, out _))
        {
            return Invalid(InvalidStartUrlMessage);
        }

        var normalizedDisplayName = displayName.Trim();
        var normalizedNotes = NormalizeNotes(notesText);

        return new EditInstanceMetadataDialogSubmission
        {
            IsValid = true,
            DisplayName = normalizedDisplayName,
            PlatformId = platformId,
            StartUrl = normalizedStartUrl,
            Notes = string.IsNullOrEmpty(normalizedNotes) ? null : normalizedNotes,
            IsUnchanged = IsSubmissionUnchanged(
                initialState,
                normalizedDisplayName,
                platformId,
                normalizedStartUrl!,
                normalizedNotes)
        };
    }

    private static string NormalizeNotes(string? notes) =>
        string.IsNullOrWhiteSpace(notes) ? string.Empty : notes.Trim();

    private static bool IsSubmissionUnchanged(
        EditInstanceMetadataFormState initial,
        string displayName,
        string platformId,
        string startUrl,
        string notes) =>
        initial.DisplayName.Equals(displayName, StringComparison.Ordinal)
        && initial.PlatformId.Equals(platformId, StringComparison.OrdinalIgnoreCase)
        && initial.StartUrl.Equals(startUrl, StringComparison.OrdinalIgnoreCase)
        && initial.Notes.Equals(notes, StringComparison.Ordinal);

    private static EditInstanceMetadataDialogSubmission Invalid(string message) =>
        new() { IsValid = false, ValidationMessage = message };
}
