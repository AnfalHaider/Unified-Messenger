using CommunityToolkit.Mvvm.ComponentModel;

namespace UnifiedMessenger.ViewModels;

public partial class PlatformModuleToggleRowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isEnabled;

    public string PlatformId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public bool CanDisable { get; init; } = true;

    public string CapabilitySummary { get; init; } = string.Empty;
}
