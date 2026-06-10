using System.Text;
using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Adapters;
using UnifiedMessenger.Services.VoiceNotes;

namespace UnifiedMessenger.Tests;

[Collection(UnifiedMessengerSerialCollection.Name)]
public class WhatsAppVoiceNotePipelineTests : IDisposable
{
    private readonly AppSettings _originalSettings;
    private readonly IReadOnlyList<ThreadData> _originalThreads;

    public WhatsAppVoiceNotePipelineTests()
    {
        _originalSettings = CloneSettings(AppSettingsService.Instance.Settings);
        _originalThreads = ThreadRegistryService.Instance.GetAllThreads();
        ThreadRegistryService.Instance.RestoreThreads([]);
        MessageTriageService.Instance.ResetForTests([]);
    }

    public void Dispose()
    {
        RestoreSettings(_originalSettings);
        ThreadRegistryService.Instance.RestoreThreads(_originalThreads);
        MessageTriageService.Instance.ResetForTests([]);
    }

    [Fact]
    public void VoiceMonitorScript_ExposesInstallHelper()
    {
        var script = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "UnifiedMessenger",
            "Assets",
            "Scripts",
            "whatsapp-voice-monitor.js"));

        Assert.Contains("__umInstallVoiceNoteMonitor", script, StringComparison.Ordinal);
        Assert.Contains("whatsapp-voice-payload", script, StringComparison.Ordinal);
        Assert.Contains("audio[src^=\"blob:\"]", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParseVoicePayload_ReadsBase64Audio()
    {
        var instance = CreateInstance();
        var audio = Convert.ToBase64String(Encoding.UTF8.GetBytes("fake-ogg-bytes"));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            type = AdapterMessageTypes.WhatsAppVoicePayload,
            conversationKey = "120363@s.whatsapp.net",
            customerName = "Sara",
            durationSeconds = 14,
            mimeType = "audio/ogg",
            audioBase64 = audio
        }));

        var payload = WhatsAppIngressHandler.TryParseVoicePayload(document.RootElement, instance);

        Assert.NotNull(payload);
        Assert.Equal(14, payload!.DurationSeconds);
        Assert.Equal(audio, payload.AudioBase64);
    }

    [Fact]
    public async Task VoicePipeline_EnqueuesTranscribedTriageItem()
    {
        ConfigureVoiceSettings(enabled: true);
        var transcription = new StubTranscriptionService("Need bridal makeup Saturday morning");
        var pipeline = new VoiceNotePipelineService(transcription);
        var instance = CreateInstance();

        await pipeline.ProcessPayloadForTestsAsync(new WhatsAppVoiceNotePayload
        {
            InstanceId = instance.Id,
            Platform = instance.Platform,
            ConversationKey = "120363@s.whatsapp.net",
            CustomerName = "Sara",
            DurationSeconds = 12,
            MimeType = "audio/ogg",
            AudioBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("voice-bytes")),
            TimestampUtc = DateTimeOffset.UtcNow
        });
        await WaitForTriageItemsAsync(instance.Id, 1);

        var item = MessageTriageService.Instance.GetAllItems().Single();
        Assert.Equal(InboundMessageKind.VoiceNote, item.MessageKind);
        Assert.Contains("bridal", item.MessageFullText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(12, item.VoiceDurationSeconds);
        Assert.Equal(0.91, item.TranscriptConfidence);
    }

    [Fact]
    public async Task VoicePipeline_FallsBackWhenTranscriptionUnavailable()
    {
        ConfigureVoiceSettings(enabled: true);
        var pipeline = new VoiceNotePipelineService(new StubTranscriptionService(null));
        var instance = CreateInstance();

        await pipeline.ProcessPayloadForTestsAsync(new WhatsAppVoiceNotePayload
        {
            InstanceId = instance.Id,
            Platform = instance.Platform,
            ConversationKey = "120363@s.whatsapp.net",
            CustomerName = "Sara",
            DurationSeconds = 9,
            MimeType = "audio/ogg",
            AudioBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("voice-bytes")),
            TimestampUtc = DateTimeOffset.UtcNow
        });
        await WaitForTriageItemsAsync(instance.Id, 1);

        var item = MessageTriageService.Instance.GetAllItems().Single();
        Assert.Contains("Voice note 9s", item.MessageFullText, StringComparison.Ordinal);

        var thread = ThreadRegistryService.Instance.GetAllThreads().Single();
        Assert.True(thread.HasUnreadVoiceNote);
        Assert.Equal(nameof(InboundMessageKind.VoiceNote), thread.LastMessageKind);
    }

    [Fact]
    public void AiWhatsAppPrompt_IncludesVoiceHintForVoiceJobs()
    {
        var prompt = AiWhatsAppTriagePromptService.BuildUserPrompt(
            new RichTriageInferenceJob
            {
                TriageItemId = "t1",
                InstanceId = "wa-1",
                InstanceDisplayName = "Depilex DHA-2",
                Platform = "whatsappbusiness",
                MessageText = "Need appointment tomorrow",
                CustomerName = "Sara",
                ConversationHint = "120363@s.whatsapp.net",
                MessageKind = InboundMessageKind.VoiceNote,
                VoiceDurationSeconds = 11,
                TranscriptConfidence = 0.82
            },
            metadata: null,
            branchKey: "DHA-2");

        Assert.Contains("voice note", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("11", prompt, StringComparison.Ordinal);
    }

    private static void ConfigureVoiceSettings(bool enabled)
    {
        AppSettingsService.Instance.Settings.EnableVoiceNoteTranscription = enabled;
        AppSettingsService.Instance.Settings.EnableLocalAi = true;
        AppSettingsService.Instance.Settings.VoiceNoteMaxDurationSeconds = 60;
        RichTriageStoreService.Instance.SetLoadedForTests();
        PlatformModuleSettingsHelper.SetPlatformEnabled(
            AppSettingsService.Instance.Settings,
            "whatsappbusiness",
            isEnabled: true);
    }

    private static MessengerInstance CreateInstance() => new()
    {
        Id = "wa-voice",
        DisplayName = "Depilex DHA-2",
        Platform = "whatsappbusiness",
        ProfileName = "wa-voice",
        Category = WorkspaceCategory.Professional,
        BranchKey = "DHA-2"
    };

    private static AppSettings CloneSettings(AppSettings settings) => new()
    {
        EnableVoiceNoteTranscription = settings.EnableVoiceNoteTranscription,
        EnableLocalAi = settings.EnableLocalAi,
        VoiceNoteMaxDurationSeconds = settings.VoiceNoteMaxDurationSeconds,
        VoiceNoteLanguageHint = settings.VoiceNoteLanguageHint,
        WhisperExecutablePath = settings.WhisperExecutablePath,
        WhisperModelPath = settings.WhisperModelPath
    };

    private static void RestoreSettings(AppSettings settings)
    {
        AppSettingsService.Instance.Settings.EnableVoiceNoteTranscription = settings.EnableVoiceNoteTranscription;
        AppSettingsService.Instance.Settings.EnableLocalAi = settings.EnableLocalAi;
        AppSettingsService.Instance.Settings.VoiceNoteMaxDurationSeconds = settings.VoiceNoteMaxDurationSeconds;
        AppSettingsService.Instance.Settings.VoiceNoteLanguageHint = settings.VoiceNoteLanguageHint;
        AppSettingsService.Instance.Settings.WhisperExecutablePath = settings.WhisperExecutablePath;
        AppSettingsService.Instance.Settings.WhisperModelPath = settings.WhisperModelPath;
    }

    private static async Task WaitForTriageItemsAsync(string instanceId, int expectedCount)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            var count = MessageTriageService.Instance.GetAllItems()
                .Count(item => item.InstanceId.Equals(instanceId, StringComparison.OrdinalIgnoreCase));
            if (count >= expectedCount)
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException($"Expected {expectedCount} triage items for {instanceId}.");
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "UnifiedMessenger.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        return Directory.GetCurrentDirectory();
    }

    private sealed class StubTranscriptionService : ILocalSpeechTranscriptionService
    {
        private readonly string? _text;

        public StubTranscriptionService(string? text) => _text = text;

        public bool IsReady => _text is not null;

        public Task<SpeechTranscriptionResult?> TranscribeAsync(
            string audioFilePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_text is null
                ? null
                : new SpeechTranscriptionResult
                {
                    Text = _text,
                    Confidence = 0.91,
                    Language = "auto"
                });
    }
}
