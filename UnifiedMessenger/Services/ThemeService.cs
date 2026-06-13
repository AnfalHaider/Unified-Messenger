using System.Diagnostics;
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
            Debug.WriteLine($"ApplyInitialLaunchTheme skipped: {ex.Message}");
        }

        // Apply HC resource overrides only; event subscription requires a live window (HRESULT 0x80070490).
        ApplyInitialHighContrastOverrides();
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
        SyncTitleBarTheme(App.CurrentWindow, ResolveEffectiveElementTheme(preference));
        EnsureSystemThemeWatcher(preference);
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
            Debug.WriteLine($"Initial high-contrast overrides skipped: {ex.Message}");
        }
    }

    private static void EnsureHighContrastWatcher()
    {
        _accessibilitySettings ??= new AccessibilitySettings();
        ApplyHighContrastOverrides(_accessibilitySettings.HighContrast);

        try
        {
            _accessibilitySettings.HighContrastChanged -= OnHighContrastChanged;
            _accessibilitySettings.HighContrastChanged += OnHighContrastChanged;
        }
        catch (COMException ex)
        {
            // AccessibilitySettings events are unavailable before the first window is activated.
            Debug.WriteLine($"HighContrastChanged subscription skipped: {ex.Message}");
        }
    }

    private static void OnHighContrastChanged(AccessibilitySettings sender, object args)
    {
        ApplyHighContrastOverrides(sender.HighContrast);

        if (App.CurrentWindow?.Content is FrameworkElement root)
        {
            var preference = AppSettingsService.Instance.Settings.ThemePreference;
            SyncTitleBarTheme(App.CurrentWindow, ResolveEffectiveElementTheme(preference));
        }
    }

    private static void EnsureSystemThemeWatcher(AppThemePreference preference)
    {
        _uiSettings ??= new UISettings();

        if (preference == AppThemePreference.System)
        {
            _uiSettings.ColorValuesChanged -= OnSystemColorValuesChanged;
            _uiSettings.ColorValuesChanged += OnSystemColorValuesChanged;
            return;
        }

        _uiSettings.ColorValuesChanged -= OnSystemColorValuesChanged;
    }

    private static void OnSystemColorValuesChanged(UISettings sender, object args)
    {
        if (AppSettingsService.Instance.Settings.ThemePreference != AppThemePreference.System)
        {
            return;
        }

        Apply(AppThemePreference.System);
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
}
