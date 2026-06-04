namespace UnifiedMessenger.Services;

public static class AboutPageHelper
{
    public const string DefaultVersionLabel = "Unified Messenger v1.0.0";

    public static string BuildAboutVersionLabel(Version? version) =>
        version is null
            ? DefaultVersionLabel
            : $"Unified Messenger v{SettingsPageHelper.FormatVersion(version)}";

    public static bool ShouldShowBackLink(bool canGoBack) => canGoBack;
}
