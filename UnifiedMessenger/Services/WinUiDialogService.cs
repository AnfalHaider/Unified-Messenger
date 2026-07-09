using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnifiedMessenger.Services;

public sealed class WinUiDialogService : IDialogService
{
    private Func<XamlRoot>? _xamlRootProvider;

    public void SetXamlRootProvider(Func<XamlRoot> provider) =>
        _xamlRootProvider = provider ?? throw new ArgumentNullException(nameof(provider));

    public async Task ShowErrorAsync(
        string title,
        string message,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = RequireXamlRoot()
        };

        await dialog.ShowAsync();
    }

    public async Task<bool> ConfirmAsync(
        string title,
        string content,
        string primaryButtonText,
        string closeButtonText = "Cancel",
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = RequireXamlRoot()
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private XamlRoot RequireXamlRoot()
    {
        if (_xamlRootProvider is null)
        {
            throw new InvalidOperationException("Dialog service requires a XamlRoot provider.");
        }

        return _xamlRootProvider.Invoke()
            ?? throw new InvalidOperationException("XamlRoot is not available yet.");
    }
}
