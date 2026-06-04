using Microsoft.UI.Xaml;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class ThemeServiceTests
{
    [Theory]
    [InlineData(AppThemePreference.System, ElementTheme.Default)]
    [InlineData(AppThemePreference.Light, ElementTheme.Light)]
    [InlineData(AppThemePreference.Dark, ElementTheme.Dark)]
    public void ResolveElementTheme_MapsPreference(AppThemePreference preference, ElementTheme expected)
    {
        Assert.Equal(expected, ThemeService.ResolveElementTheme(preference));
    }

    [Theory]
    [InlineData(AppThemePreference.System, null)]
    [InlineData(AppThemePreference.Light, ApplicationTheme.Light)]
    [InlineData(AppThemePreference.Dark, ApplicationTheme.Dark)]
    public void ResolveApplicationTheme_MapsPreference(
        AppThemePreference preference,
        ApplicationTheme? expected)
    {
        Assert.Equal(expected, ThemeService.ResolveApplicationTheme(preference));
    }

    [Theory]
    [InlineData(AppThemePreference.Light, ElementTheme.Light)]
    [InlineData(AppThemePreference.Dark, ElementTheme.Dark)]
    public void ResolveEffectiveElementTheme_UsesExplicitPreference(
        AppThemePreference preference,
        ElementTheme expected)
    {
        Assert.Equal(expected, ThemeService.ResolveEffectiveElementTheme(preference));
    }

    [Fact]
    public void ResolveEffectiveElementTheme_SystemMatchesWindowsSetting()
    {
        var expected = ThemeService.ResolveEffectiveElementTheme(AppThemePreference.System);

        Assert.True(
            expected is ElementTheme.Light or ElementTheme.Dark,
            "System theme should resolve to the current Windows light/dark mode.");
    }
}
