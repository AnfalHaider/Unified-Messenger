namespace UnifiedMessenger.Models.Ollama;

public sealed class OllamaCatalogModel
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string SizeLabel { get; init; }

    public string Description { get; init; } = string.Empty;
}

public sealed class OllamaCatalogDocument
{
    public List<OllamaCatalogModel> Models { get; init; } = [];
}
