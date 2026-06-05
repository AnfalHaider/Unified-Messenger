using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnifiedMessenger.Services.Ollama;

internal sealed class OllamaTagsResponse
{
    [JsonPropertyName("models")]
    public List<OllamaModelInfo> Models { get; init; } = [];
}

internal sealed class OllamaModelInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Name) ? Name : Model;
}

internal sealed class OllamaGenerateRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("prompt")]
    public required string Prompt { get; init; }

    [JsonPropertyName("system")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? System { get; init; }

    [JsonPropertyName("stream")]
    public bool Stream { get; init; } = true;

    [JsonPropertyName("format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Format { get; init; }

    [JsonPropertyName("options")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OllamaGenerateOptions? Options { get; init; }
}

internal sealed class OllamaGenerateOptions
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; init; }
}

internal sealed class OllamaGenerateChunk
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("response")]
    public string Response { get; init; } = string.Empty;

    [JsonPropertyName("done")]
    public bool Done { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

internal sealed class OllamaPullRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("stream")]
    public bool Stream { get; init; } = true;
}

internal sealed class OllamaPullChunk
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("digest")]
    public string? Digest { get; init; }

    [JsonPropertyName("total")]
    public long Total { get; init; }

    [JsonPropertyName("completed")]
    public long Completed { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

internal static class OllamaJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal static bool TryDeserialize<T>(ReadOnlySpan<char> line, out T? value)
    {
        value = default;
        if (line.IsEmpty)
        {
            return false;
        }

        try
        {
            value = JsonSerializer.Deserialize<T>(line, Options);
            return value is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
