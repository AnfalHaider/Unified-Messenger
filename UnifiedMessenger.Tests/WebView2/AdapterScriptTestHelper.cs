using System.Text.Json;

namespace UnifiedMessenger.Tests.WebView2;

internal static class AdapterScriptTestHelper
{
    public static string ReadScript(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Scripts", fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Adapter script not found: {path}", path);
        }

        return File.ReadAllText(path);
    }

    public static string ReadFixtureHtml(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Fixture HTML not found: {path}", path);
        }

        return File.ReadAllText(path);
    }

    public static string PrepareScript(
        string template,
        string instanceId,
        string platform,
        bool includeMutedBadges = true,
        bool notificationsMuted = false)
    {
        return template
            .Replace("__INSTANCE_ID__", JsonSerializer.Serialize(instanceId), StringComparison.Ordinal)
            .Replace("__PLATFORM__", JsonSerializer.Serialize(platform), StringComparison.Ordinal)
            .Replace("__INCLUDE_MUTED_BADGES__", includeMutedBadges ? "true" : "false", StringComparison.Ordinal)
            .Replace("__NOTIFICATIONS_MUTED__", notificationsMuted ? "true" : "false", StringComparison.Ordinal)
            .Replace("__ENABLE_VOICE_NOTES__", "false", StringComparison.Ordinal);
    }
}
