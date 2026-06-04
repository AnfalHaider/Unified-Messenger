using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Dialogs;

public sealed partial class RenameInstanceDialog : ContentDialog
{
    private readonly string _currentDisplayName;

    public RenameInstanceDialog(string? currentDisplayName)
    {
        _currentDisplayName = RenameInstanceDialogHelper.NormalizeInitialDisplayName(currentDisplayName);
        InitializeComponent();
        DisplayNameBox.Text = _currentDisplayName;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public string? ResultDisplayName { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DisplayNameBox.Focus(FocusState.Programmatic);
        DisplayNameBox.SelectAll();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ValidationMessage.Visibility = Visibility.Collapsed;

        var submission = RenameInstanceDialogHelper.ValidatePrimaryAction(
            _currentDisplayName,
            DisplayNameBox.Text);

        if (!submission.IsValid)
        {
            ValidationMessage.Text = submission.ValidationMessage ?? RenameInstanceDialogHelper.RequiredDisplayNameMessage;
            ValidationMessage.Visibility = Visibility.Visible;
            args.Cancel = true;
            return;
        }

        ResultDisplayName = submission.DisplayName;
    }
}
