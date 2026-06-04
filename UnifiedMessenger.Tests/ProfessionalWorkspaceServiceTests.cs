using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class ProfessionalWorkspaceServiceTests
{
    [Fact]
    public void HandleGoogleReviewSnapshot_SkipsDuplicateCounts()
    {
        var service = ProfessionalWorkspaceService.CreateForTests();
        var changes = 0;
        service.Changed += (_, _) => changes++;

        service.HandleGoogleReviewSnapshot("gbp-1", "Store", 3);
        service.HandleGoogleReviewSnapshot("gbp-1", "Store", 3);

        Assert.Equal(1, changes);
    }

    [Fact]
    public void HandleGoogleReviewAlert_SkipsDuplicateReviewPayload()
    {
        var service = ProfessionalWorkspaceService.CreateForTests();
        var changes = 0;
        service.Changed += (_, _) => changes++;
        var detectedAt = DateTimeOffset.UtcNow;

        service.HandleGoogleReviewAlert(
            "gbp-1",
            "Store",
            "rev-1",
            "Alex",
            "Great service",
            "Downtown",
            5,
            detectedAt);

        service.HandleGoogleReviewAlert(
            "gbp-1",
            "Store",
            "rev-1",
            "Alex",
            "Great service",
            "Downtown",
            5,
            detectedAt);

        Assert.Equal(1, changes);
    }

    [Fact]
    public void HandleMetaInboundMessage_SkipsUnchangedState()
    {
        var service = ProfessionalWorkspaceService.CreateForTests();
        var changes = 0;
        service.Changed += (_, _) => changes++;
        var timestamp = DateTimeOffset.UtcNow;

        service.HandleMetaInboundMessage("meta-1", timestamp, 2);
        service.HandleMetaInboundMessage("meta-1", timestamp, 2);

        Assert.Equal(1, changes);
    }

    [Fact]
    public void RemoveInstance_ClearsGoogleAndMetaState()
    {
        var service = ProfessionalWorkspaceService.CreateForTests();
        service.HandleGoogleReviewSnapshot("gbp-1", "Store", 2);
        service.HandleGoogleReviewAlert(
            "gbp-1",
            "Store",
            "rev-1",
            "Alex",
            "Nice",
            "Store",
            5,
            DateTimeOffset.UtcNow);
        service.HandleMetaInboundMessage("meta-1", DateTimeOffset.UtcNow, 1);

        service.RemoveInstance("gbp-1");
        service.RemoveInstance("meta-1");

        var trust = service.CaptureCustomerTrust(
        [
            new MessengerInstance { Id = "gbp-1", DisplayName = "Store", ProfileName = "gbp", Platform = "googlebusiness" }
        ]);
        var meta = service.CaptureMetaResponseEfficiency(
        [
            new MessengerInstance { Id = "meta-1", DisplayName = "Meta", ProfileName = "meta", Platform = "metabusiness" }
        ]);

        Assert.Equal(0, trust.TotalUnrepliedReviews);
        Assert.Empty(trust.PendingReviews);
        Assert.Equal(0, meta.ActiveUnreadCount);
    }

    [Theory]
    [InlineData(40, 10, 10, "Excellent")]
    [InlineData(80, 10, 10, "Good")]
    [InlineData(140, 10, 10, "Fair")]
    [InlineData(200, 10, 10, "Needs attention")]
    public void ClassifyEfficiency_UsesSlaThreshold(
        double totalMinutes,
        int slaThresholdMinutes,
        int sampleCount,
        string expected)
    {
        Assert.Equal(
            expected,
            ProfessionalWorkspaceService.ClassifyEfficiency(totalMinutes, sampleCount, slaThresholdMinutes));
    }

    [Fact]
    public void CaptureCustomerTrust_PrefersSnapshotCountsOverPendingFallback()
    {
        var service = ProfessionalWorkspaceService.CreateForTests();
        service.HandleGoogleReviewSnapshot("gbp-1", "Store", 4);
        service.HandleGoogleReviewAlert(
            "gbp-1",
            "Store",
            "rev-1",
            "Alex",
            "Nice",
            "Store",
            5,
            DateTimeOffset.UtcNow);

        var trust = service.CaptureCustomerTrust(
        [
            new MessengerInstance { Id = "gbp-1", DisplayName = "Store", ProfileName = "gbp", Platform = "googlebusiness" }
        ]);

        Assert.Equal(4, trust.TotalUnrepliedReviews);
        Assert.Single(trust.PendingReviews);
    }
}
