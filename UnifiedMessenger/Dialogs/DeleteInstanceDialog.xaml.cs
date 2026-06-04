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
        SecondaryButtonClick += OnSecondaryButtonClick;
        CloseButtonClick += OnCloseButtonClick;
        Unloaded += OnUnloaded;
    }

    public DeleteInstanceChoice Choice { get; private set; } = DeleteInstanceChoice.Cancelled;

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args) =>
        Choice = DeleteInstanceChoice.RemoveFromSidebar;

    private void OnSecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args) =>
        Choice = DeleteInstanceChoice.PermanentDelete;

    private void OnCloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args) =>
        Choice = DeleteInstanceChoice.Cancelled;

    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        PrimaryButtonClick -= OnPrimaryButtonClick;
        SecondaryButtonClick -= OnSecondaryButtonClick;
        CloseButtonClick -= OnCloseButtonClick;
        Unloaded -= OnUnloaded;
    }
}
