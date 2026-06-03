using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Dialogs;

public sealed partial class EditInstanceMetadataDialog : ContentDialog
{
    public EditInstanceMetadataDialog(MessengerInstance instance)
    {
        InitializeComponent();
        _instance = instance;
        Loaded += OnLoaded;
    }

    private readonly MessengerInstance _instance;

    public string? ResultDisplayName { get; private set; }

    public string? ResultPlatformId { get; private set; }

    public string? ResultStartUrl { get; private set; }

    public string? ResultNotes { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PlatformBox.ItemsSource = PlatformDefinition.All;
        PlatformBox.DisplayMemberPath = nameof(PlatformDefinition.DisplayName);

        DisplayNameBox.Text = _instance.DisplayName;
        CustomUrlBox.Text = _instance.StartUrl;
        NotesBox.Text = _instance.Notes ?? string.Empty;
        NotesBox.Visibility = AppSettingsService.Instance.Settings.EnableInstanceNotesTags
            ? Visibility.Visible
            : Visibility.Collapsed;

        var platform = PlatformDefinition.FindById(_instance.Platform) ?? PlatformDefinition.All[0];
        PlatformBox.SelectedItem = PlatformDefinition.All.FirstOrDefault(p =>
            p.Id.Equals(platform.Id, StringComparison.OrdinalIgnoreCase));

        UpdateCustomUrlVisibility();
        DisplayNameBox.Focus(FocusState.Programmatic);
    }

    private void PlatformBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateCustomUrlVisibility();

    private void UpdateCustomUrlVisibility()
    {
        if (PlatformBox.SelectedItem is not PlatformDefinition platform)
        {
            return;
        }

        if (platform.Id == "generic")
        {
            CustomUrlBox.PlaceholderText = "https://";
            return;
        }

        if (string.IsNullOrWhiteSpace(CustomUrlBox.Text) ||
            CustomUrlBox.Text.Equals(_instance.StartUrl, StringComparison.OrdinalIgnoreCase))
        {
            CustomUrlBox.PlaceholderText = platform.DefaultUrl;
        }
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ValidationMessage.Visibility = Visibility.Collapsed;

        if (string.IsNullOrWhiteSpace(DisplayNameBox.Text))
        {
            ShowValidation("Display name is required.");
            args.Cancel = true;
            return;
        }

        if (PlatformBox.SelectedItem is not PlatformDefinition platform)
        {
            ShowValidation("Select a platform.");
            args.Cancel = true;
            return;
        }

        var startUrl = string.IsNullOrWhiteSpace(CustomUrlBox.Text)
            ? platform.DefaultUrl
            : CustomUrlBox.Text.Trim();

        if (platform.Id == "generic" && string.IsNullOrWhiteSpace(startUrl))
        {
            ShowValidation("Enter a start URL for this instance.");
            args.Cancel = true;
            return;
        }

        if (!Uri.TryCreate(startUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("https" or "http"))
        {
            ShowValidation("Start URL must be a valid http or https address.");
            args.Cancel = true;
            return;
        }

        ResultDisplayName = DisplayNameBox.Text.Trim();
        ResultPlatformId = platform.Id;
        ResultStartUrl = startUrl;
        ResultNotes = string.IsNullOrWhiteSpace(NotesBox.Text) ? null : NotesBox.Text.Trim();
    }

    private void ShowValidation(string message)
    {
        ValidationMessage.Text = message;
        ValidationMessage.Visibility = Visibility.Visible;
    }
}
