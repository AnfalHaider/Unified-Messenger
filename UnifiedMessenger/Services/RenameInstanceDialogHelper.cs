namespace UnifiedMessenger.Services;

public sealed class RenameInstanceDialogSubmission
{
    public bool IsValid { get; init; }

    public string? ValidationMessage { get; init; }

    public string? DisplayName { get; init; }

    public bool IsUnchanged { get; init; }
}

public static class RenameInstanceDialogHelper
{
    public const string RequiredDisplayNameMessage = "Display name is required.";

    public static string NormalizeInitialDisplayName(string? currentDisplayName) =>
        string.IsNullOrWhiteSpace(currentDisplayName) ? string.Empty : currentDisplayName.Trim();

    public static RenameInstanceDialogSubmission ValidatePrimaryAction(
        string? currentDisplayName,
        string? editedDisplayName)
    {
        if (string.IsNullOrWhiteSpace(editedDisplayName))
        {
            return Invalid(RequiredDisplayNameMessage);
        }

        var normalized = editedDisplayName.Trim();
        var current = NormalizeInitialDisplayName(currentDisplayName);

        return new RenameInstanceDialogSubmission
        {
            IsValid = true,
            DisplayName = normalized,
            IsUnchanged = !string.IsNullOrEmpty(current)
                && current.Equals(normalized, StringComparison.Ordinal)
        };
    }

    private static RenameInstanceDialogSubmission Invalid(string message) =>
        new() { IsValid = false, ValidationMessage = message };
}
