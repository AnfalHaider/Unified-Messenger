using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Controls;

public sealed partial class BranchWorkspacePillBar : UserControl
{
    private readonly List<BranchWorkspacePillItem> _items = [];
    private bool _suppressSelectionChanged;
    private string? _selectedBranchKey;

    public BranchWorkspacePillBar()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    public event EventHandler<string?>? SelectionChanged;

    public bool IsInteractionEnabled
    {
        get => IsEnabled;
        set => IsEnabled = value;
    }

    public void SetItems(IReadOnlyList<BranchWorkspacePillItem> items, string? selectedBranchKey)
    {
        _items.Clear();
        _items.AddRange(items);
        _selectedBranchKey = selectedBranchKey;
        RebuildPills();
    }

    public void SelectBranchKey(string? branchKey, bool raiseChanged = false)
    {
        _selectedBranchKey = branchKey;
        _suppressSelectionChanged = !raiseChanged;
        RebuildPills();
        _suppressSelectionChanged = false;
    }

    private void RebuildPills()
    {
        PillHostPanel.Children.Clear();

        foreach (var item in _items)
        {
            var isSelected = string.Equals(_selectedBranchKey, item.BranchKey, StringComparison.OrdinalIgnoreCase) ||
                             (_selectedBranchKey is null && item.BranchKey is null);

            var pill = new ToggleButton
            {
                Content = BuildPillContent(item),
                IsChecked = isSelected,
                CornerRadius = new CornerRadius(16),
                MinHeight = 32,
                Padding = new Thickness(12, 6, 12, 6),
                Tag = item
            };

            ToolTipService.SetToolTip(pill, item.TooltipText);
            AutomationProperties.SetName(pill, item.BranchLabel);
            pill.Checked += OnPillChecked;
            PillHostPanel.Children.Add(pill);
        }
    }

    private static StackPanel BuildPillContent(BranchWorkspacePillItem item)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        panel.Children.Add(new TextBlock
        {
            Text = item.BranchLabel,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        if (!string.IsNullOrWhiteSpace(item.BadgeText))
        {
            panel.Children.Add(new Border
            {
                Padding = new Thickness(6, 2, 6, 2),
                CornerRadius = new CornerRadius(10),
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
                Child = new TextBlock
                {
                    Text = item.BadgeText,
                    FontSize = 10,
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"]
                }
            });
        }

        if (item.HasUrgent)
        {
            panel.Children.Add(new Border
            {
                Padding = new Thickness(5, 1, 5, 1),
                CornerRadius = new CornerRadius(8),
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
                Child = new TextBlock
                {
                    Text = item.UrgentCount.ToString(),
                    FontSize = 9,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255))
                }
            });
        }

        return panel;
    }

    private void OnPillChecked(object sender, RoutedEventArgs e)
    {
        if (_suppressSelectionChanged || sender is not ToggleButton checkedPill || checkedPill.Tag is not BranchWorkspacePillItem item)
        {
            return;
        }

        _selectedBranchKey = item.BranchKey;

        foreach (var child in PillHostPanel.Children.OfType<ToggleButton>())
        {
            if (!ReferenceEquals(child, checkedPill))
            {
                child.IsChecked = false;
            }
        }

        SelectionChanged?.Invoke(this, item.BranchKey);
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_items.Count == 0)
        {
            return;
        }

        var currentIndex = ResolveSelectedIndex();
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        if (e.Key == Windows.System.VirtualKey.Right)
        {
            SelectIndex(Math.Min(currentIndex + 1, _items.Count - 1));
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Left)
        {
            SelectIndex(Math.Max(currentIndex - 1, 0));
            e.Handled = true;
        }
    }

    private int ResolveSelectedIndex()
    {
        for (var index = 0; index < _items.Count; index++)
        {
            var item = _items[index];
            if (string.Equals(_selectedBranchKey, item.BranchKey, StringComparison.OrdinalIgnoreCase) ||
                (_selectedBranchKey is null && item.BranchKey is null))
            {
                return index;
            }
        }

        return -1;
    }

    private void SelectIndex(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            return;
        }

        _selectedBranchKey = _items[index].BranchKey;
        RebuildPills();
        SelectionChanged?.Invoke(this, _selectedBranchKey);
    }
}
