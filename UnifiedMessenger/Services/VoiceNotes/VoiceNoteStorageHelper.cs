namespace UnifiedMessenger.Services.VoiceNotes;

internal static class VoiceNoteStorageHelper
{
    public const int MaxBase64Length = 2_800_000;

    public static bool TryWriteCaptureFile(
        string instanceId,
        string mimeType,
        string audioBase64,
        out string filePath)
    {
        filePath = string.Empty;
        if (string.IsNullOrWhiteSpace(audioBase64) || audioBase64.Length > MaxBase64Length)
        {
            return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(audioBase64);
            if (bytes.Length == 0 || bytes.Length > WhisperRuntimeOptions.MaxAudioBytes)
            {
                return false;
            }

            var directory = Path.Combine(
                WhisperRuntimeOptions.VoiceCaptureDirectory,
                SanitizePathSegment(instanceId));
            Directory.CreateDirectory(directory);

            var extension = ResolveExtension(mimeType);
            filePath = Path.Combine(directory, $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.{extension}");
            File.WriteAllBytes(filePath, bytes);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Voice capture write failed: {ex.Message}");
            filePath = string.Empty;
            return false;
        }
    }

    public static void TryDeleteCaptureFile(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Voice capture delete failed: {ex.Message}");
        }
    }

    private static string ResolveExtension(string mimeType) =>
        mimeType.Trim().ToLowerInvariant() switch
        {
            "audio/mpeg" or "audio/mp3" => "mp3",
            "audio/mp4" or "audio/m4a" => "m4a",
            "audio/wav" or "audio/x-wav" => "wav",
            _ => "ogg"
        };

    private static string SanitizePathSegment(string value)
    {
        var trimmed = value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            trimmed = trimmed.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(trimmed) ? "instance" : trimmed;
    }
}
