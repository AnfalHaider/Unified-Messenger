using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Dialogs;

public sealed partial class EditInstanceMetadataDialog : ContentDialog
{
    private readonly EditInstanceMetadataFormState _initialState;

    public EditInstanceMetadataDialog(MessengerInstance instance)
    {
        _initialState = EditInstanceMetadataDialogHelper.CreateInitialFormState(instance);
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public string? ResultDisplayName { get; private set; }

    public string? ResultPlatformId { get; private set; }

    public string? ResultStartUrl { get; private set; }

    public string? ResultNotes { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PlatformBox.ItemsSource = PlatformDefinition.All;
        PlatformBox.DisplayMemberPath = nameof(PlatformDefinition.DisplayName);

        DisplayNameBox.Text = _initialState.DisplayName;
        CustomUrlBox.Text = _initialState.StartUrl;
        NotesBox.Text = _initialState.Notes;
        NotesBox.Visibility = EditInstanceMetadataDialogHelper.ShouldShowNotesField(
            AppSettingsService.Instance.Settings.EnableInstanceNotesTags)
            ? Visibility.Visible
            : Visibility.Collapsed;

        PlatformBox.SelectedItem = PlatformDefinition.All.FirstOrDefault(platform =>
            platform.Id.Equals(_initialState.PlatformId, StringComparison.OrdinalIgnoreCase));

        UpdateCustomUrlPlaceholder();
        DisplayNameBox.Focus(FocusState.Programmatic);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void PlatformBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateCustomUrlPlaceholder();

    private void UpdateCustomUrlPlaceholder()
    {
        if (PlatformBox.SelectedItem is not PlatformDefinition platform)
        {
            return;
        }

        var placeholder = EditInstanceMetadataDialogHelper.TryResolveCustomUrlPlaceholder(
            platform,
            CustomUrlBox.Text,
            _initialState.StartUrl);

        if (placeholder is not null)
        {
            CustomUrlBox.PlaceholderText = placeholder;
        }
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ValidationMessage.Visibility = Visibility.Collapsed;

        var submission = EditInstanceMetadataDialogHelper.ValidatePrimaryAction(
            _initialState,
            DisplayNameBox.Text,
            PlatformBox.SelectedItem as PlatformDefinition,
            CustomUrlBox.Text,
            NotesBox.Text);

        if (!submission.IsValid)
        {
            ShowValidation(submission.ValidationMessage ?? "Could not save instance metadata.");
            args.Cancel = true;
            return;
        }

        ResultDisplayName = submission.DisplayName;
        ResultPlatformId = submission.PlatformId;
        ResultStartUrl = submission.StartUrl;
        ResultNotes = submission.Notes;
    }

    private void ShowValidation(string message)
    {
        ValidationMessage.Text = message;
        ValidationMessage.Visibility = Visibility.Visible;
    }
}
