namespace UnifiedMessenger.Services.Shell;

public readonly record struct ShellSelectionState(
    bool IsDashboardSelected,
    bool IsSettingsSelected,
    string? SelectedInstanceId);
