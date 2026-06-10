using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services.VoiceNotes;

public interface ILocalSpeechTranscriptionService
{
    bool IsReady { get; }

    Task<SpeechTranscriptionResult?> TranscribeAsync(
        string audioFilePath,
        CancellationToken cancellationToken = default);
}
