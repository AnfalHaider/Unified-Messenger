using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services.VoiceNotes;

public static class WhisperRuntimeProbe
{
    public static bool IsReady(AppSettings? settings = null)
    {
        settings ??= AppSettingsService.Instance.Settings;
        return File.Exists(ResolveExecutablePath(settings)) &&
               File.Exists(ResolveModelPath(settings));
    }

    public static string ResolveExecutablePath(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!string.IsNullOrWhiteSpace(settings.WhisperExecutablePath) &&
            File.Exists(settings.WhisperExecutablePath))
        {
            return settings.WhisperExecutablePath.Trim();
        }

        foreach (var candidate in WhisperRuntimeOptions.ExecutableCandidates)
        {
            var path = Path.Combine(WhisperRuntimeOptions.RuntimeRoot, candidate);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return Path.Combine(WhisperRuntimeOptions.RuntimeRoot, WhisperRuntimeOptions.ExecutableCandidates[0]);
    }

    public static string ResolveModelPath(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!string.IsNullOrWhiteSpace(settings.WhisperModelPath) &&
            File.Exists(settings.WhisperModelPath))
        {
            return settings.WhisperModelPath.Trim();
        }

        return WhisperRuntimeOptions.DefaultModelPath;
    }

    public static string DescribeStatus(AppSettings? settings = null)
    {
        settings ??= AppSettingsService.Instance.Settings;
        if (!settings.EnableVoiceNoteTranscription)
        {
            return "Disabled in settings";
        }

        if (IsReady(settings))
        {
            return "Ready";
        }

        var executable = ResolveExecutablePath(settings);
        var model = ResolveModelPath(settings);
        if (!File.Exists(executable) && !File.Exists(model))
        {
            return $"Missing whisper executable and model under {WhisperRuntimeOptions.RuntimeRoot}";
        }

        if (!File.Exists(executable))
        {
            return $"Missing whisper executable ({executable})";
        }

        return $"Missing whisper model ({model})";
    }
}
