using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using FocusTrapHelper = UnifiedMessenger.Services.FocusTrapHelper;

namespace UnifiedMessenger.Dialogs;

public sealed partial class DeleteInstanceDialog : ContentDialog
{
    private FocusTrapHelper? _focusTrap;

    public DeleteInstanceDialog(string? displayName)
    {
        InitializeComponent();
        DescriptionText.Text = DeleteInstanceDialogHelper.BuildDescription(displayName);

        Loaded += OnLoaded;
        PrimaryButtonClick += OnPrimaryButtonClick;
        CloseButtonClick += OnCloseButtonClick;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _focusTrap?.Dispose();
        _focusTrap = FocusTrapHelper.Activate(this);
    }

    public DeleteInstanceChoice Choice { get; private set; } = DeleteInstanceChoice.Cancelled;

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args) =>
        Choice = DeleteInstanceChoice.RemoveFromSidebar;

    private void OnCloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args) =>
        Choice = DeleteInstanceChoice.Cancelled;

    private void PermanentDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        Choice = DeleteInstanceChoice.PermanentDelete;
        Hide();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        PrimaryButtonClick -= OnPrimaryButtonClick;
        CloseButtonClick -= OnCloseButtonClick;
        Unloaded -= OnUnloaded;
        _focusTrap?.Dispose();
        _focusTrap = null;
    }
}
