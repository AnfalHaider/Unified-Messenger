using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Dialogs;

public sealed partial class ConfirmPermanentDeleteDialog : ContentDialog
{
    public ConfirmPermanentDeleteDialog(string? displayName)
    {
        InitializeComponent();
        DescriptionText.Text = SettingsPageHelper.BuildPermanentDeleteConfirmation(displayName);
    }
}
