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
