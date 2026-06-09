using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Dialogs;

public sealed partial class DeleteInstanceDialog : ContentDialog
{
    public DeleteInstanceDialog(string? displayName)
    {
        InitializeComponent();
        DescriptionText.Text = DeleteInstanceDialogHelper.BuildDescription(displayName);

        PrimaryButtonClick += OnPrimaryButtonClick;
        CloseButtonClick += OnCloseButtonClick;
        Unloaded += OnUnloaded;
    }

    public DeleteInstanceChoice Choice { get; private set; } = DeleteInstanceChoice.Cancelled;

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args) =>
        Choice = DeleteInstanceChoice.RemoveFromSidebar;

    private void OnCloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args) =>
        Choice = DeleteInstanceChoice.Cancelled;

    private async void PermanentDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var confirm = new ContentDialog
        {
            Title = "Permanently delete account?",
            Content =
                "This removes the WebView profile, cookies, cache, and saved session for this account. You will need to sign in again if you add it back.",
            PrimaryButtonText = "Delete permanently",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        Choice = DeleteInstanceChoice.PermanentDelete;
        Hide();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        PrimaryButtonClick -= OnPrimaryButtonClick;
        CloseButtonClick -= OnCloseButtonClick;
        Unloaded -= OnUnloaded;
    }
}
