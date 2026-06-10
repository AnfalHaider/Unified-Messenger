using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Controls.Occ;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
    private void BranchMetricsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not BranchMetricViewModel metric)
        {
            return;
        }

        _workspaceBranchKey = metric.BranchName;
        _showWorkspaceLoading = true;
        SelectWorkspacePill(metric.BranchName);
        _ = RefreshAsync(_professionalInstances, _registry);
    }

    private void SelectWorkspacePill(string? branchName)
    {
        _suppressPillSelection = true;
        BranchWorkspacePillBar.SelectBranchKey(branchName);
        _suppressPillSelection = false;
    }

    public void SelectWorkspaceBranch(string? branchKey)
    {
        if (string.Equals(_workspaceBranchKey, branchKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _workspaceBranchKey = branchKey;
        _showWorkspaceLoading = true;
        SelectWorkspacePill(branchKey);
        ApplyBranchFilterChip();
        if (_registry is not null)
        {
            _ = RefreshAsync(_professionalInstances, _registry);
        }
    }

    public void RequestImmediateLaneFocus()
    {
        ImmediateLaneSection?.StartBringIntoView();
    }

    private void RefreshBranchMetricSelection()
    {
        var selected = _workspaceBranchKey;
        var isScoped = IsWorkspaceBranchScoped();
        for (var index = 0; index < _branchMetrics.Count; index++)
        {
            var metric = _branchMetrics[index];
            var isSelected = !string.IsNullOrWhiteSpace(selected) &&
                             metric.BranchName.Equals(selected, StringComparison.OrdinalIgnoreCase);
            if (metric.IsSelected != isSelected || metric.IsWorkspaceScoped != isScoped)
            {
                _branchMetrics[index] = new BranchMetricViewModel(metric.Source, isSelected, isScoped);
            }
        }
    }

    private void ThreadCardList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is OperationsThreadCardViewModel card &&
            !string.IsNullOrWhiteSpace(card.InstanceId))
        {
            _services.Navigation.OpenInstance(
                card.InstanceId,
                card.ConversationKey,
                card.CustomerName);
            MaybeShowThreadClickTeachingTip(sender as FrameworkElement);
        }
    }

    private void InsightFeedList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not OperationsInsightFeedViewModel item)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(item.InstanceId))
        {
            _services.Navigation.OpenInstance(
                item.InstanceId,
                item.ConversationKey,
                item.CustomerName);
        }
    }

    private void OperationalHighlightsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is OperationalHighlightViewModel highlight &&
            !string.IsNullOrWhiteSpace(highlight.InstanceId))
        {
            _services.Navigation.OpenInstance(
                highlight.InstanceId,
                highlight.ConversationKey,
                highlight.Title);
        }
    }

    private void GoogleReviewAlertsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedReviewAlert = GoogleReviewAlertsList.SelectedItem as GoogleReviewAlertView;
        if (_selectedReviewAlert is not null && string.IsNullOrWhiteSpace(ReviewReplyBox.Text))
        {
            ReviewReplyBox.Text = $"Hi {_selectedReviewAlert.ReviewerName}, thank you for your feedback. ";
        }
    }

    private async void SubmitReviewReplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedReviewAlert is null)
        {
            await ShowSimpleDialogAsync(
                "Select a review",
                "Choose a Google review alert from the list before inserting a draft reply.")
                .ConfigureAwait(true);
            return;
        }

        var replyText = ReviewReplyBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(replyText))
        {
            await ShowSimpleDialogAsync(
                "Draft reply is empty",
                "Type a reply in the text box, then choose Insert draft.")
                .ConfigureAwait(true);
            return;
        }

        _services.Navigation.OpenInstance(_selectedReviewAlert.InstanceId);

        try
        {
            await _services.WebViewScriptGateway.ExecuteAsync(
                _selectedReviewAlert.InstanceId,
                "__umSubmitReviewReply",
                [_selectedReviewAlert.ReviewId, replyText]).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await ShowSimpleDialogAsync("Could not insert draft", ex.Message).ConfigureAwait(true);
        }
    }

    private async void OpenSelectedReviewInstanceButton_Click(object sender, RoutedEventArgs e)
    {
        var instanceId = _selectedReviewAlert?.InstanceId ??
            _snapshot.PlatformIntelligence.GoogleInstanceIds.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(instanceId))
        {
            await ShowSimpleDialogAsync(
                "No Google instance",
                "Add a Google Business professional account to respond to reviews.")
                .ConfigureAwait(true);
            return;
        }

        _services.Navigation.OpenInstance(instanceId);
    }

    private void AddProfessionalInstanceButton_Click(object sender, RoutedEventArgs e) =>
        _services.Navigation.RequestAddInstance();

    private async void RefreshCommandButton_Click(object sender, RoutedEventArgs e) =>
        await RunRefreshCommandAsync(RefreshCommandButton).ConfigureAwait(true);

    private async void RefreshPlatformDataButton_Click(object sender, RoutedEventArgs e) =>
        await RunRefreshCommandAsync(RefreshPlatformDataButton).ConfigureAwait(true);

    private async Task RunRefreshCommandAsync(Button button)
    {
        button.IsEnabled = false;
        var originalContent = button.Content;
        button.Content = "Refreshing…";
        try
        {
            await RequestPlatformDataRefreshAsync().ConfigureAwait(true);
        }
        finally
        {
            button.IsEnabled = true;
            button.Content = originalContent;
        }
    }

    private async void ExportAnalyticsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_registry is null)
        {
            await ShowSimpleDialogAsync(
                "Export unavailable",
                "Instance registry is not loaded yet. Try again after the dashboard finishes loading.")
                .ConfigureAwait(true);
            return;
        }

        var picker = new Windows.Storage.Pickers.FileSavePicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow!);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        picker.SuggestedFileName = $"unified-messenger-analytics-{DateTime.Now:yyyyMMdd}";
        picker.FileTypeChoices.Add("JSON", [".json"]);
        picker.FileTypeChoices.Add("CSV", [".csv"]);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            if (_services.MessageAnalytics is not MessageAnalyticsService analyticsService)
            {
                await ShowSimpleDialogAsync("Export unavailable", "Analytics service is not ready.").ConfigureAwait(true);
                return;
            }

            if (file.FileType.Equals(".csv", StringComparison.OrdinalIgnoreCase))
            {
                await analyticsService.ExportCsvAsync(_snapshot.FilteredInstances, file.Path);
            }
            else
            {
                await analyticsService.ExportToFileAsync(file.Path);
            }

            await ShowSimpleDialogAsync("Export complete", $"Analytics saved to {file.Name}.").ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await ShowSimpleDialogAsync("Export failed", ex.Message).ConfigureAwait(true);
        }
    }

    private async Task ShowSimpleDialogAsync(string title, string message)
    {
        if (XamlRoot is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
    }
}
