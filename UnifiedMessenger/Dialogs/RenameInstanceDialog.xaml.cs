using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnifiedMessenger.Dialogs;

public sealed partial class RenameInstanceDialog : ContentDialog
{
    public RenameInstanceDialog(string currentDisplayName)
    {
        InitializeComponent();
        DisplayNameBox.Text = currentDisplayName;
        Loaded += (_, _) =>
        {
            DisplayNameBox.Focus(FocusState.Programmatic);
            DisplayNameBox.SelectAll();
        };
    }

    public string? ResultDisplayName { get; private set; }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ValidationMessage.Visibility = Visibility.Collapsed;

        if (string.IsNullOrWhiteSpace(DisplayNameBox.Text))
        {
            ValidationMessage.Text = "Display name is required.";
            ValidationMessage.Visibility = Visibility.Visible;
            args.Cancel = true;
            return;
        }

        ResultDisplayName = DisplayNameBox.Text.Trim();
    }
}
