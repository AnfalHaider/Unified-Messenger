using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

public class AppSettingsServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _storePath;

    public AppSettingsServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "UnifiedMessengerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _storePath = Path.Combine(_tempDirectory, "settings.json");
    }

    [Fact]
    public async Task LoadAsync_CreatesDefaultSettingsWhenMissing()
    {
        var service = new AppSettingsService(_storePath);

        await service.LoadAsync();

        Assert.True(File.Exists(_storePath));
        Assert.Equal(AppThemePreference.System, service.Settings.ThemePreference);
        Assert.Equal(15, service.Settings.SlaThresholdMinutes);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChangesWithStringEnums()
    {
        var service = new AppSettingsService(_storePath);
        await service.LoadAsync();

        await service.UpdateAsync(settings =>
        {
            settings.ThemePreference = AppThemePreference.Dark;
            settings.PanelDock = NotificationPanelDock.Bottom;
            settings.SlaThresholdMinutes = 30;
        });

        var reloaded = new AppSettingsService(_storePath);
        await reloaded.LoadAsync();

        Assert.Equal(AppThemePreference.Dark, reloaded.Settings.ThemePreference);
        Assert.Equal(NotificationPanelDock.Bottom, reloaded.Settings.PanelDock);
        Assert.Equal(30, reloaded.Settings.SlaThresholdMinutes);

        var json = await File.ReadAllTextAsync(_storePath);
        Assert.Contains("\"themePreference\": \"Dark\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_RecoversFromCorruptJson()
    {
        await File.WriteAllTextAsync(_storePath, "{ this is not valid json");

        var service = new AppSettingsService(_storePath);
        await service.LoadAsync();

        Assert.Equal(AppThemePreference.System, service.Settings.ThemePreference);
        Assert.NotEmpty(Directory.GetFiles(_tempDirectory, "settings.json.corrupt-*.bak"));
    }

    [Fact]
    public void Normalize_ClampsOutOfRangeValues()
    {
        var settings = new AppSettings
        {
            SlaThresholdMinutes = 999,
            MaxConcurrentWebViews = -5
        };

        settings.Normalize();

        Assert.Equal(AppSettings.MaxSlaThresholdMinutes, settings.SlaThresholdMinutes);
        Assert.Equal(0, settings.MaxConcurrentWebViews);
    }

    [Fact]
    public async Task UpdateAsync_RaisesChangedEvent()
    {
        var service = new AppSettingsService(_storePath);
        await service.LoadAsync();

        var changed = false;
        service.Changed += (_, _) => changed = true;

        await service.UpdateAsync(settings => settings.ShowTaskbarBadge = false);

        Assert.True(changed);
    }

    [Fact]
    public async Task LoadAsync_IsIdempotent()
    {
        var service = new AppSettingsService(_storePath);
        await service.LoadAsync();
        await service.UpdateAsync(settings => settings.EnableAutoUpdate = false);

        await service.LoadAsync();

        Assert.False(service.Settings.EnableAutoUpdate);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
