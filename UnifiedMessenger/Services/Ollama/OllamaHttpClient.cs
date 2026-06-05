using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using UnifiedMessenger.Models.Ollama;

namespace UnifiedMessenger.Services.Ollama;

internal sealed class OllamaHttpClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    public OllamaHttpClient(HttpClient? httpClient = null, string? baseUrl = null)
    {
        if (httpClient is null)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl ?? OllamaOptions.DefaultBaseUrl, UriKind.Absolute),
                Timeout = OllamaOptions.RequestTimeout
            };
            _ownsClient = true;
        }
        else
        {
            _httpClient = httpClient;
            if (baseUrl is not null)
            {
                _httpClient.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
            }
        }
    }

    public async Task<bool> TryPingAsync(CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(OllamaOptions.HealthTimeout);

        try
        {
            using var response = await _httpClient
                .GetAsync("api/tags", HttpCompletionOption.ResponseHeadersRead, cts.Token)
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> ListModelNamesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient
            .GetAsync("api/tags", cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content
            .ReadFromJsonAsync<OllamaTagsResponse>(OllamaJson.Options, cancellationToken)
            .ConfigureAwait(false);

        if (payload?.Models is null || payload.Models.Count == 0)
        {
            return [];
        }

        return payload.Models
            .Select(static model => model.DisplayName)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async IAsyncEnumerable<string> StreamGenerateAsync(
        string model,
        string prompt,
        string? systemPrompt,
        string? responseFormat = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestBody = new OllamaGenerateRequest
        {
            Model = model,
            Prompt = prompt,
            System = systemPrompt,
            Stream = true,
            Format = responseFormat,
            Options = string.Equals(responseFormat, "json", StringComparison.OrdinalIgnoreCase)
                ? new OllamaGenerateOptions { Temperature = 0 }
                : null
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/generate")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody, OllamaJson.Options),
                Encoding.UTF8,
                "application/json")
        };

        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException(
                $"Ollama generate failed ({(int)response.StatusCode}): {TrimForError(body)}");
        }

        await using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        await foreach (var chunk in OllamaJsonStreamReader
                           .ReadNdjsonAsync<OllamaGenerateChunk>(stream, cancellationToken)
                           .ConfigureAwait(false))
        {
            if (!string.IsNullOrEmpty(chunk.Error))
            {
                throw new InvalidOperationException($"Ollama generate error: {chunk.Error}");
            }

            if (!string.IsNullOrEmpty(chunk.Response))
            {
                yield return chunk.Response;
            }

            if (chunk.Done)
            {
                yield break;
            }
        }
    }

    public async IAsyncEnumerable<OllamaPullProgress> StreamPullAsync(
        string model,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestBody = new OllamaPullRequest { Model = model, Stream = true };
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/pull")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody, OllamaJson.Options),
                Encoding.UTF8,
                "application/json")
        };

        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException(
                $"Ollama pull failed ({(int)response.StatusCode}): {TrimForError(body)}");
        }

        await using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        await foreach (var chunk in OllamaJsonStreamReader
                           .ReadNdjsonAsync<OllamaPullChunk>(stream, cancellationToken)
                           .ConfigureAwait(false))
        {
            if (!string.IsNullOrEmpty(chunk.Error))
            {
                yield return new OllamaPullProgress
                {
                    Model = model,
                    Status = chunk.Status,
                    Completed = chunk.Completed,
                    Total = chunk.Total,
                    IsComplete = false,
                    Error = chunk.Error
                };
                yield break;
            }

            var isComplete = chunk.Status.Contains("success", StringComparison.OrdinalIgnoreCase);
            yield return new OllamaPullProgress
            {
                Model = model,
                Status = chunk.Status,
                Completed = chunk.Completed,
                Total = chunk.Total,
                IsComplete = isComplete
            };

            if (isComplete)
            {
                yield break;
            }
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }

    private static string TrimForError(string body) =>
        body.Length <= 240 ? body : body[..240] + "...";
}
