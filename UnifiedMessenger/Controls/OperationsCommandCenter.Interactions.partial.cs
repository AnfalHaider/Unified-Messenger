using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Controls.Occ;
using UnifiedMessenger.Models;
using UnifiedMessenger.Presenters;
using UnifiedMessenger.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.System;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
    private void KanbanBoard_ColumnOrderChanged(
        object sender,
        (string ColumnKey, IReadOnlyList<string> OrderedThreadIds) e) =>
        _services.ThreadDisplayOrder.UpdateColumnOrder(e.ColumnKey, e.OrderedThreadIds);

    private void KanbanBoard_ItemClickRequested(object sender, OperationsThreadCardViewModel card)
    {
        if (!string.IsNullOrWhiteSpace(card.InstanceId))
        {
            _services.Navigation.OpenInstance(
                card.InstanceId,
                card.ConversationKey,
                card.CustomerName);
            MaybeShowThreadClickTeachingTip(KanbanBoard);
        }
    }

    private void KanbanBoard_CrossColumnDragAttempted(object sender, EventArgs e)
    {
        if (_services.AppSettings.Settings.OccKanbanColumnTeachingDismissed)
        {
            return;
        }

        OccTeachingTipHelper.ShowTeachingTip(
            KanbanBoard,
            "Status controls column",
            "Drag reorder only changes display priority within a column. Thread status still controls which column a conversation appears in.");
        _ = _services.AppSettings.UpdateAsync(settings => settings.OccKanbanColumnTeachingDismissed = true);
    }

    private void KanbanBoard_ThreadContextMenuRequested(
        object sender,
        (OperationsThreadCardViewModel Card, ListView SourceList, Point Position) e) =>
        ShowThreadContextMenu(e.Card, e.SourceList, e.Position, ResolveKanbanColumnKey(e.SourceList));

    private void ImmediateQueueList_DragItemsCompleted(object sender, DragItemsCompletedEventArgs e)
    {
        if (!_viewModel.IsLayoutEditMode || _viewModel.ImmediateQueue.Count == 0)
        {
            return;
        }

        var orderedIds = _viewModel.ImmediateQueue.Select(card => card.ThreadId).ToList();
        _services.ThreadDisplayOrder.UpdateColumnOrder(
            ThreadDisplayOrderService.ImmediateColumnKey,
            orderedIds);
    }

    private void ThreadCardList_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not ListView listView)
        {
            return;
        }

        var card = ResolveThreadCardFromList(listView, e.OriginalSource);
        if (card is null)
        {
            return;
        }

        ShowThreadContextMenu(
            card,
            listView,
            e.GetPosition(listView),
            ThreadDisplayOrderService.ImmediateColumnKey);
    }

    private void ShowThreadContextMenu(
        OperationsThreadCardViewModel card,
        ListView listView,
        Point position,
        string? columnKey)
    {
        var flyout = new MenuFlyout();
        var openItem = new MenuFlyoutItem { Text = "Open thread" };
        openItem.Click += (_, _) => _services.Navigation.OpenInstance(
            card.InstanceId,
            card.ConversationKey,
            card.CustomerName);
        var copyItem = new MenuFlyoutItem { Text = "Copy summary" };
        copyItem.Click += (_, _) =>
        {
            var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            package.SetText(card.NextActionSummary);
            Clipboard.SetContent(package);
        };
        var moveItem = new MenuFlyoutItem
        {
            Text = "Move to top",
            IsEnabled = !string.IsNullOrWhiteSpace(columnKey)
        };
        moveItem.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(columnKey))
            {
                return;
            }

            _services.ThreadDisplayOrder.MoveToTop(columnKey, card.ThreadId);
            _services.DashboardRefresh.ScheduleRefresh();
        };
        flyout.Items.Add(openItem);
        flyout.Items.Add(copyItem);
        flyout.Items.Add(moveItem);
        flyout.ShowAt(listView, position);
    }

    private static OperationsThreadCardViewModel? ResolveThreadCardFromList(ListView listView, object originalSource)
    {
        if (originalSource is FrameworkElement element &&
            element.DataContext is OperationsThreadCardViewModel card)
        {
            return card;
        }

        return listView.SelectedItem as OperationsThreadCardViewModel;
    }

    private string? ResolveKanbanColumnKey(ListView listView) =>
        KanbanBoard.ResolveColumnKey(listView);

    private void OnOccKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var isControlDown = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (_viewModel.IsLayoutEditMode &&
            IsLayoutUndoShortcut(e.Key, isControlDown) &&
            _layoutUndoSnapshot is not null)
        {
            _ = TryUndoLayoutChangeAsync();
            e.Handled = true;
            return;
        }

        var isAltDown = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (!_viewModel.IsLayoutEditMode || !IsKeyboardReorderKey(e.Key, isAltDown))
        {
            return;
        }

        var focusedList = FindFocusedListView();
        if (focusedList is null || focusedList.SelectedItem is not OperationsThreadCardViewModel selected)
        {
            return;
        }

        var collection = focusedList.ItemsSource as ObservableCollection<OperationsThreadCardViewModel>;
        if (collection is null)
        {
            return;
        }

        var index = collection.IndexOf(selected);
        if (index < 0)
        {
            return;
        }

        var targetIndex = e.Key == VirtualKey.Up ? index - 1 : index + 1;
        if (targetIndex < 0 || targetIndex >= collection.Count)
        {
            return;
        }

        collection.Move(index, targetIndex);
        var columnKey = ReferenceEquals(focusedList, ImmediateQueueList)
            ? ThreadDisplayOrderService.ImmediateColumnKey
            : ResolveKanbanColumnKey(focusedList);
        if (!string.IsNullOrWhiteSpace(columnKey))
        {
            _services.ThreadDisplayOrder.UpdateColumnOrder(
                columnKey,
                collection.Select(card => card.ThreadId).ToList());
        }

        e.Handled = true;
    }

    internal static bool IsKeyboardReorderKey(VirtualKey key, bool isAltDown) =>
        isAltDown && key is VirtualKey.Up or VirtualKey.Down;

    private ListView? FindFocusedListView()
    {
        if (XamlRoot is null)
        {
            return null;
        }

        var focused = FocusManager.GetFocusedElement(XamlRoot);
        while (focused is DependencyObject current and not ListView)
        {
            focused = VisualTreeHelper.GetParent(current);
        }

        return focused as ListView;
    }

    private void MaybeShowTeachingTips()
    {
        if (_services.AppSettings.Settings.OccBranchPillTeachingDismissed)
        {
            return;
        }

        OccTeachingTipHelper.ShowTeachingTip(
            BranchWorkspacePillBar,
            "Branch workspace",
            "Use branch pills to scope KPIs, kanban columns, and the immediate queue.");
        _ = _services.AppSettings.UpdateAsync(settings => settings.OccBranchPillTeachingDismissed = true);
    }

    private void MaybeShowThreadClickTeachingTip(FrameworkElement? target)
    {
        if (target is null || _services.AppSettings.Settings.OccThreadClickTeachingDismissed)
        {
            return;
        }

        OccTeachingTipHelper.ShowTeachingTip(
            target,
            "Open threads quickly",
            "Click a thread card to jump to that conversation in the matching account.");
        _ = _services.AppSettings.UpdateAsync(settings => settings.OccThreadClickTeachingDismissed = true);
    }

    private void ApplyBackfillStatusUi()
    {
        var status = OccBackfillStatusPresenter.BuildStatus(_professionalInstances);
        _viewModel.ApplyBackfillStatus(status);
        BackfillStatusText.Text = _viewModel.BackfillStatusText;
        BackfillStatusPanel.Visibility = _viewModel.ShowBackfillStatus
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
