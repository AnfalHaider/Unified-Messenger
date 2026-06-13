using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class OperationsThreadCardPresentationHelperTests
{
    [Fact]
    public void BuildFallbackSummary_UsesMessagePreviewWhenSummaryMissing()
    {
        var thread = CreateThread(nextActionSummary: string.Empty, preview: "Need bridal quote for Saturday");

        Assert.Equal("Need bridal quote for Saturday", OperationsThreadCardPresentationHelper.BuildFallbackSummary(thread));
    }

    [Fact]
    public void BuildFallbackSummary_UsesSuggestedActionBeforePreview()
    {
        var thread = CreateThread(
            nextActionSummary: string.Empty,
            preview: "Need quote",
            suggestedAction: "Reply");

        Assert.Equal("Suggested: Reply", OperationsThreadCardPresentationHelper.BuildFallbackSummary(thread));
    }

    [Fact]
    public void BuildFallbackSummary_DoesNotUseAwaitingAiCopy()
    {
        var thread = CreateThread();

        Assert.DoesNotContain("Awaiting AI", OperationsThreadCardPresentationHelper.BuildFallbackSummary(thread));
    }

    private static ThreadData CreateThread(
        string nextActionSummary = "",
        string preview = "",
        string suggestedAction = "",
        string intent = UnifiedMessengerIntentCategory.Inquiry) =>
        new()
        {
            ThreadId = "inst|chat",
            Platform = "whatsapp",
            InstanceId = "inst",
            NextActionSummary = nextActionSummary,
            LastMessagePreview = preview,
            SuggestedAction = suggestedAction,
            AiIntentCategory = intent
        };
}
