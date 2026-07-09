namespace UnifiedMessenger.Services;

public interface IDialogService
{
    Task ShowErrorAsync(string title, string message, CancellationToken cancellationToken = default);

    Task<bool> ConfirmAsync(
        string title,
        string content,
        string primaryButtonText,
        string closeButtonText = "Cancel",
        CancellationToken cancellationToken = default);
}
