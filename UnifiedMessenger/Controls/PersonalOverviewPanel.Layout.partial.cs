using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

public sealed partial class PersonalOverviewPanel
{
    private bool _isLayoutEditMode;

    private void PersonalLayoutEditButton_Click(object sender, RoutedEventArgs e) =>
        SetPersonalLayoutEditMode(!_isLayoutEditMode);

    private void SetPersonalLayoutEditMode(bool enabled)
    {
        _isLayoutEditMode = enabled;
        PersonalLayoutEditButton.Content = enabled ? "Done" : "Customize layout";
        PersonalLayoutMoveUpButton.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        PersonalLayoutMoveDownButton.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyPersonalLayoutPreferences()
    {
        var order = PersonalOverviewLayoutService.Resolve(_services.AppSettings.Settings);
        var sectionMap = BuildPersonalSectionMap();
        const int startRow = 2;

        for (var index = 0; index < order.Count; index++)
        {
            if (!sectionMap.TryGetValue(order[index], out var element))
            {
                continue;
            }

            Grid.SetRow(element, startRow + index);
        }
    }

    private Dictionary<string, FrameworkElement> BuildPersonalSectionMap() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Search"] = GlobalSearchBox,
            ["Summary"] = SummaryCardsGrid,
            ["Toolbar"] = PersonalToolbarGrid,
            ["Content"] = ContentGrid
        };

    private async void PersonalLayoutMoveUpButton_Click(object sender, RoutedEventArgs e) =>
        await MoveSelectedPersonalSectionAsync(-1).ConfigureAwait(true);

    private async void PersonalLayoutMoveDownButton_Click(object sender, RoutedEventArgs e) =>
        await MoveSelectedPersonalSectionAsync(1).ConfigureAwait(true);

    private async Task MoveSelectedPersonalSectionAsync(int delta)
    {
        var selected = ResolveFocusedPersonalSectionId();
        if (string.IsNullOrWhiteSpace(selected))
        {
            selected = "Summary";
        }

        await _services.AppSettings.UpdateAsync(settings =>
        {
            var order = PersonalOverviewLayoutService.Resolve(settings).ToList();
            var index = order.FindIndex(section => section.Equals(selected, StringComparison.OrdinalIgnoreCase));
            var targetIndex = index + delta;
            if (index < 0 || targetIndex < 0 || targetIndex >= order.Count)
            {
                return;
            }

            (order[index], order[targetIndex]) = (order[targetIndex], order[index]);
            settings.PersonalOverviewSectionOrder = order;
        }).ConfigureAwait(true);

        ApplyPersonalLayoutPreferences();
    }

    private string? ResolveFocusedPersonalSectionId()
    {
        var focused = FocusManager.GetFocusedElement(XamlRoot) as FrameworkElement;
        while (focused is not null)
        {
            if (focused.Tag is string tag &&
                BuildPersonalSectionMap().ContainsKey(tag))
            {
                return tag;
            }

            focused = focused.Parent as FrameworkElement;
        }

        return null;
    }
}
