using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.ViewModels;

public sealed class PersonalOverviewSearchSuggestionViewModel
{
    public required string Label { get; init; }

    public required string SubLabel { get; init; }

    public string? InstanceId { get; init; }

    public required string AccentColorHex { get; init; }

    public SolidColorBrush AccentBrush => PlatformBrandingHelper.GetAccentBrush(AccentColorHex);

    public override string ToString() => $"{Label} ({SubLabel})";
}
