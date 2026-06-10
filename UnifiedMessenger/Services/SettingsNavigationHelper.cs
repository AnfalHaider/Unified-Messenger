using UnifiedMessenger.Models;
using UnifiedMessenger.ViewModels;

namespace UnifiedMessenger.Services;

public static class SettingsNavigationHelper
{
    public const string NotificationsSectionKey = "notifications";

    public const string AppearanceSectionKey = "appearance";

    public const string SessionPerformanceSectionKey = "session-performance";

    public const string PlatformModulesSectionKey = "platform-modules";

    public const string ProfessionalMetricsSectionKey = "professional-metrics";

    public const string DataPrivacySectionKey = "data-privacy";

    public const string KeyboardShortcutsSectionKey = "keyboard-shortcuts";

    public const string SystemSectionKey = "system";

    public const string RemovedAccountsSectionKey = "removed-accounts";

    public const string StorageSectionKey = "storage";

    public const string LocalAiSectionKey = "local-ai";

    public const string AboutSectionKey = "about";

    public static IReadOnlyList<SettingsSectionNavItemViewModel> BuildSectionNavItems() =>
    [
        new() { Key = NotificationsSectionKey, Label = "Notifications" },
        new() { Key = AppearanceSectionKey, Label = "Appearance" },
        new() { Key = SessionPerformanceSectionKey, Label = "Session & performance" },
        new() { Key = PlatformModulesSectionKey, Label = "Platform modules" },
        new() { Key = ProfessionalMetricsSectionKey, Label = "Professional metrics" },
        new() { Key = DataPrivacySectionKey, Label = "Data & privacy" },
        new() { Key = KeyboardShortcutsSectionKey, Label = "Keyboard shortcuts" },
        new() { Key = SystemSectionKey, Label = "System" },
        new() { Key = RemovedAccountsSectionKey, Label = "Removed accounts" },
        new() { Key = StorageSectionKey, Label = "Storage" },
        new() { Key = LocalAiSectionKey, Label = "Local AI" },
        new() { Key = AboutSectionKey, Label = "About" }
    ];

    public static string BuildBreadcrumb(string currentPageLabel) =>
        $"Settings › {currentPageLabel}";

    public static bool ShouldShowBackLink(bool canGoBack) => canGoBack;
}

public sealed record SettingsExportSummary(int ActiveCount, int ArchivedCount, string StorePath);

public sealed record SettingsImportSummary(int ActiveCount, int ArchivedCount, string SourcePath);
