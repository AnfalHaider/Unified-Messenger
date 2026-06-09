using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed class ToastActivationEventArgs : EventArgs
{
    public required string InstanceId { get; init; }

    public string? AlertId { get; init; }

    public string? Action { get; init; }

    public string? ConversationKey { get; init; }

    public string? CustomerName { get; init; }
}

public sealed class AppNotificationService
{
    private static readonly Lazy<AppNotificationService> LazyInstance = new(() => new AppNotificationService());

    private bool _registered;

    public static AppNotificationService Instance => LazyInstance.Value;

    public event EventHandler<ToastActivationEventArgs>? ActivationRequested;

    public void Initialize()
    {
        if (_registered)
        {
            return;
        }

        try
        {
            AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
            AppNotificationManager.Default.Register();
            _registered = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"App notification registration failed: {ex.Message}");
        }
    }

    public void Shutdown()
    {
        if (!_registered)
        {
            return;
        }

        AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
        AppNotificationManager.Default.Unregister();
        _registered = false;
    }

    public void ShowAlertToast(NotificationAlert alert, MessengerInstance? instance = null)
    {
        ArgumentNullException.ThrowIfNull(alert);

        if (string.IsNullOrWhiteSpace(alert.InstanceId) ||
            NotificationHub.Instance.IsInstanceMuted(alert.InstanceId))
        {
            return;
        }

        try
        {
            var settings = AppSettingsService.Instance.Settings;
            var builder = new AppNotificationBuilder()
                .AddArgument("action", "openAlert")
                .AddArgument("alertId", alert.Id)
                .AddArgument("instanceId", alert.InstanceId)
                .SetTag(ResolveToastTag(settings, alert));

            if (!string.IsNullOrWhiteSpace(alert.ConversationKey))
            {
                builder.AddArgument("conversationKey", alert.ConversationKey);
            }

            if (!string.IsNullOrWhiteSpace(alert.CustomerName))
            {
                builder.AddArgument("customerName", alert.CustomerName);
            }

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

            ApplyToastSound(builder, settings);

            var iconUri = ApplicationPaths.TryResolveAppIconUri();
            if (!string.IsNullOrWhiteSpace(iconUri))
            {
                builder.SetAppLogoOverride(new Uri(iconUri));
            }

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
                TryRaiseActivation(notificationArgs.Argument);
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Launch notification activation failed: {ex.Message}");
        }

        return false;
    }

    internal static string ResolveToastTag(AppSettings settings, NotificationAlert alert) =>
        settings.ToastGroupByInstance ? alert.InstanceId : alert.Id;

    internal static bool ShouldMuteToast(AppSettings settings) =>
        settings.ToastSound == ToastSoundPreference.Silent;

    internal static void ApplyToastSound(AppNotificationBuilder builder, AppSettings settings)
    {
        if (ShouldMuteToast(settings))
        {
            builder.MuteAudio();
        }
    }

    internal static bool TryParseActivationArguments(
        string? argumentString,
        out ToastActivationEventArgs activation)
    {
        activation = null!;

        if (string.IsNullOrWhiteSpace(argumentString))
        {
            return false;
        }

        var arguments = ParseArguments(argumentString);
        if (!arguments.TryGetValue("instanceId", out var instanceId) ||
            string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        arguments.TryGetValue("alertId", out var alertId);
        arguments.TryGetValue("action", out var action);
        arguments.TryGetValue("conversationKey", out var conversationKey);
        arguments.TryGetValue("customerName", out var customerName);

        activation = new ToastActivationEventArgs
        {
            InstanceId = instanceId,
            AlertId = string.IsNullOrWhiteSpace(alertId) ? null : alertId,
            Action = string.IsNullOrWhiteSpace(action) ? null : action,
            ConversationKey = string.IsNullOrWhiteSpace(conversationKey) ? null : conversationKey,
            CustomerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName
        };

        return true;
    }

    private void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        TryRaiseActivation(args.Argument);
    }

    private void TryRaiseActivation(string? argumentString)
    {
        if (!TryParseActivationArguments(argumentString, out var activation))
        {
            return;
        }

        ActivationRequested?.Invoke(this, activation);
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
