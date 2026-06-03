using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Models;

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

    public void SetEntries(IReadOnlyList<CommandPaletteEntry> entries)
    {
        _allEntries = entries;
        ApplyFilter(SearchBox.Text);
    }

    public void Open()
    {
        Visibility = Visibility.Visible;
        IsHitTestVisible = true;
        SearchBox.Text = string.Empty;
        ApplyFilter(string.Empty);
        SearchBox.Focus(FocusState.Programmatic);
    }

    public void Close()
    {
        Visibility = Visibility.Collapsed;
        IsHitTestVisible = false;
        SearchBox.Text = string.Empty;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyFilter(string? query)
    {
        query = query?.Trim() ?? string.Empty;
        IEnumerable<CommandPaletteEntry> filtered = _allEntries;

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = _allEntries
                .Where(e => e.Matches(query))
                .OrderByDescending(e => e.Score(query))
                .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase);
        }

        ResultsList.ItemsSource = filtered.Take(12).ToList();
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
        ItemSelected?.Invoke(this, entry.Selection);
        Close();
    }

    private void Overlay_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        Close();
    }

    private void Panel_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        e.Handled = true;
    }

    private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}

public sealed class CommandPaletteEntry
{
    public required string Title { get; init; }

    public required string Subtitle { get; init; }

    public required string Category { get; init; }

    public required CommandPaletteSelection Selection { get; init; }

    public bool Matches(string query)
    {
        query = query.Trim();
        return Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               Category.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    public int Score(string query)
    {
        query = query.Trim();
        if (Title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        if (Title.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 50;
        }

        return Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase) ? 25 : 0;
    }
}

public sealed class CommandPaletteSelection
{
    public CommandPaletteAction Action { get; init; }

    public string? InstanceId { get; init; }

    public string? AlertId { get; init; }
}

public enum CommandPaletteAction
{
    OpenInstance,
    OpenDashboard,
    OpenSettings,
    OpenAlert,
    ToggleNotifications,
    ClearNotifications,
    MarkAllRead
}
