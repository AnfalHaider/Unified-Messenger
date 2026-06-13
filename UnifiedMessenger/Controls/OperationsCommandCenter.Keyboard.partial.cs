using System.Collections.ObjectModel;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.ViewModels;
using Windows.System;
using Windows.UI.Core;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
    private string? _keyboardMoveColumnKey;
    private string? _keyboardMoveThreadId;

    private void WireKanbanKeyboardShortcuts()
    {
        KeyDown -= OnKanbanKeyDown;
        KeyDown += OnKanbanKeyDown;
    }

    private void OnKanbanItemSelectedForKeyboardMove(object? sender, OperationsThreadCardViewModel card)
    {
        _keyboardMoveThreadId = card.ThreadId;
        _keyboardMoveColumnKey = ResolveColumnKeyForThread(card.ThreadId);
    }

    private void OnKanbanKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.OriginalSource is TextBox or AutoSuggestBox)
        {
            return;
        }

        if (e.Key == VirtualKey.Escape)
        {
            if (_keyboardMoveThreadId is not null)
            {
                ClearKeyboardMoveSelection();
                e.Handled = true;
            }

            return;
        }

        if (KeyboardShortcutService.ShouldHandleShortcut(() => _keyboardMoveThreadId is not null) &&
            e.Key is VirtualKey.Up or VirtualKey.Down &&
            IsAltDown())
        {
            var delta = e.Key == VirtualKey.Up ? -1 : 1;
            if (TryApplyKeyboardReorder(delta))
            {
                e.Handled = true;
            }
        }
    }

    private bool TryApplyKeyboardReorder(int delta)
    {
        if (string.IsNullOrWhiteSpace(_keyboardMoveColumnKey) ||
            string.IsNullOrWhiteSpace(_keyboardMoveThreadId))
        {
            return false;
        }

        var collection = ResolveKanbanCollection(_keyboardMoveColumnKey);
        if (collection is null || collection.Count < 2)
        {
            return false;
        }

        var orderedIds = collection.Select(card => card.ThreadId).ToList();
        if (!OccKanbanKeyboardReorderHelper.TryMoveSelection(
                orderedIds,
                _keyboardMoveThreadId,
                delta,
                out var newOrder))
        {
            return false;
        }

        _services.ThreadDisplayOrder.UpdateColumnOrder(_keyboardMoveColumnKey, newOrder);
        ReorderKanbanCollection(collection, newOrder);
        return true;
    }

    private void ReorderKanbanCollection(
        ObservableCollection<OperationsThreadCardViewModel> collection,
        IReadOnlyList<string> orderedThreadIds)
    {
        var lookup = collection.ToDictionary(card => card.ThreadId, StringComparer.OrdinalIgnoreCase);
        collection.Clear();
        foreach (var threadId in orderedThreadIds)
        {
            if (lookup.TryGetValue(threadId, out var card))
            {
                collection.Add(card);
            }
        }
    }

    private ObservableCollection<OperationsThreadCardViewModel>? ResolveKanbanCollection(string columnKey)
    {
        if (columnKey.Equals(
                ThreadDisplayOrderService.GetColumnKey(UnifiedMessengerKanbanColumn.NewInquiries),
                StringComparison.OrdinalIgnoreCase))
        {
            return _viewModel.NewInquiries;
        }

        if (columnKey.Equals(
                ThreadDisplayOrderService.GetColumnKey(UnifiedMessengerKanbanColumn.HangingLeads),
                StringComparison.OrdinalIgnoreCase))
        {
            return _viewModel.HangingLeads;
        }

        if (columnKey.Equals(
                ThreadDisplayOrderService.GetColumnKey(UnifiedMessengerKanbanColumn.Resolved),
                StringComparison.OrdinalIgnoreCase))
        {
            return _viewModel.Resolved;
        }

        return null;
    }

    private string? ResolveColumnKeyForThread(string threadId)
    {
        if (_viewModel.NewInquiries.Any(card =>
                card.ThreadId.Equals(threadId, StringComparison.OrdinalIgnoreCase)))
        {
            return ThreadDisplayOrderService.GetColumnKey(UnifiedMessengerKanbanColumn.NewInquiries);
        }

        if (_viewModel.HangingLeads.Any(card =>
                card.ThreadId.Equals(threadId, StringComparison.OrdinalIgnoreCase)))
        {
            return ThreadDisplayOrderService.GetColumnKey(UnifiedMessengerKanbanColumn.HangingLeads);
        }

        if (_viewModel.Resolved.Any(card =>
                card.ThreadId.Equals(threadId, StringComparison.OrdinalIgnoreCase)))
        {
            return ThreadDisplayOrderService.GetColumnKey(UnifiedMessengerKanbanColumn.Resolved);
        }

        return null;
    }

    private void ClearKeyboardMoveSelection()
    {
        _keyboardMoveColumnKey = null;
        _keyboardMoveThreadId = null;
    }

    private static bool IsAltDown() =>
        InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu)
            .HasFlag(CoreVirtualKeyStates.Down);
}
