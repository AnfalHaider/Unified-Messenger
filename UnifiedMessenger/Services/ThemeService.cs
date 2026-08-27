using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using UnifiedMessenger.Models;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace UnifiedMessenger.Services;

public static class ThemeService
{
    private static UISettings? _uiSettings;

    private static AccessibilitySettings? _accessibilitySettings;

    private static ResourceDictionary? _highContrastDictionary;

    private static readonly Uri HighContrastDictionaryUri =
        new("ms-appx:///Themes/HighContrast.xaml");

    /// <summary>
    /// Sets <see cref="Application.RequestedTheme"/> once at startup (before the main window).
    /// System leaves the OS default unchanged.
    /// </summary>
    public static void ApplyInitialLaunchTheme(AppThemePreference preference)
    {
        if (Application.Current is not Application application)
        {
            return;
        }

        if (ResolveApplicationTheme(preference) is not ApplicationTheme applicationTheme)
        {
            return;
        }

        try
        {
            application.RequestedTheme = applicationTheme;
        }
        catch (COMException ex)
        {
            // Defensive: WinUI rejects Application.RequestedTheme after the first window exists.
            AppLogger.LogInfo("Theme", $"ApplyInitialLaunchTheme skipped: {Describe(ex)}");
        }

        // Reading AccessibilitySettings.HighContrast works; subscribing to its change event does not, in
        // this app configuration or any later one — see EnsureHighContrastWatcher for what a real launch
        // showed. This comment used to attribute that to there being no window yet, which is wrong.
        ApplyInitialHighContrastOverrides();

        // Record the theme for code that runs off the UI thread. Asking WinRT from a background thread is
        // fatal rather than throwable, so anything not on the UI thread reads this instead.
        UmSemanticBrushes.CaptureTheme();
    }

    /// <summary>
    /// Applies the saved preference to the live window without restart (UI-04).
    /// </summary>
    public static void Apply(AppThemePreference preference)
    {
        if (App.CurrentWindow?.Content is not FrameworkElement root)
        {
            return;
        }

        root.RequestedTheme = ResolveElementTheme(preference);
        UmSemanticBrushes.CaptureTheme(root);
        SyncTitleBarTheme(App.CurrentWindow, ResolveEffectiveElementTheme(preference));
        EnsureSystemThemeWatcher();
        EnsureHighContrastWatcher();
    }

