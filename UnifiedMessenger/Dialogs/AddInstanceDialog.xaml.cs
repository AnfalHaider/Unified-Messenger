using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using FocusTrapHelper = UnifiedMessenger.Services.FocusTrapHelper;

namespace UnifiedMessenger.Dialogs;

public sealed partial class AddInstanceDialog : ContentDialog
{
    private readonly IReadOnlyList<MessengerInstance> _archivedInstances;
    private bool _isRestoreMode;
    private FocusTrapHelper? _focusTrap;

    public AddInstanceDialog(IReadOnlyList<MessengerInstance> archivedInstances)
    {
        _archivedInstances = archivedInstances ?? [];
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
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

        if (AddInstanceDialogHelper.ShouldShowRestorePicker(_archivedInstances.Count))
        {
            RestoreBox.Visibility = Visibility.Visible;
            RestoreBox.ItemsSource = _archivedInstances;
            RestoreBox.SelectedIndex = -1;
        }

        UpdateCustomUrlVisibility();
        UpdateFormMode();
        _focusTrap?.Dispose();
        _focusTrap = FocusTrapHelper.Activate(this);
        DisplayNameBox.Focus(FocusState.Programmatic);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        _focusTrap?.Dispose();
        _focusTrap = null;
    }

    private void RestoreBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _isRestoreMode = AddInstanceDialogHelper.IsRestoreMode(RestoreBox.SelectedItem as MessengerInstance);
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
        var enableNewFields = AddInstanceDialogHelper.ShouldEnableNewInstanceFields(_isRestoreMode);
        DisplayNameBox.IsEnabled = enableNewFields;
        PlatformBox.IsEnabled = enableNewFields;
        CategoryBox.IsEnabled = enableNewFields;
        CustomUrlBox.IsEnabled = enableNewFields;
    }

    private void UpdateCustomUrlVisibility()
    {
        if (PlatformBox.SelectedItem is PlatformDefinition platform)
        {
            CustomUrlBox.Visibility = AddInstanceDialogHelper.ShouldShowCustomUrlField(platform.Id)
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (!AddInstanceDialogHelper.ShouldShowCustomUrlField(platform.Id)
                && string.IsNullOrWhiteSpace(CustomUrlBox.Text))
            {
                CustomUrlBox.PlaceholderText = platform.DefaultUrl;
            }
        }
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ValidationMessage.Visibility = Visibility.Collapsed;

        var category = CategoryBox.SelectedItem is WorkspaceCategoryOption categoryOption
            ? categoryOption.Category
            : ResultCategory;

        var submission = AddInstanceDialogHelper.ValidatePrimaryAction(
            RestoreBox.SelectedItem as MessengerInstance,
            DisplayNameBox.Text,
            PlatformBox.SelectedItem as PlatformDefinition,
            CustomUrlBox.Text,
            category);

        if (!submission.IsValid)
        {
            ShowValidation(submission.ValidationMessage ?? "Could not add this instance.");
            args.Cancel = true;
            return;
        }

        ResultRestoreInstanceId = submission.RestoreInstanceId;
        ResultDisplayName = submission.DisplayName;
        ResultPlatformId = submission.PlatformId;
        ResultCustomUrl = submission.CustomUrl;
        ResultCategory = submission.Category;
    }

    private sealed record WorkspaceCategoryOption(WorkspaceCategory Category, string Label);

    private void ShowValidation(string message)
    {
        ValidationMessage.Text = message;
        ValidationMessage.Visibility = Visibility.Visible;
    }
}
