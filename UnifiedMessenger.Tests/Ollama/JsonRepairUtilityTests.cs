using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Ollama;

namespace UnifiedMessenger.Tests.Ollama;

public class JsonRepairUtilityTests
{
    [Fact]
    public void TryDeserialize_PlainJson_Succeeds()
    {
        const string raw = """
            {"UrgencyScore":72,"Sentiment":"Negative","CustomerIntent":"Complaint","ExtractedEntities":{"CustomerName":"Sara","ContactNumber":null,"RequestedDate":"Friday","RequestedTime":null,"ServiceType":"color","ActionRequired":"call back"},"CoreSummary":"Cancel booking urgently"}
            """;

        Assert.True(JsonRepairUtility.TryDeserialize<RichTriageLlmResponse>(raw, out var parsed));
        Assert.Equal(72, parsed!.LegacyUrgencyScore);
        Assert.Equal("Complaint", parsed.CustomerIntent);
        Assert.Equal("Sara", parsed.ExtractedEntities.CustomerName);
    }

    [Fact]
    public void TryDeserialize_MarkdownFence_StripsWrapper()
    {
        const string raw = """
            Sure! Here is the triage JSON:
            ```json
            {"UrgencyScore":40,"Sentiment":"Neutral","CustomerIntent":"Inquiry","ExtractedEntities":{"CustomerName":null,"ContactNumber":null,"RequestedDate":null,"RequestedTime":null,"ServiceType":null,"ActionRequired":null},"CoreSummary":"Asked about pricing"}
            ```
            """;

        Assert.True(JsonRepairUtility.TryDeserialize<RichTriageLlmResponse>(raw, out var parsed));
        Assert.Equal(40, parsed!.LegacyUrgencyScore);
        Assert.Equal("Inquiry", parsed.CustomerIntent);
    }

    [Fact]
    public void TryDeserialize_ConversationalPreamble_IsolatesObject()
    {
        const string raw = """
            Based on the message, the customer seems upset. JSON output:
            {"UrgencyScore":88,"Sentiment":"Negative","CustomerIntent":"Complaint","ExtractedEntities":{"CustomerName":"Ali","ContactNumber":"+92 300 1234567","RequestedDate":null,"RequestedTime":null,"ServiceType":"bridal","ActionRequired":"refund"},"CoreSummary":"Demands immediate refund"}
            Hope this helps.
            """;

        Assert.True(JsonRepairUtility.TryDeserialize<RichTriageLlmResponse>(raw, out var parsed));
        Assert.Equal(88, parsed!.LegacyUrgencyScore);
        Assert.Equal("+92 300 1234567", parsed.ExtractedEntities.ContactNumber);
    }

    [Fact]
    public void TryDeserialize_TruncatedObject_ClosesBracketsAndString()
    {
        const string raw = """
            {"UrgencyScore":65,"Sentiment":"Positive","CustomerIntent":"Booking","ExtractedEntities":{"CustomerName":"Noor","ContactNumber":null,"RequestedDate":"Saturday","RequestedTime":"3pm","ServiceType":"facial","ActionRequired":"confirm slot"},"CoreSummary":"Wants Saturday facial at
            """;

        Assert.True(JsonRepairUtility.TryDeserialize<RichTriageLlmResponse>(raw, out var parsed));
        Assert.Equal(65, parsed!.LegacyUrgencyScore);
        Assert.Equal("Booking", parsed.CustomerIntent);
        Assert.Equal("Saturday", parsed.ExtractedEntities.RequestedDate);
    }

    [Fact]
    public void TryDeserialize_TrailingCommaBeforeClose_Repairs()
    {
        const string raw = """
            {"UrgencyScore":15,"Sentiment":"Neutral","CustomerIntent":"Spam","ExtractedEntities":{"CustomerName":null,"ContactNumber":null,"RequestedDate":null,"RequestedTime":null,"ServiceType":null,"ActionRequired":null},"CoreSummary":"Promotional blast",
            """;

        Assert.True(JsonRepairUtility.TryDeserialize<RichTriageLlmResponse>(raw, out var parsed));
        Assert.Equal(CustomerIntent.Spam, MessageTriageInferenceRunner.ParseCustomerIntent(parsed!.CustomerIntent));
    }

    [Fact]
    public void TryDeserialize_GarbageOnly_ReturnsFalse()
    {
        Assert.False(JsonRepairUtility.TryDeserialize<RichTriageLlmResponse>("not json at all", out _));
    }

    [Fact]
    public void TryExtractJsonObject_ReturnsBalancedSlice()
    {
        const string raw = """prefix {"a":1,"b":[2,3]} suffix""";

        Assert.True(JsonRepairUtility.TryExtractJsonObject(raw, out var json));
        Assert.Equal("""{"a":1,"b":[2,3]}""", json);
    }
}
