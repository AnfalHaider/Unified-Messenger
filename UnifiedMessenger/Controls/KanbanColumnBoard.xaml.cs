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

    private OperationsThreadCardViewModel? _dragSourceCard;

    public KanbanColumnBoard()
    {
        InitializeComponent();
    }

    public event EventHandler<OperationsThreadCardViewModel>? ItemClickRequested;

    public event EventHandler<(string ColumnKey, IReadOnlyList<string> OrderedThreadIds)>? ColumnOrderChanged;

    public event EventHandler? CrossColumnDragAttempted;

    public event EventHandler<(OperationsThreadCardViewModel Card, string SourceColumnKey, string TargetColumnKey)>?
        ColumnTransferRequested;

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
        SetColumnEmptyState(NewInquiriesEmptyPanel, NewInquiriesList, newCount);
        SetColumnEmptyState(HangingLeadsEmptyPanel, HangingLeadsList, hangingCount);
        SetColumnEmptyState(ResolvedEmptyPanel, ResolvedList, resolvedCount);

        UpdateDropZoneChrome(NewColumnDropZone, newCount);
        UpdateDropZoneChrome(HangingColumnDropZone, hangingCount);
        UpdateDropZoneChrome(ResolvedColumnDropZone, resolvedCount);
    }

    private static void SetColumnEmptyState(Border emptyPanel, ListView listView, int count)
    {
        var isEmpty = count == 0;
        emptyPanel.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        listView.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
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

        foreach (var dropZone in new[] { NewColumnDropZone, HangingColumnDropZone, ResolvedColumnDropZone })
        {
            dropZone.BorderThickness = new Thickness(1);
            dropZone.Opacity = 1.0;
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
        _dragSourceCard = e.Items.FirstOrDefault() as OperationsThreadCardViewModel;
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
            string.IsNullOrWhiteSpace(targetColumn))
        {
            e.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Move;
        e.DragUIOverride.IsGlyphVisible = false;

        if (targetColumn.Equals(_dragSourceColumnKey, StringComparison.OrdinalIgnoreCase))
        {
            e.DragUIOverride.Caption = string.Empty;
            return;
        }

        e.DragUIOverride.Caption = $"Move to {ResolveColumnLabel(targetColumn)}";
    }

    private void KanbanList_Drop(object sender, DragEventArgs e)
    {
        if (!IsReorderEnabled ||
            sender is not ListView targetList ||
            _dragSourceCard is null ||
            string.IsNullOrWhiteSpace(_dragSourceColumnKey))
        {
            e.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        var targetColumn = ResolveColumnKey(targetList);
        if (string.IsNullOrWhiteSpace(targetColumn))
        {
            e.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        if (targetColumn.Equals(_dragSourceColumnKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Move;
        CrossColumnInfoBar.IsOpen = false;
        ColumnTransferRequested?.Invoke(
            this,
            (_dragSourceCard, _dragSourceColumnKey, targetColumn));
        CrossColumnDragAttempted?.Invoke(this, EventArgs.Empty);
    }

    private void KanbanList_DragItemsCompleted(object sender, DragItemsCompletedEventArgs e)
    {
        if (!IsReorderEnabled || sender is not ListView listView)
        {
            _dragSourceColumnKey = null;
            _dragSourceCard = null;
            return;
        }

        var columnKey = ResolveColumnKeyInternal(listView);
        if (string.IsNullOrWhiteSpace(columnKey) ||
            listView.ItemsSource is not IEnumerable<OperationsThreadCardViewModel> items)
        {
            _dragSourceColumnKey = null;
            _dragSourceCard = null;
            return;
        }

        var orderedIds = items.Select(card => card.ThreadId).ToList();
        ColumnOrderChanged?.Invoke(this, (columnKey, orderedIds));
        _dragSourceColumnKey = null;
        _dragSourceCard = null;
    }

    private static string ResolveColumnLabel(string columnKey) =>
        columnKey switch
        {
            nameof(UnifiedMessengerKanbanColumn.NewInquiries) => "New",
            nameof(UnifiedMessengerKanbanColumn.HangingLeads) => "Hanging",
            nameof(UnifiedMessengerKanbanColumn.Resolved) => "Resolved",
            _ => columnKey
        };

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
        var minHeight = Application.Current.Resources.TryGetValue("UmKanbanColumnMinHeight", out var token) &&
                        token is double baseHeight
            ? baseHeight
            : 120;
        dropZone.MinHeight = IsReorderEnabled && itemCount == 0 ? minHeight + 40 : minHeight;
    }

    public string? ResolveColumnKey(ListView listView) => ResolveColumnKeyInternal(listView);

    public void WireScrollBubbling(ScrollViewer parentScrollViewer)
    {
        ScrollInputHelper.EnableVerticalScrollBubbling(NewInquiriesList, parentScrollViewer);
        ScrollInputHelper.EnableVerticalScrollBubbling(HangingLeadsList, parentScrollViewer);
        ScrollInputHelper.EnableVerticalScrollBubbling(ResolvedList, parentScrollViewer);
    }
}
