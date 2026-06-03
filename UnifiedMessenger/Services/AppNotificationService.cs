using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Services;

public sealed class AppNotificationService
{
    private static readonly Lazy<AppNotificationService> LazyInstance = new(() => new AppNotificationService());

    public static AppNotificationService Instance => LazyInstance.Value;

    public event EventHandler<string>? InstanceActivationRequested;

    public void Initialize()
    {
        try
        {
            AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
            AppNotificationManager.Default.Register();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"App notification registration failed: {ex.Message}");
        }
    }

    public void Shutdown()
    {
        AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
        AppNotificationManager.Default.Unregister();
    }

    public void ShowAlertToast(NotificationAlert alert, MessengerInstance? instance = null)
    {
        try
        {
            var settings = AppSettingsService.Instance.Settings;
            var builder = new AppNotificationBuilder()
                .AddArgument("action", "openAlert")
                .AddArgument("alertId", alert.Id)
                .AddArgument("instanceId", alert.InstanceId)
                .SetTag(alert.Id);

            if (settings.ToastGroupByInstance)
            {
                builder.SetGroup(alert.InstanceId);
            }

            if (settings.ToastUsePlatformBranding && instance is not null)
            {
                builder.AddText(instance.DisplayName)
                    .AddText(alert.Title)
                    .AddText(string.IsNullOrWhiteSpace(alert.Body) ? "New message" : alert.Body);
            }
            else
            {
                builder.AddText(alert.InstanceDisplayName)
                    .AddText(alert.Title)
                    .AddText(string.IsNullOrWhiteSpace(alert.Body) ? "New message" : alert.Body);
            }

            builder.SetAppLogoOverride(new Uri("ms-appx:///Assets/Square44x44Logo.png"));

            AppNotificationManager.Default.Show(builder.BuildNotification());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Native toast failed: {ex.Message}");
        }
    }

    public bool TryHandleLaunchActivation()
    {
        try
        {
            var activatedArgs = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
            if (activatedArgs.Kind != Microsoft.Windows.AppLifecycle.ExtendedActivationKind.AppNotification)
            {
                return false;
            }

            if (activatedArgs.Data is AppNotificationActivatedEventArgs notificationArgs)
            {
                HandleActivationArguments(notificationArgs.Argument);
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Launch notification activation failed: {ex.Message}");
        }

        return false;
    }

    private void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        HandleActivationArguments(args.Argument);
    }

    private void HandleActivationArguments(string? argumentString)
    {
        if (string.IsNullOrWhiteSpace(argumentString))
        {
            return;
        }

        var arguments = ParseArguments(argumentString);
        if (!arguments.TryGetValue("instanceId", out var instanceId) ||
            string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        InstanceActivationRequested?.Invoke(this, instanceId);
    }

    private static Dictionary<string, string> ParseArguments(string argumentString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var segment in argumentString.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(segment[..separatorIndex]);
            var value = Uri.UnescapeDataString(segment[(separatorIndex + 1)..]);
            result[key] = value;
        }

        return result;
    }
}
