using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Dialogs;

/// <summary>
/// Tells the owner that this session could not read their account list, and offers to try again.
///
/// <para>
/// The wording lives in <see cref="AccountsUnavailableNotice"/> so the copy contract is unit-testable.
/// This type is only the presentation.
/// </para>
/// <para>
/// Modal, like the settings-recovery notice, and for a stronger reason: the screen behind it is a
/// first-run welcome page. Left unexplained, that page tells a business owner their customer history is
/// gone. This has to be read, not noticed.
/// </para>
/// </summary>
internal static class AccountsUnavailableDialog
{
    /// <summary>Shows the notice. Returns true if a retry succeeded and the accounts are now loaded.</summary>
    public static async Task<bool> ShowAsync(
        XamlRoot xamlRoot,
        IInstanceRegistryService registry)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);
        ArgumentNullException.ThrowIfNull(registry);

        var body = new TextBlock
        {
            Text = AccountsUnavailableNotice.BuildMessage(registry.StorePath, registry.LoadFailureDetail),
            TextWrapping = TextWrapping.WrapWholeWords,
            IsTextSelectionEnabled = true,
            MaxWidth = 440
        };

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = AccountsUnavailableNotice.Title,
            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 380,
                Content = body
            },
            PrimaryButtonText = AccountsUnavailableNotice.RetryButtonText,
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowManagedAsync();

        AppLogger.LogInfo("Registry.Recovery", $"Accounts-unavailable notice closed with '{result}'.");

        if (result != ContentDialogResult.Primary)
        {
            return false;
        }

        var recovered = await registry.RetryLoadAsync().ConfigureAwait(true);
        AppLogger.LogInfo(
            "Registry.Recovery",
            recovered
                ? $"Retry succeeded — {registry.Instances.Count} account(s) loaded."
                : "Retry failed; the account list is still unreadable.");

        if (recovered)
        {
            return true;
        }

        // A retry that silently changes nothing reads as a broken button. Say what happened, once.
        await new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = AccountsUnavailableNotice.Title,
            Content = new TextBlock
            {
                Text = AccountsUnavailableNotice.RetryFailedMessage,
                TextWrapping = TextWrapping.WrapWholeWords,
                MaxWidth = 440
            },
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close
        }.ShowManagedAsync();

        return false;
    }
}
