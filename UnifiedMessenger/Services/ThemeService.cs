using Microsoft.UI.Xaml;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class ThemeService
{
    public static void Apply(AppThemePreference preference)
    {
        var theme = preference switch
        {
            AppThemePreference.Light => ElementTheme.Light,
            AppThemePreference.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        if (Application.Current is App && App.CurrentWindow?.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme;
        }
    }
}
