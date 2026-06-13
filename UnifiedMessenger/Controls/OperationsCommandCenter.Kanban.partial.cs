using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Presenters;
using UnifiedMessenger.Services;
using UnifiedMessenger.ViewModels;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
    private bool ShouldHideBranchOnCards() =>
        !string.IsNullOrWhiteSpace(WorkspaceBranchKey);

    private void ApplyKanban(UnifiedMessengerDashboardSnapshot threadOps)
    {
        var hideBranch = ShouldHideBranchOnCards();

        SyncThreadCards(
            _viewModel.NewInquiries,
            OccThreadCardPresenter
                .BuildKanbanColumn(threadOps.AllThreads, UnifiedMessengerKanbanColumn.NewInquiries, hideBranch)
                .ToList());
        SyncThreadCards(
            _viewModel.HangingLeads,
            OccThreadCardPresenter
                .BuildKanbanColumn(threadOps.AllThreads, UnifiedMessengerKanbanColumn.HangingLeads, hideBranch)
                .ToList());
        SyncThreadCards(
            _viewModel.Resolved,
            OccThreadCardPresenter
                .BuildKanbanColumn(threadOps.AllThreads, UnifiedMessengerKanbanColumn.Resolved, hideBranch)
                .ToList());

        KanbanBoard.UpdateEmptyStates(
            _viewModel.NewInquiries.Count,
            _viewModel.HangingLeads.Count,
            _viewModel.Resolved.Count);
    }

    private void ApplyImmediateQueue(UnifiedMessengerDashboardSnapshot threadOps)
    {
        var hideBranch = ShouldHideBranchOnCards();
        SyncThreadCards(
            _viewModel.ImmediateQueue,
            OccThreadCardPresenter.BuildThreadCards(threadOps.ImmediateActionQueue, hideBranch));

        _viewModel.ApplyImmediateQueuePresentation(
            OccSnapshotPresenter.BuildImmediateQueuePresentation(threadOps));
        ImmediateQueueEmptyText.Visibility = _viewModel.ShowImmediateQueueEmpty
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static void SyncThreadCards(
        ObservableCollection<OperationsThreadCardViewModel> target,
        IReadOnlyList<OperationsThreadCardViewModel> source) =>
        ObservableCollectionSyncHelper.Sync(
            target,
            source,
            card => card.ThreadId,
            OperationsThreadCardSync.ContentEquals);

    private void ThreadCardList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is OperationsThreadCardViewModel card &&
            !string.IsNullOrWhiteSpace(card.InstanceId))
        {
            _services.Navigation.OpenInstance(
                card.InstanceId,
                card.ConversationKey,
                card.CustomerName);
        }
    }

    private void KanbanBoard_ColumnOrderChanged(
        object sender,
        (string ColumnKey, IReadOnlyList<string> OrderedThreadIds) e) =>
        _services.ThreadDisplayOrder.UpdateColumnOrder(e.ColumnKey, e.OrderedThreadIds);

    private async void KanbanBoard_ColumnTransferRequested(
        object sender,
        (OperationsThreadCardViewModel Card, string SourceColumnKey, string TargetColumnKey) e)
    {
        if (!Enum.TryParse<UnifiedMessengerKanbanColumn>(e.TargetColumnKey, out var targetColumn))
        {
            return;
        }

        _services.ThreadRegistry.SetThreadKanbanColumn(e.Card.ThreadId, targetColumn);
        _services.ThreadDisplayOrder.RemoveFromColumn(e.SourceColumnKey, e.Card.ThreadId);
        _services.ThreadDisplayOrder.MoveToTop(e.TargetColumnKey, e.Card.ThreadId);

        await RefreshAsync(_professionalInstances, _registry).ConfigureAwait(true);
    }

    private void KanbanBoard_ItemClickRequested(object sender, OperationsThreadCardViewModel card)
    {
        _keyboardMoveThreadId = card.ThreadId;
        _keyboardMoveColumnKey = ResolveColumnKeyForThread(card.ThreadId);

        if (!string.IsNullOrWhiteSpace(card.InstanceId))
        {
            _services.Navigation.OpenInstance(
                card.InstanceId,
                card.ConversationKey,
                card.CustomerName);
        }
    }

    private void KanbanBoard_CrossColumnDragAttempted(object sender, EventArgs e)
    {
        // Cross-column transfer is handled by ColumnTransferRequested.
    }
}
