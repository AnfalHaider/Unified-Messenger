using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class OperationsThreadCardPresentationHelperTests
{
    [Fact]
    public void BuildMessagePreview_UsesLastMessagePreview()
    {
        var thread = CreateThread(preview: "Need bridal quote for Saturday");

        Assert.Equal("Need bridal quote for Saturday", OperationsThreadCardPresentationHelper.BuildMessagePreview(thread));
    }

    [Fact]
    public void BuildMessagePreview_UsesPlaceholderForInquiryWithoutPreview()
    {
        var thread = CreateThread();

        Assert.Equal(
            OperationsThreadCardPresentationHelper.NoMessageCapturedPlaceholder,
            OperationsThreadCardPresentationHelper.BuildMessagePreview(thread));
    }

    [Fact]
    public void BuildMessagePreview_DoesNotReturnEmDash()
    {
        var thread = CreateThread();

        Assert.DoesNotContain("—", OperationsThreadCardPresentationHelper.BuildMessagePreview(thread));
    }

    [Fact]
    public void BuildOpsHint_UsesNextActionSummary()
    {
        var thread = CreateThread(nextActionSummary: "Reply with availability");

        Assert.Equal("Reply with availability", OperationsThreadCardPresentationHelper.BuildOpsHint(thread));
    }

    [Fact]
    public void BuildFallbackSummary_UsesMessagePreviewWhenSummaryMissing()
    {
        var thread = CreateThread(nextActionSummary: string.Empty, preview: "Need bridal quote for Saturday");

        Assert.Equal("Need bridal quote for Saturday", OperationsThreadCardPresentationHelper.BuildFallbackSummary(thread));
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
