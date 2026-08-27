using System.Text;
using OllamaSharp;
using OllamaSharp.Models;
using UnifiedMessenger.Models;
using UnifiedMessenger.Models.Ai;

namespace UnifiedMessenger.Services.Ai;

public sealed class OllamaInferenceClient : IAiInferenceClient, IDisposable
{
    private static readonly Lazy<OllamaInferenceClient> LazyInstance =
        new(() => new OllamaInferenceClient());

    private static readonly string StructuredOutputSchema = """
        {
          "type": "object",
          "properties": {
            "intent": {
              "type": "string",
              "enum": ["Booking", "Complaint", "Price_Inquiry", "Lead", "Inquiry", "Spam"]
            },
            "next_action": { "type": "string" },
            "suggested_action": {
              "type": "string",
              "enum": ["Reply", "Escalate", "Ignore", "Follow_up"]
            }
          },
          "required": ["intent", "next_action", "suggested_action"]
        }
        """;

    private readonly Func<string> _endpointProvider;
    private readonly HttpClient _healthClient;
    private readonly bool _ownsHealthClient;

    internal OllamaInferenceClient(
        Func<string>? endpointProvider = null,
        HttpClient? healthClient = null)
    {
        _endpointProvider = endpointProvider ?? (() => AppSettingsService.Instance.Settings.OllamaEndpoint);
        if (healthClient is null)
        {
            _healthClient = new HttpClient
            {
                Timeout = OllamaOptions.HealthTimeout
            };
            _ownsHealthClient = true;
        }
        else
        {
            _healthClient = healthClient;
        }
    }

    public static OllamaInferenceClient Instance => LazyInstance.Value;

    public async Task<bool> TryPingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = OllamaOptions.NormalizeEndpoint(_endpointProvider());
            using var response = await _healthClient
                .GetAsync($"{endpoint}api/tags", cancellationToken)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            AppLogger.LogWarning("Ollama", $"Ollama health probe failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Whether the named model has actually been pulled onto this machine.
    /// </summary>
    /// <remarks>
    /// The runtime being up and the model being present are different things, and confusing them produces a
    /// dead end: with Ollama running but nothing pulled, generation just returns null and the feature can
    /// only say "that didn't work". Knowing which of the two is missing is what lets the UI point at
    /// Settings → AI instead. Returns true when the list cannot be read, so an unexpected API shape degrades
    /// into the old behaviour rather than blocking a model that is really there.
    /// </remarks>
    public async Task<bool> IsModelInstalledAsync(string modelName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return false;
        }

        try
        {
            var endpoint = OllamaOptions.NormalizeEndpoint(_endpointProvider());
            using var response = await _healthClient
                .GetAsync($"{endpoint}api/tags", cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return true;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = System.Text.Json.JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("models", out var models) ||
                models.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return true;
            }

            // Ollama reports "phi3:mini"; a user may have configured plain "phi3". Match either way round so
            // an omitted ":latest"-style tag is not reported as missing.
            var wanted = modelName.Trim();
            foreach (var model in models.EnumerateArray())
            {
                var name = model.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (name.Equals(wanted, StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith(wanted + ":", StringComparison.OrdinalIgnoreCase) ||
                    wanted.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning("Ollama", $"Ollama model probe failed: {ex.Message}");
            return true;
        }
    }

    public async Task<AiInferenceResult?> GenerateStructuredAsync(
        string transcript,
        string modelName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transcript) || string.IsNullOrWhiteSpace(modelName))
        {
            return null;
        }

        var endpoint = OllamaOptions.NormalizeEndpoint(_endpointProvider());
        using var apiClient = new OllamaApiClient(new Uri(endpoint));

        var request = new GenerateRequest
        {
            Model = modelName.Trim(),
            Prompt = BuildPrompt(transcript),
            System = BuildSystemPrompt(),
            Format = StructuredOutputSchema,
            Stream = false
        };

        var builder = new StringBuilder();
        await foreach (var chunk in apiClient
                           .GenerateAsync(request, cancellationToken)
                           .ConfigureAwait(false))
        {
            if (!string.IsNullOrWhiteSpace(chunk?.Response))
            {
                builder.Append(chunk.Response);
            }
        }

        return AiInferenceResult.TryParse(builder.ToString());
    }

    /// <summary>
    /// Free-form single-shot completion (no JSON schema). Returns the trimmed text, or null on any failure
    /// so callers can fall back gracefully. Used for short oversight insight lines.
    /// </summary>
    public async Task<string?> GenerateTextAsync(
        string prompt,
        string systemPrompt,
        string modelName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt) || string.IsNullOrWhiteSpace(modelName))
        {
            return null;
        }

        try
        {
            var endpoint = OllamaOptions.NormalizeEndpoint(_endpointProvider());
            using var apiClient = new OllamaApiClient(new Uri(endpoint));

            var request = new GenerateRequest
            {
                Model = modelName.Trim(),
                Prompt = prompt,
                System = systemPrompt,
                Stream = false
            };

            var builder = new StringBuilder();
            await foreach (var chunk in apiClient
                               .GenerateAsync(request, cancellationToken)
                               .ConfigureAwait(false))
            {
                if (!string.IsNullOrWhiteSpace(chunk?.Response))
                {
                    builder.Append(chunk.Response);
                }
            }

            var text = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning("Ollama", $"Ollama text generation failed: {ex.Message}");
            return null;
        }
    }

    private static string BuildSystemPrompt() =>
        """
        You classify inbound customer support messages for a salon and wellness business.
        Respond with JSON only. Keep next_action under 120 characters.
        """;

    private static string BuildPrompt(string transcript) =>
        $"""
        Analyze this inbound message thread excerpt and return structured JSON.

        {transcript}
        """;

    public void Dispose()
    {
        if (_ownsHealthClient)
        {
            _healthClient.Dispose();
        }
    }
}
