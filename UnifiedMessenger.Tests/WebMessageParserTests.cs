using System.Text.Json;
using UnifiedMessenger.Services.Adapters;

namespace UnifiedMessenger.Tests;

public class WebMessageParserTests
{
    [Fact]
    public void Parse_AcceptsDoubleEncodedJsonString()
    {
        var inner = JsonSerializer.Serialize(new { type = "badge-count", count = 3 });
        var wrapped = JsonSerializer.Serialize(inner);

        using var document = WebMessageParser.Parse(wrapped);
        var root = document.RootElement;

        Assert.Equal("badge-count", root.GetProperty("type").GetString());
        Assert.Equal(3, root.GetProperty("count").GetInt32());
    }

    [Fact]
    public void Parse_AcceptsPlainJsonObject()
    {
        using var document = WebMessageParser.Parse("""{"type":"adapter-ready","adapterId":"slack"}""");
        var root = document.RootElement;

        Assert.Equal("adapter-ready", root.GetProperty("type").GetString());
        Assert.Equal("slack", root.GetProperty("adapterId").GetString());
    }
}
