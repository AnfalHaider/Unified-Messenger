using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace UnifiedMessenger.Controls;

public sealed partial class KanbanColumnBoard : UserControl
{
    private string? _dragSourceColumnKey;
    private OperationsThreadCardViewModel? _dragSourceCard;
    private ScrollViewer? _parentScrollViewer;

    public KanbanColumnBoard()
    {
        InitializeComponent();
        foreach (var repeater in EnumerateKanbanRepeaters())
        {
            repeater.ElementPrepared += KanbanRepeater_ElementPrepared;
        }
    }

    public event EventHandler<OperationsThreadCardViewModel>? ItemClickRequested;

    public event EventHandler<(string ColumnKey, IReadOnlyList<string> OrderedThreadIds)>? ColumnOrderChanged;

    public event EventHandler? CrossColumnDragAttempted;

    public event EventHandler<(OperationsThreadCardViewModel Card, string SourceColumnKey, string TargetColumnKey)>?
        ColumnTransferRequested;

    public event EventHandler<(OperationsThreadCardViewModel Card, ItemsRepeater SourceRepeater, Point Position)>?
        ThreadContextMenuRequested;

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

    private static void SetColumnEmptyState(Border emptyPanel, ItemsRepeater repeater, int count)
    {
        var isEmpty = count == 0;
        emptyPanel.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        repeater.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
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
        foreach (var dropZone in new[] { NewColumnDropZone, HangingColumnDropZone, ResolvedColumnDropZone })
        {
            dropZone.BorderThickness = new Thickness(1);
            dropZone.Opacity = 1.0;
        }
    }

