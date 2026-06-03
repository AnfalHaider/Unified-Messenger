using Microsoft.UI.Xaml.Controls;

namespace UnifiedMessenger.Dialogs;

public enum DeleteInstanceChoice
{
    Cancelled,
    RemoveFromSidebar,
    PermanentDelete
}

public sealed partial class DeleteInstanceDialog : ContentDialog
{
    public DeleteInstanceDialog(string displayName)
    {
        InitializeComponent();
        DescriptionText.Text = $"How would you like to remove \"{displayName}\"?";

        PrimaryButtonClick += (_, _) => Choice = DeleteInstanceChoice.RemoveFromSidebar;
        SecondaryButtonClick += (_, _) => Choice = DeleteInstanceChoice.PermanentDelete;
        CloseButtonClick += (_, _) => Choice = DeleteInstanceChoice.Cancelled;
    }

    public DeleteInstanceChoice Choice { get; private set; } = DeleteInstanceChoice.Cancelled;
}
