namespace UnifiedMessenger.Services.VoiceNotes;

internal static class WhisperRuntimeOptions
{
    public const string RuntimeFolderName = "whisper";

    public const string ModelsFolderName = "models";

    public const string DefaultModelFileName = "ggml-base.bin";

    public static readonly IReadOnlyList<string> ExecutableCandidates =
        ["whisper-cli.exe", "main.exe", "whisper.exe"];

    public static string RuntimeRoot =>
        Path.Combine(ApplicationPaths.UserDataRoot, RuntimeFolderName);

    public static string ModelsDirectory =>
        Path.Combine(RuntimeRoot, ModelsFolderName);

    public static string DefaultModelPath =>
        Path.Combine(ModelsDirectory, DefaultModelFileName);

    public static string VoiceCaptureDirectory =>
        Path.Combine(ApplicationPaths.UserDataRoot, "voice-captures");

    public const int MaxAudioBytes = 1_572_864;
}
