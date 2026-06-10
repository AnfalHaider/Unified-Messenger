using System.Threading.Channels;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Adapters;
using UnifiedMessenger.Services.PlatformModules;

namespace UnifiedMessenger.Services.VoiceNotes;

public sealed class VoiceNotePipelineService
{
    private const int MaxQueueCapacity = 16;

    private static readonly Lazy<VoiceNotePipelineService> LazyInstance =
        new(() => new VoiceNotePipelineService());

    private readonly Channel<WhatsAppVoiceNotePayload> _channel = Channel.CreateBounded<WhatsAppVoiceNotePayload>(
        new BoundedChannelOptions(MaxQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    private readonly ILocalSpeechTranscriptionService _transcriptionService;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;

    private VoiceNotePipelineService()
        : this(WhisperCliTranscriptionService.Instance)
    {
    }

    internal VoiceNotePipelineService(ILocalSpeechTranscriptionService transcriptionService)
    {
        ArgumentNullException.ThrowIfNull(transcriptionService);
        _transcriptionService = transcriptionService;
        _worker = Task.Run(ProcessQueueAsync);
    }

    public static VoiceNotePipelineService Instance => LazyInstance.Value;

    public void Enqueue(WhatsAppVoiceNotePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (!AppSettingsService.Instance.Settings.EnableVoiceNoteTranscription ||
            !PlatformModuleRegistry.Instance.IsEnabled(payload.Platform))
        {
            return;
        }

        _ = ChannelWriteHelper.TryWriteWithDropLog(
            _channel.Writer,
            payload,
            "VoiceNotePipeline");
    }

    internal async Task ProcessPayloadForTestsAsync(
        WhatsAppVoiceNotePayload payload,
        CancellationToken cancellationToken = default) =>
        await ProcessPayloadAsync(payload, cancellationToken).ConfigureAwait(false);

    private async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (var payload in _channel.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                try
                {
                    await ProcessPayloadAsync(payload, _cts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Voice note pipeline failed: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    private async Task ProcessPayloadAsync(
        WhatsAppVoiceNotePayload payload,
        CancellationToken cancellationToken)
    {
        var settings = AppSettingsService.Instance.Settings;
        if (payload.DurationSeconds > settings.VoiceNoteMaxDurationSeconds)
        {
            return;
        }

        if (!VoiceNoteStorageHelper.TryWriteCaptureFile(
                payload.InstanceId,
                payload.MimeType,
                payload.AudioBase64,
                out var capturePath))
        {
            return;
        }

        try
        {
            ThreadRegistryService.Instance.MarkVoiceNoteReceived(
                payload.InstanceId,
                payload.ConversationKey,
                payload.DurationSeconds,
                hasUnreadVoiceNote: true);

            SpeechTranscriptionResult? transcription = null;
            if (_transcriptionService.IsReady)
            {
                transcription = await _transcriptionService
                    .TranscribeAsync(capturePath, cancellationToken)
                    .ConfigureAwait(false);
            }

            var messageText = BuildMessageText(payload.DurationSeconds, transcription);
            if (string.IsNullOrWhiteSpace(messageText))
            {
                return;
            }

            var instance = await TryResolveInstanceAsync(payload.InstanceId, cancellationToken)
                .ConfigureAwait(false);
            instance ??= new MessengerInstance
            {
                Id = payload.InstanceId,
                DisplayName = payload.CustomerName,
                Platform = payload.Platform,
                ProfileName = payload.InstanceId
            };

            var selection = WhatsAppIngressHandler.BuildVoiceInboundSelection(
                payload,
                instance,
                messageText,
                transcription?.Confidence ?? 0);

            MessageTriageService.Instance.Enqueue(
                selection,
                instance?.DisplayName ?? payload.CustomerName,
                instance is null ? string.Empty : BranchWorkspaceHelper.ResolveBranchKey(instance),
                skipDedupeCheck: true);

            ThreadRegistryService.Instance.MarkVoiceNoteReceived(
                payload.InstanceId,
                payload.ConversationKey,
                payload.DurationSeconds,
                hasUnreadVoiceNote: transcription is null);

            UnifiedMessengerDashboardService.Instance.NotifyChanged();
        }
        finally
        {
            VoiceNoteStorageHelper.TryDeleteCaptureFile(capturePath);
        }
    }

    private static string BuildMessageText(double durationSeconds, SpeechTranscriptionResult? transcription)
    {
        if (transcription is not null && !string.IsNullOrWhiteSpace(transcription.Text))
        {
            return transcription.Text.Trim();
        }

        var rounded = Math.Max(1, (int)Math.Round(durationSeconds));
        return $"[Voice note {rounded}s — transcription unavailable]";
    }

    private static async Task<MessengerInstance?> TryResolveInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var registry = new InstanceRegistryService();
            await registry.LoadAsync(cancellationToken).ConfigureAwait(false);
            return registry.FindById(instanceId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Voice note instance lookup failed: {ex.Message}");
            return null;
        }
    }

}
