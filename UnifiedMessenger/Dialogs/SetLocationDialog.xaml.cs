using Microsoft.UI.Xaml.Controls;

namespace UnifiedMessenger.Dialogs;

public sealed partial class SetLocationDialog : ContentDialog
{
    public SetLocationDialog(
        string instanceDisplayName,
        string? currentBranchKey,
        IReadOnlyList<string> existingLocations)
    {
        InitializeComponent();
        Title = $"Set location — {instanceDisplayName}";
        foreach (var loc in existingLocations)
        {
            LocationCombo.Items.Add(loc);
        }

        LocationCombo.Text = currentBranchKey ?? string.Empty;
        PrimaryButtonClick += OnPrimaryButtonClick;
        SecondaryButtonClick += OnSecondaryButtonClick;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// The trimmed location name to save; null when the user chose Clear or left the field empty.
    /// Only valid after ShowAsync() returns Primary or Secondary.
    /// </summary>
    public string? SelectedLocation { get; private set; }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        SelectedLocation = string.IsNullOrWhiteSpace(LocationCombo.Text)
            ? null
            : LocationCombo.Text.Trim();
    }

    private void OnSecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        SelectedLocation = null;
    }

    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        PrimaryButtonClick -= OnPrimaryButtonClick;
        SecondaryButtonClick -= OnSecondaryButtonClick;
        Unloaded -= OnUnloaded;
    }
}
