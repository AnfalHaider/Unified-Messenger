using System.Runtime.InteropServices;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using UnifiedMessenger.Models;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace UnifiedMessenger.Services;

/// <summary>How toasts are reaching the desktop, if at all.</summary>
public enum NotificationDelivery
{
    /// <summary>No toast can be shown. Every notification surface in the app is inert.</summary>
    Unavailable,

    /// <summary>Windows App SDK. Supports click-to-activate.</summary>
    WindowsAppSdk,

    /// <summary>Classic shell notifier via the Start Menu shortcut's AppUserModelID. Displays, but a click does not open the app.</summary>
    ClassicShortcut
}

public sealed class ToastActivationEventArgs : EventArgs
{
    public required string InstanceId { get; init; }

    public string? AlertId { get; init; }

    public string? Action { get; init; }

    public string? ConversationKey { get; init; }

    public string? CustomerName { get; init; }
}

public sealed class AppNotificationService : IAppNotificationService
{
    private static readonly Lazy<AppNotificationService> LazyInstance = new(() => new AppNotificationService());

    /// <summary>
    /// Identity the shell groups this app's toasts and taskbar entry under.
    /// </summary>
    /// <remarks>
    /// Must match the <c>AppUserModelID</c> the installer stamps on the Start Menu shortcut
    /// (<c>installer.iss</c>, <c>[Icons]</c>). An unpackaged app has no identity of its own, so without
    /// both halves of this the classic shell notifier refuses to create a notifier at all.
    /// </remarks>
    internal const string Aumid = "AnfalHaider.UnifiedMessenger";

    private bool _registered;

    private ToastNotifier? _classicNotifier;

    public static AppNotificationService Instance => LazyInstance.Value;

    /// <summary>How toasts are currently reaching the desktop. Rendered in Settings → Notifications.</summary>
    public NotificationDelivery Delivery { get; private set; } = NotificationDelivery.Unavailable;

    public event EventHandler<ToastActivationEventArgs>? ActivationRequested;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SetCurrentProcessExplicitAppUserModelID(
        [MarshalAs(UnmanagedType.LPWStr)] string appId);

