using System.Text.Json.Serialization;

namespace UnifiedMessenger.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WorkspaceCategory
{
    Personal,
    Professional
}
