using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace UnifiedMessenger.Controls;

public sealed partial class KanbanColumnBoard : UserControl
{
    private string? _dragSourceColumnKey;

    public KanbanColumnBoard()
    {
        InitializeComponent();
    }

    public event EventHandler<OperationsThreadCardViewModel>? ItemClickRequested;

    public event EventHandler<(string ColumnKey, IReadOnlyList<string> OrderedThreadIds)>? ColumnOrderChanged;

    public event EventHandler? CrossColumnDragAttempted;

    public event EventHandler<(OperationsThreadCardViewModel Card, ListView SourceList, Point Position)>? ThreadContextMenuRequested;

    public bool IsReorderEnabled
    {
        get => (bool)GetValue(IsReorderEnabledProperty);
        set => SetValue(IsReorderEnabledProperty, value);
    }

    public static readonly DependencyProperty IsReorderEnabledProperty =
        DependencyProperty.Register(
            nameof(IsReorderEnabled),
            typeof(bool),
            typeof(KanbanColumnBoard),
            new PropertyMetadata(false, OnIsReorderEnabledChanged));

    public void BindCollections(
        ObservableCollection<OperationsThreadCardViewModel> newInquiries,
        ObservableCollection<OperationsThreadCardViewModel> hangingLeads,
        ObservableCollection<OperationsThreadCardViewModel> resolved)
    {
        NewInquiriesList.ItemsSource = newInquiries;
        HangingLeadsList.ItemsSource = hangingLeads;
        ResolvedList.ItemsSource = resolved;
    }

    public void UpdateEmptyStates(
        int newCount,
        int hangingCount,
        int resolvedCount)
    {
        NewInquiriesEmptyText.Visibility = newCount == 0 ? Visibility.Visible : Visibility.Collapsed;
        HangingLeadsEmptyText.Visibility = hangingCount == 0 ? Visibility.Visible : Visibility.Collapsed;
        ResolvedEmptyText.Visibility = resolvedCount == 0 ? Visibility.Visible : Visibility.Collapsed;

        UpdateDropZoneChrome(NewColumnDropZone, newCount);
        UpdateDropZoneChrome(HangingColumnDropZone, hangingCount);
        UpdateDropZoneChrome(ResolvedColumnDropZone, resolvedCount);
    }

    private static void OnIsReorderEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KanbanColumnBoard board)
        {
            board.ApplyReorderMode((bool)e.NewValue);
        }
    }

    private void ApplyReorderMode(bool enabled)
    {
        foreach (var list in EnumerateKanbanLists())
        {
            list.CanDragItems = enabled;
            list.CanReorderItems = enabled;
            list.IsSwipeEnabled = enabled;
        }

        var dashStyle = enabled ? "Dashed" : "Solid";
        foreach (var dropZone in new[] { NewColumnDropZone, HangingColumnDropZone, ResolvedColumnDropZone })
        {
            dropZone.BorderThickness = new Thickness(1);
            dropZone.Opacity = enabled ? 1.0 : 1.0;
        }
    }

    private void KanbanList_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not ListView listView)
        {
            return;
        }

        if (e.OriginalSource is FrameworkElement element &&
            element.DataContext is OperationsThreadCardViewModel card)
        {
            ThreadContextMenuRequested?.Invoke(this, (card, listView, e.GetPosition(listView)));
        }
    }

    private void KanbanList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is OperationsThreadCardViewModel card)
        {
            ItemClickRequested?.Invoke(this, card);
        }
    }

    private void KanbanList_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (!IsReorderEnabled || sender is not ListView listView)
        {
            e.Cancel = true;
            return;
        }

        _dragSourceColumnKey = ResolveColumnKey(listView);
        e.Data.SetText(_dragSourceColumnKey ?? string.Empty);
        e.Data.RequestedOperation = DataPackageOperation.Move;
    }

    private void KanbanList_DragOver(object sender, DragEventArgs e)
    {
        if (!IsReorderEnabled || sender is not ListView targetList)
        {
            e.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        var targetColumn = ResolveColumnKey(targetList);
        if (string.IsNullOrWhiteSpace(_dragSourceColumnKey) ||
            string.IsNullOrWhiteSpace(targetColumn) ||
            targetColumn.Equals(_dragSourceColumnKey, StringComparison.OrdinalIgnoreCase))
        {
            e.AcceptedOperation = DataPackageOperation.Move;
            e.DragUIOverride.IsGlyphVisible = false;
            return;
        }

        e.AcceptedOperation = DataPackageOperation.None;
        e.DragUIOverride.Caption = "Status controls column";
        e.DragUIOverride.IsContentVisible = false;
        e.DragUIOverride.IsGlyphVisible = true;
        CrossColumnInfoBar.IsOpen = true;
        CrossColumnDragAttempted?.Invoke(this, EventArgs.Empty);
    }

    private void KanbanList_Drop(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.None;
        CrossColumnInfoBar.IsOpen = true;
        CrossColumnDragAttempted?.Invoke(this, EventArgs.Empty);
    }

    private void KanbanList_DragItemsCompleted(object sender, DragItemsCompletedEventArgs e)
    {
        if (!IsReorderEnabled || sender is not ListView listView)
        {
            return;
        }

        var columnKey = ResolveColumnKeyInternal(listView);
        if (string.IsNullOrWhiteSpace(columnKey) || listView.ItemsSource is not IEnumerable<OperationsThreadCardViewModel> items)
        {
            return;
        }

        var orderedIds = items.Select(card => card.ThreadId).ToList();
        ColumnOrderChanged?.Invoke(this, (columnKey, orderedIds));
        _dragSourceColumnKey = null;
    }

    private static string? ResolveColumnKeyInternal(ListView listView) =>
        listView.Name switch
        {
            nameof(NewInquiriesList) =>
                ThreadDisplayOrderService.GetColumnKey(UnifiedMessengerKanbanColumn.NewInquiries),
            nameof(HangingLeadsList) =>
                ThreadDisplayOrderService.GetColumnKey(UnifiedMessengerKanbanColumn.HangingLeads),
            nameof(ResolvedList) =>
                ThreadDisplayOrderService.GetColumnKey(UnifiedMessengerKanbanColumn.Resolved),
            _ => null
        };

    private IEnumerable<ListView> EnumerateKanbanLists()
    {
        yield return NewInquiriesList;
        yield return HangingLeadsList;
        yield return ResolvedList;
    }

    private void UpdateDropZoneChrome(Border dropZone, int itemCount)
    {
        dropZone.MinHeight = IsReorderEnabled && itemCount == 0 ? 160 : 120;
    }

    public string? ResolveColumnKey(ListView listView) => ResolveColumnKeyInternal(listView);

    public void WireScrollBubbling(ScrollViewer parentScrollViewer)
    {
        ScrollInputHelper.EnableVerticalScrollBubbling(NewInquiriesList, parentScrollViewer);
        ScrollInputHelper.EnableVerticalScrollBubbling(HangingLeadsList, parentScrollViewer);
        ScrollInputHelper.EnableVerticalScrollBubbling(ResolvedList, parentScrollViewer);
    }
}
