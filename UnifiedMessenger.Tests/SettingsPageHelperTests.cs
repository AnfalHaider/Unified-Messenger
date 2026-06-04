using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class SettingsPageHelperTests
{
    [Fact]
    public void FormatVersion_ReturnsUnknownForNull()
    {
        Assert.Equal("unknown", SettingsPageHelper.FormatVersion(null));
    }

    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    public void FormatVersion_FormatsVersion(string versionText, string expected)
    {
        Assert.Equal(expected, SettingsPageHelper.FormatVersion(Version.Parse(versionText)));
    }

    [Fact]
    public void BuildUpdateCheckMessage_DescribesUpToDateResult()
    {
        var message = SettingsPageHelper.BuildUpdateCheckMessage(new UpdateCheckResult(
            UpdateCheckStatus.UpToDate,
            new Version(1, 0, 3)));

        Assert.Contains("latest version", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1.0.3", message);
    }

    [Fact]
    public void BuildUpdateCheckMessage_UsesErrorMessageWhenFailed()
    {
        var message = SettingsPageHelper.BuildUpdateCheckMessage(new UpdateCheckResult(
            UpdateCheckStatus.Failed,
            ErrorMessage: "Repository not found"));

        Assert.Equal("Repository not found", message);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(40, 32)]
    [InlineData(4.6, 5)]
    public void NormalizeMaxConcurrentWebViews_ClampsToCap(double rawValue, int expected)
    {
        Assert.Equal(expected, SettingsPageHelper.NormalizeMaxConcurrentWebViews(rawValue));
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(200, 120)]
    [InlineData(15.4, 15)]
    public void NormalizeSlaThresholdMinutes_ClampsToConfiguredRange(double rawValue, int expected)
    {
        Assert.Equal(expected, SettingsPageHelper.NormalizeSlaThresholdMinutes(rawValue));
    }

    [Fact]
    public void BuildArchivedAccountItems_OrdersByDisplayNameAndSkipsBlankIds()
    {
        var items = SettingsPageHelper.BuildArchivedAccountItems([
            new MessengerInstance { Id = "b", DisplayName = "Beta", Platform = "slack" },
            new MessengerInstance { Id = "   ", DisplayName = "Invalid", Platform = "slack" },
            new MessengerInstance { Id = "a", DisplayName = "Alpha", Platform = "whatsapp" }
        ]);

        Assert.Equal(["a", "b"], items.Select(item => item.InstanceId));
        Assert.Equal("WhatsApp", items[0].PlatformLabel);
    }

    [Fact]
    public void ResolveInstancesStorePath_FallsBackToDefaultLocation()
    {
        var path = SettingsPageHelper.ResolveInstancesStorePath(null);

        Assert.EndsWith("instances.json", path, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UnifiedMessenger", path, StringComparison.OrdinalIgnoreCase);
    }
}
