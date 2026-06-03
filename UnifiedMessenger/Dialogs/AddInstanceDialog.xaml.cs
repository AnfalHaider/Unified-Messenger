using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Dialogs;

public sealed partial class AddInstanceDialog : ContentDialog
{
    private readonly IReadOnlyList<MessengerInstance> _archivedInstances;
    private bool _isRestoreMode;

    public AddInstanceDialog(IReadOnlyList<MessengerInstance> archivedInstances)
    {
        _archivedInstances = archivedInstances;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public string? ResultDisplayName { get; private set; }

    public string? ResultPlatformId { get; private set; }

    public string? ResultCustomUrl { get; private set; }

    public string? ResultRestoreInstanceId { get; private set; }

    public WorkspaceCategory ResultCategory { get; private set; } = WorkspaceCategory.Personal;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PlatformBox.ItemsSource = PlatformDefinition.All;
        PlatformBox.DisplayMemberPath = nameof(PlatformDefinition.DisplayName);
        PlatformBox.SelectedIndex = 0;

        CategoryBox.ItemsSource = new[]
        {
            new WorkspaceCategoryOption(WorkspaceCategory.Personal, "Personal"),
            new WorkspaceCategoryOption(WorkspaceCategory.Professional, "Professional")
        };
        CategoryBox.DisplayMemberPath = nameof(WorkspaceCategoryOption.Label);
        CategoryBox.SelectedIndex = 0;

        if (_archivedInstances.Count > 0)
        {
            RestoreBox.Visibility = Visibility.Visible;
            RestoreBox.ItemsSource = _archivedInstances;
            RestoreBox.SelectedIndex = -1;
        }

        UpdateCustomUrlVisibility();
        UpdateFormMode();
    }

    private void RestoreBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _isRestoreMode = RestoreBox.SelectedItem is MessengerInstance;
        UpdateFormMode();
    }

    private void PlatformBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateCustomUrlVisibility();
    }

    private void CategoryBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoryBox.SelectedItem is WorkspaceCategoryOption option)
        {
            ResultCategory = option.Category;
        }
    }

    private void UpdateFormMode()
    {
        var enableNewFields = !_isRestoreMode;
        DisplayNameBox.IsEnabled = enableNewFields;
        PlatformBox.IsEnabled = enableNewFields;
        CategoryBox.IsEnabled = enableNewFields;
        CustomUrlBox.IsEnabled = enableNewFields;
    }

    private void UpdateCustomUrlVisibility()
    {
        if (PlatformBox.SelectedItem is PlatformDefinition platform)
        {
            CustomUrlBox.Visibility = platform.Id == "generic"
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (platform.Id != "generic" && string.IsNullOrWhiteSpace(CustomUrlBox.Text))
            {
                CustomUrlBox.PlaceholderText = platform.DefaultUrl;
            }
        }
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ValidationMessage.Visibility = Visibility.Collapsed;

        if (_isRestoreMode && RestoreBox.SelectedItem is MessengerInstance archived)
        {
            ResultRestoreInstanceId = archived.Id;
            return;
        }

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

        var customUrl = string.IsNullOrWhiteSpace(CustomUrlBox.Text) ? null : CustomUrlBox.Text.Trim();

        if (platform.Id == "generic" && string.IsNullOrWhiteSpace(customUrl))
        {
            ShowValidation("Enter a custom URL for this platform.");
            args.Cancel = true;
            return;
        }

        if (!string.IsNullOrWhiteSpace(customUrl))
        {
            if (!Uri.TryCreate(customUrl, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("https" or "http"))
            {
                ShowValidation("Custom URL must be a valid http or https address.");
                args.Cancel = true;
                return;
            }
        }

        ResultDisplayName = DisplayNameBox.Text.Trim();
        ResultPlatformId = platform.Id;
        ResultCustomUrl = customUrl;

        if (CategoryBox.SelectedItem is WorkspaceCategoryOption categoryOption)
        {
            ResultCategory = categoryOption.Category;
        }
    }

    private sealed record WorkspaceCategoryOption(WorkspaceCategory Category, string Label);

    private void ShowValidation(string message)
    {
        ValidationMessage.Text = message;
        ValidationMessage.Visibility = Visibility.Visible;
    }
}
