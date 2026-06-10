using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class AppNotificationServiceTests
{
    [Fact]
    public void TryParseActivationArguments_ParsesInstanceAndAlertIds()
    {
        var raw = "action=openAlert&alertId=alert-1&instanceId=inst-whatsapp";

        Assert.True(AppNotificationService.TryParseActivationArguments(raw, out var activation));
        Assert.Equal("inst-whatsapp", activation.InstanceId);
        Assert.Equal("alert-1", activation.AlertId);
        Assert.Equal("openAlert", activation.Action);
    }

    [Fact]
    public void TryParseActivationArguments_DecodesEscapedValues()
    {
        var raw = "instanceId=inst%201&alertId=a%2Fb";

        Assert.True(AppNotificationService.TryParseActivationArguments(raw, out var activation));
        Assert.Equal("inst 1", activation.InstanceId);
        Assert.Equal("a/b", activation.AlertId);
    }

    [Fact]
    public void TryParseActivationArguments_RejectsMissingInstanceId()
    {
        Assert.False(AppNotificationService.TryParseActivationArguments("action=openAlert", out _));
    }

    [Fact]
    public void ResolveToastTag_UsesInstanceIdWhenGroupingEnabled()
    {
        var settings = new AppSettings { ToastGroupByInstance = true };
        var alert = NotificationAlert.Create("inst-1", "Work", "slack", "Ping");

        Assert.Equal("inst-1", AppNotificationService.ResolveToastTag(settings, alert));
    }

    [Fact]
    public void ResolveToastTag_UsesAlertIdWhenGroupingDisabled()
    {
        var settings = new AppSettings { ToastGroupByInstance = false };
        var alert = NotificationAlert.Create("inst-1", "Work", "slack", "Ping");

        Assert.Equal(alert.Id, AppNotificationService.ResolveToastTag(settings, alert));
    }

    [Theory]
    [InlineData(ToastSoundPreference.Silent, true)]
    [InlineData(ToastSoundPreference.Default, false)]
    public void ShouldMuteToast_ReflectsPreference(ToastSoundPreference preference, bool expected)
    {
        Assert.Equal(
            expected,
            AppNotificationService.ShouldMuteToast(new AppSettings { ToastSound = preference }));
    }

    [Fact]
    public void ResolveToastAttribution_IncludesPlatformAndInstanceNames()
    {
        var instance = new MessengerInstance
        {
            Platform = "slack",
            DisplayName = "Work Ops"
        };
        instance.ApplyPlatformBranding();

        var attribution = PlatformBrandingHelper.ResolveToastAttribution(instance);

        Assert.Contains("Slack", attribution, StringComparison.Ordinal);
        Assert.Contains("Work Ops", attribution, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveToastAppLogoUri_PrefersPlatformAssetWhenBrandingEnabled()
    {
        var platformDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Platforms");
        Directory.CreateDirectory(platformDir);
        var platformIconPath = Path.Combine(platformDir, "slack.png");
        File.WriteAllBytes(platformIconPath, [0x89, 0x50, 0x4E, 0x47]);

        try
        {
            var settings = new AppSettings { ToastUsePlatformBranding = true };
            var instance = new MessengerInstance { Platform = "slack", DisplayName = "Work" };

            var uri = AppNotificationService.ResolveToastAppLogoUri(settings, instance);

            Assert.NotNull(uri);
            Assert.Contains("slack.png", uri, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(platformIconPath))
            {
                File.Delete(platformIconPath);
            }
        }
    }

    [Fact]
    public void ResolveToastAppLogoUri_FallsBackToAppIconWhenBrandingDisabled()
    {
        var settings = new AppSettings { ToastUsePlatformBranding = false };
        var instance = new MessengerInstance { Platform = "slack", DisplayName = "Work" };

        var uri = AppNotificationService.ResolveToastAppLogoUri(settings, instance);

        if (ApplicationPaths.TryResolveAppIconUri() is null)
        {
            Assert.Null(uri);
            return;
        }

        Assert.NotNull(uri);
        Assert.Contains("AppIcon.ico", uri, StringComparison.OrdinalIgnoreCase);
    }
}
