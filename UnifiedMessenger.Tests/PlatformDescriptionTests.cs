using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Regression tests for F-ORCH-01 (S1).
///
/// The account picker rendered DisplayName only, so the capability disclaimers written into
/// PlatformDefinition.Description ("No oversight metrics", "adapter is planned") had zero read sites and
/// were invisible. Six channels looked equivalent when only some produce oversight data, so adding
/// Messenger or Discord appeared broken rather than out of scope.
///
/// The picker now renders Description. These tests pin the invariants that keep it honest:
/// every selectable channel must say something, and a channel that is not measured must say so.
/// </summary>
public class PlatformDescriptionTests
{
    private static AppSettings Settings() => new();

    [Fact]
    public void EverySelectablePlatformHasADescription()
    {
        // A platform added to the picker without a description would render a blank capability line —
        // exactly the silent gap this finding was about.
        var missing = PlatformModuleSettingsHelper.GetSelectablePlatforms(Settings())
            .Where(p => string.IsNullOrWhiteSpace(p.Description))
            .Select(p => p.Id)
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"Selectable platforms with no Description (the picker would show a blank line): {string.Join(", ", missing)}");
    }

    [Fact]
    public void EveryRegisteredPlatformHasADescription()
    {
        // Hidden-from-picker platforms still resolve for existing accounts and may be surfaced elsewhere,
        // so they are held to the same bar.
        var missing = PlatformDefinition.All
            .Where(p => string.IsNullOrWhiteSpace(p.Description))
            .Select(p => p.Id)
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"Registered platforms with no Description: {string.Join(", ", missing)}");
    }

    [Fact]
    public void UnmeasuredChannelsSayTheyAreUnmeasured()
    {
        // googlebusiness is deliberately excluded: it contributes no CONVERSATION metrics, but it does
        // ship review metrics (GoogleReviewSnapshotService — rating, unanswered, reply rate) on a separate
        // surface. Telling the user it has "no oversight metrics" would be its own wrong claim.
        var unmeasured = PlatformDefinition.All
            .Where(p => !PlatformModuleSettingsHelper.ContributesConversationMetrics(p.Id))
            .Where(p => !string.Equals(p.Id, "googlebusiness", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(unmeasured); // guard: if this ever empties, the test has stopped testing anything

        foreach (var platform in unmeasured)
        {
            Assert.True(
                platform.Description.Contains("No oversight metrics", StringComparison.OrdinalIgnoreCase),
                $"'{platform.Id}' produces no conversation metrics but its description does not say so: "
                + $"\"{platform.Description}\"");
        }
    }

    [Fact]
    public void MeasuredChannelsDoNotClaimToBeUnmeasured()
    {
        // The guard above enforces one direction only: an unmeasured channel must say so. Nothing failed
        // when a channel that HAD started producing metrics carried on claiming it produced none — a
        // customer-visible false statement in the picker, with no test able to see it. The two flags and
        // the sentence must move together, so assert both directions rather than trusting whoever flips a
        // capability to remember the copy.
        var measured = PlatformDefinition.All
            .Where(p => PlatformModuleSettingsHelper.ContributesConversationMetrics(p.Id))
            .ToList();

        Assert.NotEmpty(measured); // guard: if this ever empties, the test has stopped testing anything

        foreach (var platform in measured)
        {
            Assert.False(
                platform.Description.Contains("No oversight metrics", StringComparison.OrdinalIgnoreCase),
                $"'{platform.Id}' produces conversation metrics but its description still says it does not: "
                + $"\"{platform.Description}\"");
        }
    }

    [Fact]
    public void NoDescriptionPromisesFutureWork()
    {
        // "planned", "coming soon" and friends are roadmap language. A paying customer reading the picker
        // is choosing what to use today, not reading a roadmap. This is what made the Google entry claim a
        // shipped feature was still "planned" (F-ORCH-02) for releases without anyone noticing.
        string[] roadmapWords = ["planned", "coming soon", "not yet", "future", "TODO", "placeholder"];

        foreach (var platform in PlatformDefinition.All)
        {
            foreach (var word in roadmapWords)
            {
                Assert.False(
                    platform.Description.Contains(word, StringComparison.OrdinalIgnoreCase),
                    $"'{platform.Id}' description contains roadmap language \"{word}\": \"{platform.Description}\"");
            }
        }
    }

    [Fact]
    public void WhatsAppChannelsAdvertiseOversight()
    {
        // The positive case: the two channels that DO produce full oversight must not be left blank, or
        // the picker would imply they are as unmeasured as the embed-only ones.
        foreach (var id in new[] { "whatsapp", "whatsappbusiness" })
        {
            var platform = PlatformDefinition.FindById(id);

            Assert.NotNull(platform);
            Assert.False(string.IsNullOrWhiteSpace(platform!.Description));
            Assert.DoesNotContain("No oversight metrics", platform.Description, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void GoogleDescriptionReflectsWhatShipsAndDeniesAMessageChannel()
    {
        // Google Business Messages was shut down in July 2024 and the data deleted. The picker must never
        // imply a Google conversation channel, now or later.
        var google = PlatformDefinition.FindById("googlebusiness");

        Assert.NotNull(google);
        Assert.Contains("No message channel", google!.Description, StringComparison.OrdinalIgnoreCase);
    }
}