    internal static ElementTheme ResolveElementTheme(AppThemePreference preference) =>
        preference switch
        {
            AppThemePreference.Light => ElementTheme.Light,
            AppThemePreference.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

    internal static ApplicationTheme? ResolveApplicationTheme(AppThemePreference preference) =>
        preference switch
        {
            AppThemePreference.Light => ApplicationTheme.Light,
            AppThemePreference.Dark => ApplicationTheme.Dark,
            _ => null
        };

    internal static ElementTheme ResolveEffectiveElementTheme(AppThemePreference preference) =>
        preference switch
        {
            AppThemePreference.Light => ElementTheme.Light,
            AppThemePreference.Dark => ElementTheme.Dark,
            _ => ReadSystemElementTheme()
        };

    internal static bool IsSystemHighContrastEnabled() =>
        new AccessibilitySettings().HighContrast;

    internal static void ApplyHighContrastOverrides(bool enabled)
    {
        if (Application.Current?.Resources is not ResourceDictionary root)
        {
            return;
        }

        if (enabled)
        {
            if (_highContrastDictionary is null)
            {
                _highContrastDictionary = new ResourceDictionary
                {
                    Source = HighContrastDictionaryUri
                };
                root.MergedDictionaries.Add(_highContrastDictionary);
            }

            return;
        }

        if (_highContrastDictionary is not null)
        {
            root.MergedDictionaries.Remove(_highContrastDictionary);
            _highContrastDictionary = null;
        }
    }

    private static void ApplyInitialHighContrastOverrides()
    {
        try
        {
            _accessibilitySettings ??= new AccessibilitySettings();
            ApplyHighContrastOverrides(_accessibilitySettings.HighContrast);
        }
        catch (COMException ex)
        {
            AppLogger.LogInfo("Theme", $"Initial high-contrast overrides skipped: {Describe(ex)}");
        }
    }

    /// <summary>
    /// True once <see cref="AccessibilitySettings.HighContrastChanged"/> has been tried and refused, so the
    /// app is relying on <see cref="UISettings.ColorValuesChanged"/> to notice a high-contrast switch.
    /// </summary>
    private static bool _highContrastEventUnavailable;

    private static void EnsureHighContrastWatcher()
    {
        _accessibilitySettings ??= new AccessibilitySettings();
        ApplyHighContrastOverrides(_accessibilitySettings.HighContrast);

        if (_highContrastEventUnavailable)
        {
            return;
        }

        try
        {
            _accessibilitySettings.HighContrastChanged -= OnHighContrastChanged;
            _accessibilitySettings.HighContrastChanged += OnHighContrastChanged;
        }
        // This used to say "unavailable before the first window is activated", and log at INF as though it
        // were a transient startup condition that would resolve itself. A real launch disproved that: the
        // line appears twice, the second time from Apply() long after the window exists, and both carry
        // HRESULT 0x80070490 (ERROR_NOT_FOUND). Windows.UI.ViewManagement events want a CoreWindow, which
        // an unpackaged WinUI 3 desktop app never has, so this registration fails now and will keep
        // failing — meaning the app could not notice High Contrast being switched on while it was running,
        // and the only sign of that was an INF line saying the opposite of what was happening.
        //
        // UISettings.ColorValuesChanged does reach this process (it is the channel v4.99.46's marshalling
        // fix was written for) and Windows raises it for a high-contrast switch as well as light/dark and
        // accent, so OnSystemColorValuesChanged now carries the re-evaluation. Logged once, at WRN,
        // because a repeated INF line is what hid this in the first place.
        catch (COMException ex)
        {
            _highContrastEventUnavailable = true;
            AppLogger.LogWarning(
                "Theme",
                $"HighContrastChanged is unavailable in this app configuration ({Describe(ex)}); "
                + "watching ColorValuesChanged for high-contrast switches instead.");
        }
    }

    private static void OnHighContrastChanged(AccessibilitySettings sender, object args) =>
        // Windows raises this on a background thread; everything below is UI-thread-only XAML.
        // See OnSystemColorValuesChanged for what that costs when it is not marshalled.
        UiThreadRunner.Post(() =>
        {
            ApplyHighContrastOverrides(sender.HighContrast);

            if (App.CurrentWindow?.Content is FrameworkElement)
            {
                var preference = AppSettingsService.Instance.Settings.ThemePreference;
                SyncTitleBarTheme(App.CurrentWindow, ResolveEffectiveElementTheme(preference));
            }
        });

    /// <summary>
    /// Subscribes the one Windows-settings channel that reaches this process.
    /// </summary>
    /// <remarks>
    /// This used to unsubscribe whenever the owner pinned Light or Dark, on the reasoning that only the
    /// System preference cares what Windows is doing. High contrast is not a preference — it is an
    /// accessibility setting that must win over Light and Dark alike — and with
    /// <see cref="AccessibilitySettings.HighContrastChanged"/> refusing to register at all, unsubscribing
    /// here left the app with no way to notice it whatsoever. The subscription is now unconditional and the
    /// handler decides what to act on.
    /// </remarks>
    private static void EnsureSystemThemeWatcher()
    {
        _uiSettings ??= new UISettings();

        _uiSettings.ColorValuesChanged -= OnSystemColorValuesChanged;
        _uiSettings.ColorValuesChanged += OnSystemColorValuesChanged;
    }

    private static void OnSystemColorValuesChanged(UISettings sender, object args)
    {
        // UISettings.ColorValuesChanged is raised on a background thread, and everything below reads
        // UI-thread-only interfaces — Window.Content, Application.Current.Resources, and
        // AccessibilitySettings.HighContrast. Calling them here threw COMException 0x8001010E
        // (RPC_E_WRONGTHREAD) past App.OnUnhandledException, which leaves Handled=false on purpose, so a
        // routine Windows light/dark or accent switch terminated the app.
        UiThreadRunner.Post(() =>
        {
            // Reading the saved preference is a plain POCO field. Under Light or Dark there is no theme to
            // re-resolve, but a high-contrast switch still has to be honoured, so the overrides are
            // re-applied either way.
            var preference = AppSettingsService.Instance.Settings.ThemePreference;
            if (preference == AppThemePreference.System)
            {
                Apply(AppThemePreference.System);
                return;
            }

            EnsureHighContrastWatcher();
            if (App.CurrentWindow?.Content is FrameworkElement)
            {
                SyncTitleBarTheme(App.CurrentWindow, ResolveEffectiveElementTheme(preference));
            }
        });
    }

    private static ElementTheme ReadSystemElementTheme()
    {
        var background = _uiSettings?.GetColorValue(UIColorType.Background)
            ?? new UISettings().GetColorValue(UIColorType.Background);

        var luminance = background.R + background.G + background.B;
        return luminance > 384 ? ElementTheme.Light : ElementTheme.Dark;
    }

    private static void SyncTitleBarTheme(Window window, ElementTheme theme)
    {
        var titleBar = window.AppWindow.TitleBar;
        var highContrast = _accessibilitySettings?.HighContrast == true;

        if (highContrast)
        {
            titleBar.ButtonForegroundColor = Color.FromArgb(255, 255, 255, 255);
            titleBar.ButtonHoverForegroundColor = Color.FromArgb(255, 255, 255, 255);
            titleBar.ButtonHoverBackgroundColor = Color.FromArgb(40, 255, 255, 255);
            titleBar.ButtonPressedBackgroundColor = Color.FromArgb(60, 255, 255, 255);
            return;
        }

        if (theme == ElementTheme.Dark)
        {
            titleBar.ButtonForegroundColor = Color.FromArgb(255, 255, 255, 255);
            titleBar.ButtonHoverForegroundColor = Color.FromArgb(255, 255, 255, 255);
            titleBar.ButtonHoverBackgroundColor = Color.FromArgb(15, 255, 255, 255);
            titleBar.ButtonPressedBackgroundColor = Color.FromArgb(25, 255, 255, 255);
            return;
        }

        titleBar.ButtonForegroundColor = Color.FromArgb(255, 25, 25, 25);
        titleBar.ButtonHoverForegroundColor = Color.FromArgb(255, 25, 25, 25);
        titleBar.ButtonHoverBackgroundColor = Color.FromArgb(9, 0, 0, 0);
        titleBar.ButtonPressedBackgroundColor = Color.FromArgb(18, 0, 0, 0);
    }

    /// <summary>
    /// Describes a COM failure for the log, including the HRESULT.
    /// </summary>
    /// <remarks>
    /// These three sites interpolated <c>ex.Message</c> alone, and on a real launch every one of them
    /// produced a line reading exactly <c>"… skipped:"</c> with nothing after the colon — a diagnostic whose
    /// entire job is to say why, saying nothing, three times per start. The messages are genuinely empty:
    /// WinRT raises these through an <c>IErrorInfo</c> with no description, so the HRESULT is the only thing
    /// that identifies the failure. It is what tells "the window does not exist yet" (RPC_E_WRONGTHREAD and
    /// friends) apart from "this API needs a CoreWindow, which an unpackaged desktop app never has".
    /// </remarks>
    internal static string Describe(COMException ex)
    {
        var message = ex.Message?.Trim();
        var hresult = $"0x{ex.HResult:X8}";
        return string.IsNullOrEmpty(message) ? hresult : $"{message} ({hresult})";
    }
}
