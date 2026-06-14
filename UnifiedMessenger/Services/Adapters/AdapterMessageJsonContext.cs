using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnifiedMessenger.Services.Adapters;

/// <summary>
/// Source-generated JSON context for hot-path WebMessage envelope parsing.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(WebMessageEnvelope))]
internal partial class AdapterMessageJsonContext : JsonSerializerContext;

internal sealed class WebMessageEnvelope
{
    public string? Type { get; set; }

    public string? InstanceId { get; set; }
}
