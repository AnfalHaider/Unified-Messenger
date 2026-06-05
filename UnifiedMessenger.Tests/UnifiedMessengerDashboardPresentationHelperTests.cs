using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class UnifiedMessengerDashboardPresentationHelperTests
{
    [Fact]
    public void FormatIntentLabel_MapsOperationalCategories()
    {
        Assert.Equal("Price inquiry", UnifiedMessengerDashboardPresentationHelper.FormatIntentLabel(UnifiedMessengerIntentCategory.PriceInquiry));
        Assert.Equal("Complaint", UnifiedMessengerDashboardPresentationHelper.FormatIntentLabel(UnifiedMessengerIntentCategory.Complaint));
    }

    [Fact]
    public void ResolveLatencyHex_UsesTrafficLightBands()
    {
        Assert.Equal("#22C55E", UnifiedMessengerDashboardPresentationHelper.ResolveLatencyHex("Green"));
        Assert.Equal("#F59E0B", UnifiedMessengerDashboardPresentationHelper.ResolveLatencyHex("Amber"));
        Assert.Equal("#EF4444", UnifiedMessengerDashboardPresentationHelper.ResolveLatencyHex("Red"));
    }

    [Fact]
    public void FilterThreadsForBranch_ReturnsSortedBranchSubset()
    {
        var threads = new[]
        {
            CreateThread("DHA-2", "A", DateTimeOffset.UtcNow.AddMinutes(-5)),
            CreateThread("F-11", "B", DateTimeOffset.UtcNow.AddMinutes(-1)),
            CreateThread("DHA-2", "C", DateTimeOffset.UtcNow)
        };

        var filtered = UnifiedMessengerDashboardPresentationHelper.FilterThreadsForBranch(threads, "DHA-2");

        Assert.Equal(2, filtered.Count);
        Assert.Equal("C", filtered[0].CustomerName);
        Assert.Equal("A", filtered[1].CustomerName);
    }

    [Fact]
    public void BuildScopeLabel_ReflectsBranchFilter()
    {
        var label = UnifiedMessengerDashboardPresentationHelper.BuildScopeLabel(
            "inst-1",
            ["DHA-2", "F-11"]);

        Assert.Contains("Selected branch filter", label, StringComparison.Ordinal);
    }

    private static ThreadData CreateThread(string branch, string customer, DateTimeOffset time) =>
        new()
        {
            ThreadId = $"inst|{customer}",
            Platform = "whatsapp",
            InstanceId = "inst",
            BranchName = branch,
            CustomerName = customer,
            LastMessageTime = time
        };
}
