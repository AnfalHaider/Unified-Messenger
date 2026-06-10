using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using UnifiedMessenger.Controls.Occ;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
    private void LayoutEditToggleButton_Click(object sender, RoutedEventArgs e) =>
        SetLayoutEditMode(!_viewModel.IsLayoutEditMode);

    private async void RestoreLayoutButton_Click(object sender, RoutedEventArgs e)
    {
        CaptureLayoutUndoSnapshot();
        await _services.AppSettings.UpdateAsync(settings => OccLayoutService.ApplyDefaults(settings))
            .ConfigureAwait(true);
        ApplyLayoutPreferences();
        ShowLayoutUndoInfoBar();
    }

    private async void LayoutPresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressPresetSelection || !_viewModel.IsLayoutEditMode)
        {
            return;
        }

        if (LayoutPresetComboBox.SelectedItem is not ComboBoxItem item ||
            item.Tag is not string presetId)
        {
            return;
        }

        CaptureLayoutUndoSnapshot();
        await _services.AppSettings.UpdateAsync(settings => OccLayoutService.ApplyPreset(settings, presetId))
            .ConfigureAwait(true);
        ApplyLayoutPreferences();
        AnnounceLayoutChange($"Applied {item.Content} layout preset.");
        ShowLayoutUndoInfoBar();
    }

    private void SetLayoutEditMode(bool enabled)
    {
        _viewModel.IsLayoutEditMode = enabled;
        LayoutEditToggleButton.Content = enabled ? "Done" : "Customize layout";
        RestoreLayoutButton.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        LayoutPresetComboBox.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        HiddenPanelsTray.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        KanbanBoard.IsReorderEnabled = enabled;
        ImmediateQueueList.CanDragItems = enabled;
        ImmediateQueueList.CanReorderItems = enabled;
        ImmediateQueueList.IsSwipeEnabled = enabled;
        ImmediateLaneDragGrip.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        ApplyLayoutEditChrome(enabled);

        if (enabled)
        {
            PopulateLayoutPresetComboBox();
        }

        if (enabled && !_services.AppSettings.Settings.OccLayoutTeachingDismissed)
        {
            OccTeachingTipHelper.ShowTeachingTip(
                LayoutEditToggleButton,
                "Customize your dashboard",
                "Drag panels on the grid, resize with +/− or Shift+arrow, hide panels, pick a preset, or press Ctrl+Z to undo. Arrow keys move the focused panel header.");
            _ = _services.AppSettings.UpdateAsync(settings => settings.OccLayoutTeachingDismissed = true);
        }
    }

    private void PopulateLayoutPresetComboBox()
    {
        _suppressPresetSelection = true;
        LayoutPresetComboBox.Items.Clear();
        LayoutPresetComboBox.Items.Add(new ComboBoxItem { Content = "Operations focus", Tag = OccLayoutPresets.OperationsFocus });
        LayoutPresetComboBox.Items.Add(new ComboBoxItem { Content = "WhatsApp focus", Tag = OccLayoutPresets.WhatsAppFocus });
        LayoutPresetComboBox.Items.Add(new ComboBoxItem { Content = "Analytics focus", Tag = OccLayoutPresets.AnalyticsFocus });
        LayoutPresetComboBox.Items.Add(new ComboBoxItem { Content = "Compact", Tag = OccLayoutPresets.Compact });
        LayoutPresetComboBox.Items.Add(new ComboBoxItem { Content = "Front desk", Tag = OccLayoutPresets.FrontDesk });
        LayoutPresetComboBox.Items.Add(new ComboBoxItem { Content = "Manager", Tag = OccLayoutPresets.Manager });
        LayoutPresetComboBox.Items.Add(new ComboBoxItem { Content = "After hours", Tag = OccLayoutPresets.AfterHours });

        var currentPreset = OccLayoutService.Resolve(_services.AppSettings.Settings).LayoutPresetId;
        var selectedIndex = 0;
        for (var index = 0; index < LayoutPresetComboBox.Items.Count; index++)
        {
            if (LayoutPresetComboBox.Items[index] is ComboBoxItem item &&
                item.Tag is string presetId &&
                presetId.Equals(currentPreset, StringComparison.OrdinalIgnoreCase))
            {
                selectedIndex = index;
                break;
            }
        }

        LayoutPresetComboBox.SelectedIndex = selectedIndex;
        _suppressPresetSelection = false;
    }

    private void ApplyLayoutEditChrome(bool enabled)
    {
        foreach (var (panelId, section) in EnumerateLayoutSectionEntries())
        {
            EnsurePanelChrome(section, panelId);
            var surface = ResolveLayoutSectionSurface(panelId, section);
            surface.CanDrag = enabled;
            surface.AllowDrop = enabled;
            if (enabled)
            {
                surface.DragStarting -= OccSection_DragStarting;
                surface.DragStarting += OccSection_DragStarting;
                surface.DragOver -= OccSection_DragOver;
                surface.DragOver += OccSection_DragOver;
                surface.DragLeave -= OccSection_DragLeave;
                surface.DragLeave += OccSection_DragLeave;
                surface.Drop -= OccSection_Drop;
                surface.Drop += OccSection_Drop;
                surface.KeyDown -= OccSection_KeyDown;
                surface.KeyDown += OccSection_KeyDown;
                surface.IsTabStop = true;
            }
            else
            {
                surface.DragStarting -= OccSection_DragStarting;
                surface.DragOver -= OccSection_DragOver;
                surface.DragLeave -= OccSection_DragLeave;
                surface.Drop -= OccSection_Drop;
                surface.KeyDown -= OccSection_KeyDown;
            }
        }

        if (!enabled)
        {
            ClearDropPreviewHighlight();
        }

        foreach (var kpiCard in EnumerateKpiCards())
        {
            kpiCard.CanDrag = enabled;
            if (enabled)
            {
                kpiCard.DragStarting -= KpiCard_DragStarting;
                kpiCard.DragStarting += KpiCard_DragStarting;
            }
            else
            {
                kpiCard.DragStarting -= KpiCard_DragStarting;
            }
        }

        KpiStripSection.AllowDrop = enabled;
        if (enabled)
        {
            KpiStripSection.DragOver += KpiGrid_DragOver;
            KpiStripSection.Drop += KpiGrid_Drop;
        }
        else
        {
            KpiStripSection.DragOver -= KpiGrid_DragOver;
            KpiStripSection.Drop -= KpiGrid_Drop;
        }

        foreach (var chrome in _panelChromes.Values)
        {
            chrome.IsEditMode = enabled;
        }
    }

    private void EnsurePanelChrome(FrameworkElement section, string panelId)
    {
        if (_panelChromes.TryGetValue(panelId, out var existingChrome))
        {
            existingChrome.IsEditMode = _viewModel.IsLayoutEditMode;
            if (section is Expander expander && _viewModel.IsLayoutEditMode)
            {
                expander.IsExpanded = true;
            }

            return;
        }

        if (section.Parent is OccPanelChrome)
        {
            return;
        }

        var chrome = new OccPanelChrome
        {
            PanelId = panelId,
            PanelTitle = ResolvePanelTitle(panelId),
            IsEditMode = _viewModel.IsLayoutEditMode,
            Tag = panelId
        };
        chrome.HideRequested += OnPanelChromeHideRequested;
        chrome.ResizeRequested += OnPanelChromeResizeRequested;
        WrapSectionWithChrome(section, chrome);
        _panelChromes[panelId] = chrome;

        if (section is Expander wrappedExpander && _viewModel.IsLayoutEditMode)
        {
            wrappedExpander.IsExpanded = true;
        }
    }

    private static void WrapSectionWithChrome(FrameworkElement section, OccPanelChrome chrome)
    {
        if (section.Parent is not Panel parent)
        {
            return;
        }

        var index = parent.Children.IndexOf(section);
        if (index < 0)
        {
            return;
        }

        var row = Grid.GetRow(section);
        var column = Grid.GetColumn(section);
        var rowSpan = Grid.GetRowSpan(section);
        var columnSpan = Grid.GetColumnSpan(section);

        chrome.PanelContent = section;
        parent.Children.RemoveAt(index);
        parent.Children.Insert(index, chrome);
        Grid.SetRow(chrome, row);
        Grid.SetColumn(chrome, column);
        Grid.SetRowSpan(chrome, rowSpan);
        Grid.SetColumnSpan(chrome, columnSpan);
    }

    private void OnPanelChromeHideRequested(object? sender, string panelId) =>
        _ = HidePanelAsync(panelId);

    private void OnPanelChromeResizeRequested(object? sender, (string PanelId, int DeltaColumns) e) =>
        _ = ResizePanelAsync(e.PanelId, e.DeltaColumns);

    private void AnnounceLayoutChange(string message) =>
        LayoutLiveRegion.Text = message;
}
