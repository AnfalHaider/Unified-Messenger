using System.Diagnostics;
using System.Text;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services.VoiceNotes;

public sealed class WhisperCliTranscriptionService : ILocalSpeechTranscriptionService
{
    public static WhisperCliTranscriptionService Instance { get; } = new();

    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool IsReady => WhisperRuntimeProbe.IsReady();

    public async Task<SpeechTranscriptionResult?> TranscribeAsync(
        string audioFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!IsReady || string.IsNullOrWhiteSpace(audioFilePath) || !File.Exists(audioFilePath))
        {
            return null;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var settings = AppSettingsService.Instance.Settings;
            var executable = WhisperRuntimeProbe.ResolveExecutablePath(settings);
            var model = WhisperRuntimeProbe.ResolveModelPath(settings);
            var language = string.IsNullOrWhiteSpace(settings.VoiceNoteLanguageHint)
                ? "auto"
                : settings.VoiceNoteLanguageHint.Trim();

            var arguments = new StringBuilder()
                .Append('-').Append('m').Append(' ')
                .Append('"').Append(model).Append('"').Append(' ')
                .Append('-').Append('f').Append(' ')
                .Append('"').Append(audioFilePath).Append('"').Append(' ')
                .Append('-').Append('l').Append(' ')
                .Append(language).Append(' ')
                .Append("-nt ")
                .Append("--no-timestamps")
                .ToString();

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            if (!process.Start())
            {
                return null;
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var stdout = (await stdoutTask.ConfigureAwait(false)).Trim();
            var stderr = (await stderrTask.ConfigureAwait(false)).Trim();

            if (process.ExitCode != 0)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Whisper transcription failed ({process.ExitCode}): {stderr}");
                return null;
            }

            var transcript = ExtractTranscript(stdout, stderr);
            if (string.IsNullOrWhiteSpace(transcript))
            {
                return null;
            }

            return new SpeechTranscriptionResult
            {
                Text = transcript.Trim(),
                Confidence = 0.75,
                Language = language
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Whisper transcription failed: {ex.Message}");
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string ExtractTranscript(string stdout, string stderr)
    {
        if (!string.IsNullOrWhiteSpace(stdout))
        {
            var lines = stdout
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line =>
                    !line.StartsWith("whisper_", StringComparison.OrdinalIgnoreCase) &&
                    !line.StartsWith("system_info", StringComparison.OrdinalIgnoreCase) &&
                    !line.StartsWith("main:", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (lines.Count > 0)
            {
                return string.Join(' ', lines);
            }
        }

        return stderr;
    }
}
