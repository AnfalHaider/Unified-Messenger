using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class BranchPulseTests
{
    [Fact]
    public void SelectThreadsForBatch_PrioritizesUrgentUnresolvedThreads()
    {
        var threads = new[]
        {
            CreateThread("low", urgency: 1, latency: 5),
            CreateThread("high", urgency: 5, latency: 90),
            CreateThread("mid", urgency: 3, latency: 30)
        };

        var batch = AiBranchPulsePromptService.SelectThreadsForBatch(threads);

        Assert.Equal("high", batch[0].CustomerName);
        Assert.Equal("mid", batch[1].CustomerName);
        Assert.Equal("low", batch[2].CustomerName);
    }

    [Fact]
    public void ParseResponse_ExtractsThemesAndSummary()
    {
        const string raw = """
            THEMES:
            1. Bridal package quotes pending
            2. Saturday slot holds expiring

            SUMMARY:
            Two high-value bridal threads need same-day quotes. Prioritize F-11 holds before noon.
            """;

        var snapshot = AiBranchPulsePromptService.ParseResponse("F-11", "F-11", 4, raw);

        Assert.Equal(BranchPulseState.Ready, snapshot.State);
        Assert.Equal(2, snapshot.Themes.Count);
        Assert.Contains("Bridal package quotes pending", snapshot.Themes[0], StringComparison.Ordinal);
        Assert.Contains("Prioritize F-11 holds", snapshot.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateForTestsAsync_ReturnsNoThreadsWhenScopeEmpty()
    {
        var original = AppSettingsService.Instance.Settings.EnableBranchPulse;
        var originalAi = AppSettingsService.Instance.Settings.EnableLocalAi;
        try
        {
            AppSettingsService.Instance.Settings.EnableBranchPulse = true;
            AppSettingsService.Instance.Settings.EnableLocalAi = true;

            var snapshot = await BranchPulseService.Instance.GenerateForTestsAsync(
                "DHA-2",
                [],
                (_, _) => Task.FromResult<string?>("unused"));

            Assert.Equal(BranchPulseState.NoThreads, snapshot.State);
            Assert.Equal(0, snapshot.OpenThreadCount);
        }
        finally
        {
            AppSettingsService.Instance.Settings.EnableBranchPulse = original;
            AppSettingsService.Instance.Settings.EnableLocalAi = originalAi;
            BranchPulseService.Instance.Invalidate();
        }
    }

    [Fact]
    public async Task GenerateForTestsAsync_UsesInjectedLlmResponse()
    {
        var originalPulse = AppSettingsService.Instance.Settings.EnableBranchPulse;
        var originalAi = AppSettingsService.Instance.Settings.EnableLocalAi;
        try
        {
            AppSettingsService.Instance.Settings.EnableBranchPulse = true;
            AppSettingsService.Instance.Settings.EnableLocalAi = true;
            BranchPulseService.Instance.Invalidate("F-11");

            var threads = new[] { CreateThread("Aisha", urgency: 5, latency: 40, branch: "F-11") };
            var snapshot = await BranchPulseService.Instance.GenerateForTestsAsync(
                "F-11",
                threads,
                (_, _) => Task.FromResult<string?>(
                    """
                    THEMES:
                    1. Bridal quote follow-up

                    SUMMARY:
                    Reply to Aisha with package options today.
                    """));

            Assert.Equal(BranchPulseState.Ready, snapshot.State);
            Assert.Single(snapshot.Themes);
            Assert.Contains("Aisha", snapshot.Summary, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            AppSettingsService.Instance.Settings.EnableBranchPulse = originalPulse;
            AppSettingsService.Instance.Settings.EnableLocalAi = originalAi;
            BranchPulseService.Instance.Invalidate();
        }
    }

    [Fact]
    public void WhatsAppFocusPreset_IncludesBranchPulsePanel()
    {
        var placements = OccLayoutPresets.CreateWhatsAppFocus();

        Assert.Contains(placements, placement =>
            placement.PanelId == OccLayoutDefaults.BranchPulsePanelId && placement.IsVisible);
    }

    private static ThreadData CreateThread(
        string customerName,
        int urgency,
        double latency,
        string branch = "DHA-2") =>
        new()
        {
            ThreadId = $"thread-{customerName}",
            Platform = "whatsappbusiness",
            InstanceId = "wa-1",
            BranchName = branch,
            CustomerName = customerName,
            UrgencyScore = urgency,
            LatencyMinutes = latency,
            NextActionSummary = "Follow up"
        };
}
