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
}
