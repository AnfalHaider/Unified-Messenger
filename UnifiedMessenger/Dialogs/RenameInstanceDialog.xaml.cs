using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Services;
using FocusTrapHelper = UnifiedMessenger.Services.FocusTrapHelper;

namespace UnifiedMessenger.Dialogs;

public sealed partial class RenameInstanceDialog : ContentDialog
{
    private readonly string _currentDisplayName;
    private FocusTrapHelper? _focusTrap;

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
        _focusTrap?.Dispose();
        _focusTrap = FocusTrapHelper.Activate(this);
        DisplayNameBox.Focus(FocusState.Programmatic);
        DisplayNameBox.SelectAll();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        _focusTrap?.Dispose();
        _focusTrap = null;
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
