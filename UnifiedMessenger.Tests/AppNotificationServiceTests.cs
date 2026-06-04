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
}
