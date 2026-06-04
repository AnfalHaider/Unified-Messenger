using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Adapters;

namespace UnifiedMessenger.Tests;

public class AdapterMessageTypesTests
{
    [Theory]
    [InlineData("badge-count", true)]
    [InlineData("adapter-ready", true)]
    [InlineData("meta-inbound-message", false)]
    [InlineData("google-review-alert", false)]
    public void IsStandardType_ClassifiesMessageTypes(string type, bool expected)
    {
        Assert.Equal(expected, AdapterMessageTypes.IsStandardType(type));
    }

    [Theory]
    [InlineData("badge-count", true)]
    [InlineData("google-review-snapshot", true)]
    [InlineData("inbound-message-selected", true)]
    [InlineData("dashboard-scrape-status", true)]
    [InlineData("meta-telemetry-snapshot", true)]
    [InlineData("unknown-type", false)]
    public void IsKnownType_ClassifiesSupportedMessages(string type, bool expected)
    {
        Assert.Equal(expected, AdapterMessageTypes.IsKnownType(type));
    }
}

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

    [Fact]
    public void Parse_RejectsEmptyPayload()
    {
        Assert.Throws<JsonException>(() => WebMessageParser.Parse(string.Empty));
    }

    [Fact]
    public void MatchesInstance_RejectsMismatchedInstanceId()
    {
        using var document = WebMessageParser.Parse("""{"type":"badge-count","instanceId":"other","count":1}""");
        var instance = new MessengerInstance { Id = "inst-1", DisplayName = "Work", ProfileName = "work" };

        Assert.False(WebMessageParser.MatchesInstance(document.RootElement, instance));
    }

    [Fact]
    public void ReadNonNegativeInt_ClampsNegativeValues()
    {
        using var document = WebMessageParser.Parse("""{"count":-5}""");

        Assert.Equal(0, WebMessageParser.ReadNonNegativeInt(document.RootElement, "count"));
    }

    [Fact]
    public void ReadOptionalDouble_ParsesNumericAndStringValues()
    {
        using var numeric = WebMessageParser.Parse("""{"averageResponseMinutes":12.5}""");
        using var text = WebMessageParser.Parse("""{"averageResponseMinutes":"18"}""");

        Assert.Equal(12.5, WebMessageParser.ReadOptionalDouble(numeric.RootElement, "averageResponseMinutes"));
        Assert.Equal(18, WebMessageParser.ReadOptionalDouble(text.RootElement, "averageResponseMinutes"));
    }
}