    private void KanbanRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is not FrameworkElement element)
        {
            return;
        }

        if (_parentScrollViewer is not null)
        {
            ScrollInputHelper.EnableVerticalScrollBubbling(element, _parentScrollViewer);
        }

        element.IsTapEnabled = true;
        element.Tapped -= KanbanCard_Tapped;
        element.Tapped += KanbanCard_Tapped;
        element.RightTapped -= KanbanCard_RightTapped;
        element.RightTapped += KanbanCard_RightTapped;
        element.CanDrag = IsReorderEnabled;
        element.DragStarting -= KanbanCard_DragStarting;
        element.DragStarting += KanbanCard_DragStarting;
    }

    private void KanbanCard_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement element &&
            element.DataContext is OperationsThreadCardViewModel card)
        {
            ItemClickRequested?.Invoke(this, card);
        }
    }

    private void KanbanCard_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            element.DataContext is not OperationsThreadCardViewModel card)
        {
            return;
        }

        var repeater = FindParentRepeater(element);
        if (repeater is null)
        {
            return;
        }

        ThreadContextMenuRequested?.Invoke(this, (card, repeater, e.GetPosition(repeater)));
    }

    private void KanbanCard_DragStarting(UIElement sender, DragStartingEventArgs e)
    {
        if (!IsReorderEnabled || sender is not FrameworkElement element)
        {
            e.Cancel = true;
            return;
        }

        var repeater = FindParentRepeater(element);
        if (repeater is null)
        {
            e.Cancel = true;
            return;
        }

        _dragSourceColumnKey = ResolveColumnKey(repeater);
        _dragSourceCard = element.DataContext as OperationsThreadCardViewModel;
        e.Data.SetText(_dragSourceColumnKey ?? string.Empty);
        e.Data.RequestedOperation = DataPackageOperation.Move;
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        if (!IsReorderEnabled || sender is not Border dropZone)
        {
            e.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        var targetColumn = ResolveColumnKeyForDropZone(dropZone);
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

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (!IsReorderEnabled ||
            sender is not Border dropZone ||
            _dragSourceCard is null ||
            string.IsNullOrWhiteSpace(_dragSourceColumnKey))
        {
            e.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        var targetColumn = ResolveColumnKeyForDropZone(dropZone);
        if (string.IsNullOrWhiteSpace(targetColumn))
        {
            e.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        if (targetColumn.Equals(_dragSourceColumnKey, StringComparison.OrdinalIgnoreCase))
        {
            TryPublishColumnOrder(dropZone);
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Move;
        CrossColumnInfoBar.IsOpen = false;
        ColumnTransferRequested?.Invoke(
            this,
            (_dragSourceCard, _dragSourceColumnKey, targetColumn));
        CrossColumnDragAttempted?.Invoke(this, EventArgs.Empty);
    }

    private static string ResolveColumnLabel(string columnKey) =>
        columnKey switch
        {
            nameof(UnifiedMessengerKanbanColumn.NewInquiries) => "New",
            nameof(UnifiedMessengerKanbanColumn.HangingLeads) => "Hanging",
            nameof(UnifiedMessengerKanbanColumn.Resolved) => "Resolved",
            _ => columnKey
        };

    private static string? ResolveColumnKeyInternal(ItemsRepeater repeater) =>
        repeater.Name switch
        {
            nameof(NewInquiriesList) =>
                ThreadDisplayOrderService.GetColumnKey(UnifiedMessengerKanbanColumn.NewInquiries),
            nameof(HangingLeadsList) =>
                ThreadDisplayOrderService.GetColumnKey(UnifiedMessengerKanbanColumn.HangingLeads),
            nameof(ResolvedList) =>
                ThreadDisplayOrderService.GetColumnKey(UnifiedMessengerKanbanColumn.Resolved),
            _ => null
        };

    private static string? ResolveColumnKeyForDropZone(Border dropZone) =>
        dropZone.Name switch
        {
            nameof(NewColumnDropZone) =>
                ThreadDisplayOrderService.GetColumnKey(UnifiedMessengerKanbanColumn.NewInquiries),
            nameof(HangingColumnDropZone) =>
                ThreadDisplayOrderService.GetColumnKey(UnifiedMessengerKanbanColumn.HangingLeads),
            nameof(ResolvedColumnDropZone) =>
                ThreadDisplayOrderService.GetColumnKey(UnifiedMessengerKanbanColumn.Resolved),
            _ => null
        };

    private IEnumerable<ItemsRepeater> EnumerateKanbanRepeaters()
    {
        yield return NewInquiriesList;
        yield return HangingLeadsList;
        yield return ResolvedList;
    }

    private static ItemsRepeater? FindParentRepeater(DependencyObject start)
    {
        for (var current = start; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is ItemsRepeater repeater)
            {
                return repeater;
            }
        }

        return null;
    }

    private ItemsRepeater? ResolveRepeaterForDropZone(Border dropZone) =>
        dropZone.Name switch
        {
            nameof(NewColumnDropZone) => NewInquiriesList,
            nameof(HangingColumnDropZone) => HangingLeadsList,
            nameof(ResolvedColumnDropZone) => ResolvedList,
            _ => null
        };

    private void TryPublishColumnOrder(Border dropZone)
    {
        var repeater = ResolveRepeaterForDropZone(dropZone);
        if (repeater is null ||
            ResolveColumnKeyForDropZone(dropZone) is not { Length: > 0 } columnKey ||
            repeater.ItemsSourceView is not IEnumerable<OperationsThreadCardViewModel> items)
        {
            return;
        }

        ColumnOrderChanged?.Invoke(this, (columnKey, items.Select(card => card.ThreadId).ToList()));
    }

    private void UpdateDropZoneChrome(Border dropZone, int itemCount)
    {
        var minHeight = Application.Current.Resources.TryGetValue("UmKanbanColumnMinHeight", out var token) &&
                        token is double baseHeight
            ? baseHeight
            : 120;
        dropZone.MinHeight = IsReorderEnabled && itemCount == 0 ? minHeight + 40 : minHeight;
    }

    public string? ResolveColumnKey(ItemsRepeater repeater) => ResolveColumnKeyInternal(repeater);

    public void WireScrollBubbling(ScrollViewer parentScrollViewer)
    {
        _parentScrollViewer = parentScrollViewer;
    }
}
