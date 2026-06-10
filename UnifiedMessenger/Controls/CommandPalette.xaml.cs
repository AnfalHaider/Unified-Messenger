using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using Windows.System;

namespace UnifiedMessenger.Controls;

public sealed partial class CommandPalette : UserControl
{
    public CommandPalette()
    {
        InitializeComponent();
        IsHitTestVisible = false;
        Visibility = Visibility.Collapsed;
    }

    public event EventHandler? CloseRequested;

    public event EventHandler<CommandPaletteSelection>? ItemSelected;

    private IReadOnlyList<CommandPaletteEntry> _allEntries = [];

    private FocusTrapHelper? _focusTrap;

    public void SetEntries(IReadOnlyList<CommandPaletteEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _allEntries = entries;
        ApplyFilter(SearchBox.Text);
    }

    public void Open()
    {
        Visibility = Visibility.Visible;
        IsHitTestVisible = true;
        SearchBox.Text = string.Empty;
        ApplyFilter(string.Empty);
        _focusTrap?.Dispose();
        _focusTrap = FocusTrapHelper.Activate(PalettePanel);
        SearchBox.Focus(FocusState.Programmatic);
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        _focusTrap?.Dispose();
        _focusTrap = null;
        Visibility = Visibility.Collapsed;
        IsHitTestVisible = false;
        SearchBox.Text = string.Empty;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    public bool IsOpen => Visibility == Visibility.Visible;

    private void ApplyFilter(string? query)
    {
        ResultsList.ItemsSource = CommandPaletteHelper.FilterEntries(_allEntries, query);
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.ProgrammaticChange)
        {
            return;
        }

        ApplyFilter(sender.Text);
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (ResultsList.ItemsSource is IList<CommandPaletteEntry> items && items.Count > 0)
        {
            SelectEntry(items[0]);
        }
    }

    private void ResultsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CommandPaletteEntry entry)
        {
            SelectEntry(entry);
        }
    }

    private void SelectEntry(CommandPaletteEntry entry)
    {
        if (!CommandPaletteHelper.IsValidSelection(entry.Selection))
        {
            return;
        }

        ItemSelected?.Invoke(this, entry.Selection);
        Close();
    }

    private void Overlay_PointerPressed(object sender, PointerRoutedEventArgs e) => Close();

    private void Panel_PointerPressed(object sender, PointerRoutedEventArgs e) => e.Handled = true;

    private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Enter &&
            ResultsList.ItemsSource is IList<CommandPaletteEntry> items &&
            items.Count > 0)
        {
            SelectEntry(items[0]);
            e.Handled = true;
        }
    }
}