    /// <summary>
    /// Claims <see cref="Aumid"/> for this process. Call before the first window is created.
    /// </summary>
    public static void ApplyAppUserModelId()
    {
        try
        {
            SetCurrentProcessExplicitAppUserModelID(Aumid);
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning("Notifications", $"Could not set the app identity: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Picks a working toast channel, preferring the one that supports click-to-activate.
    /// </summary>
    /// <remarks>
    /// <b>Why there are two.</b> <c>AppNotificationManager.Register()</c> throws
    /// <c>COMException: The specified module could not be found</c> on this shipping configuration —
    /// unpackaged (<c>WindowsPackageType=None</c>) plus <c>WindowsAppSDKSelfContained</c>. Registering
    /// activates a COM local server that lives in the WindowsAppRuntime <i>Singleton</i> package, which a
    /// self-contained deployment does not carry; the publish output has
    /// <c>PushNotificationsLongRunningTask.ProxyStub.dll</c> but no server to proxy to. The failure was
    /// caught only as a single <c>[WRN]</c> line, so every toast surface in the product — awaiting-reply
    /// alerts, the unhappy-review toast, background message alerts — had silently never worked, while
    /// Settings went on offering five controls that governed them.
    /// <para>
    /// The classic shell notifier needs no runtime package: it needs an AppUserModelID and a Start Menu
    /// shortcut carrying it, which the installer now stamps. It cannot activate the app on click (that
    /// needs a registered COM activator), so it is the fallback rather than the default — a toast the
    /// owner can read beats no toast at all.
    /// </para>
    /// </remarks>
    public void Initialize()
    {
        if (Delivery != NotificationDelivery.Unavailable)
        {
            return;
        }

        try
        {
            AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
            AppNotificationManager.Default.Register();
            _registered = true;
            Delivery = NotificationDelivery.WindowsAppSdk;
            return;
        }
        catch (Exception ex)
        {
            // Debug.WriteLine is [Conditional("DEBUG")] and vanishes from the shipping build, so this
            // failure used to be completely invisible in Release — no log, no symptom except toasts
            // silently never appearing.
            AppLogger.LogWarning("Notifications", $"Toast registration failed: {ex.GetType().Name}: {ex.Message}");

            try
            {
                AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
            }
            catch
            {
                // Nothing to unhook if Register never got far enough to matter.
            }
        }

        try
        {
            _classicNotifier = ToastNotificationManager.CreateToastNotifier(Aumid);
            Delivery = NotificationDelivery.ClassicShortcut;
            AppLogger.LogInfo("Notifications", "Using the classic shell notifier; toasts will show but will not open the app when clicked.");
        }
        catch (Exception ex)
        {
            Delivery = NotificationDelivery.Unavailable;
            AppLogger.LogWarning(
                "Notifications",
                $"No toast channel is available: {ex.GetType().Name}: {ex.Message}. Desktop notifications are off.");
        }
    }

    /// <summary>One sentence for Settings → Notifications, so a dead channel is visible in the UI.</summary>
    public string DeliveryDescription => Delivery switch
    {
        NotificationDelivery.WindowsAppSdk => "Desktop notifications are working.",
        NotificationDelivery.ClassicShortcut =>
            "Desktop notifications are working. Clicking one will not open the app — reinstall from the Start Menu shortcut to restore that.",
        _ => "Windows is not accepting desktop notifications from this app, so the settings below have no effect. Reinstalling usually fixes it."
    };

    /// <summary>
    /// Sends one built notification down whichever channel is live.
    /// </summary>
    private void Deliver(AppNotification notification)
    {
        switch (Delivery)
        {
            case NotificationDelivery.WindowsAppSdk:
                AppNotificationManager.Default.Show(notification);
                break;

            case NotificationDelivery.ClassicShortcut when _classicNotifier is { } notifier:
                var xml = new XmlDocument();
                xml.LoadXml(notification.Payload);

                // Tag and Group live on the AppNotification object, not inside its XML payload, so the
                // grouping and replace-in-place behaviour the settings offer has to be copied across
                // explicitly or toasts would stack instead of replacing.
                var toast = new ToastNotification(xml) { Tag = notification.Tag, Group = notification.Group };
                notifier.Show(toast);
                break;
        }
    }

    public void Shutdown()
    {
        _classicNotifier = null;
        Delivery = NotificationDelivery.Unavailable;

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

        // The availability guard its sibling always had. Without it this method built a whole toast and
        // handed it to a dead channel, caught the throw, and logged "Alert toast failed" — which reads to
        // the caller exactly like success and gave the app no way to tell "toasts off" from "toasts broken".
        if (Delivery == NotificationDelivery.Unavailable ||
            string.IsNullOrWhiteSpace(alert.InstanceId) ||
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
                builder.AddText(PlatformBrandingHelper.ResolveToastAttribution(instance))
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

            var iconUri = ResolveToastAppLogoUri(settings, instance);
            if (!string.IsNullOrWhiteSpace(iconUri))
            {
                builder.SetAppLogoOverride(new Uri(iconUri));
            }

            Deliver(builder.BuildNotification());
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning("Notifications", $"Alert toast failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public void ShowInfoToast(string title, string body, string? instanceId = null)
    {
        if (Delivery == NotificationDelivery.Unavailable)
        {
            return;
        }

        try
        {
            var builder = new AppNotificationBuilder()
                .AddArgument("action", "openInstance")
                .AddArgument("instanceId", instanceId ?? string.Empty)
                .AddText(title)
                .AddText(body);

            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                builder.SetTag($"info-{instanceId}");
            }

            Deliver(builder.BuildNotification());
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning("Notifications", $"Info toast failed: {ex.GetType().Name}: {ex.Message}");
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
            AppLogger.LogWarning("Notifications", $"Launch activation failed: {ex.GetType().Name}: {ex.Message}");
        }

        return false;
    }

    internal static string ResolveToastTag(AppSettings settings, NotificationAlert alert) =>
        settings.ToastGroupByInstance ? alert.InstanceId : alert.Id;

    internal static string? ResolveToastAppLogoUri(AppSettings settings, MessengerInstance? instance)
    {
        if (settings.ToastUsePlatformBranding && instance is not null)
        {
            var platformIconUri = PlatformBrandingHelper.TryResolvePlatformIconUri(instance.Platform);
            if (!string.IsNullOrWhiteSpace(platformIconUri))
            {
                return platformIconUri;
            }
        }

        return ApplicationPaths.TryResolveAppIconUri();
    }

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
