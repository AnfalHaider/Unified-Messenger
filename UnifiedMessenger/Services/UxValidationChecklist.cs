namespace UnifiedMessenger.Services;

/// <summary>
/// Manual UX validation surfaces and keyboard paths for Wave 11.
/// </summary>
public static class UxValidationChecklist
{
    public static IReadOnlyList<string> XamlSurfaces { get; } =
    [
        "MainWindow.xaml",
        "Pages/DashboardPage.xaml",
        "Controls/OperationsCommandCenter.xaml",
        "Controls/PersonalOverviewPanel.xaml",
        "Controls/WorkspaceSidebar.xaml",
        "Controls/NotificationFeedPanel.xaml",
        "Controls/CommandPalette.xaml",
        "Controls/BranchWorkspacePillBar.xaml",
        "Controls/KanbanColumnBoard.xaml",
        "Controls/WeeklyActivityChart.xaml",
        "Controls/SentimentActivityChart.xaml",
        "Controls/Shared/OperationsThreadCardView.xaml",
        "Pages/SettingsPage.xaml",
        "Pages/LocalAISettingsPage.xaml",
        "Pages/AboutPage.xaml",
        "Dialogs/AddInstanceDialog.xaml",
        "Dialogs/DeleteInstanceDialog.xaml",
        "Dialogs/EditInstanceMetadataDialog.xaml",
        "Dialogs/RenameInstanceDialog.xaml",
        "App.xaml"
    ];

    public static IReadOnlyList<string> KeyboardPaths { get; } =
    [
        "Dashboard tabs → Operations Command Center",
        "Branch workspace pill → thread card open",
        "Thread card / notification alert → instance focus with conversation key",
        "Settings section nav → section content",
        "Personal search → account navigation"
    ];
}
