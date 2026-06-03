using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Pages;

public sealed partial class SettingsPage : Page
{
    private InstanceRegistryService? _registry;
    private bool _suppressToggleEvents;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        EnsureComboBoxesInitialized();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is SettingsNavigationArgs args)
        {
            _registry = args.Registry;
        }

        RefreshAll();
    }

    public void RefreshAll()
    {
        EnsureComboBoxesInitialized();

        var settings = AppSettingsService.Instance.Settings;

        _suppressToggleEvents = true;
        BackgroundToastsToggle.IsOn = settings.EnableBackgroundToasts;
        TaskbarBadgeToggle.IsOn = settings.ShowTaskbarBadge;
        ThemePreferenceBox.SelectedItem = ((ThemePreferenceOption[])ThemePreferenceBox.ItemsSource!)
            .FirstOrDefault(o => o.Preference == settings.ThemePreference)
            ?? ((ThemePreferenceOption[])ThemePreferenceBox.ItemsSource!)[0];
        PanelAutoOpenBox.SelectedItem = ((PanelAutoOpenOption[])PanelAutoOpenBox.ItemsSource!)
            .FirstOrDefault(o => o.Mode == settings.PanelAutoOpen)
            ?? ((PanelAutoOpenOption[])PanelAutoOpenBox.ItemsSource!)[0];
        IncludeMutedBadgesToggle.IsOn = settings.IncludeMutedChatBadges;
        ToastGroupToggle.IsOn = settings.ToastGroupByInstance;
        ToastBrandingToggle.IsOn = settings.ToastUsePlatformBranding;
        SlaThresholdBox.Value = settings.SlaThresholdMinutes;
        _suppressToggleEvents = false;

        RefreshArchivedAccounts();
        RefreshStoragePaths();
        VersionText.Text = $"Unified Messenger {GetAppVersion()}";
    }

    private void EnsureComboBoxesInitialized()
    {
        if (ThemePreferenceBox.ItemsSource is null)
        {
            ThemePreferenceBox.ItemsSource = new[]
            {
                new ThemePreferenceOption(AppThemePreference.System, "Use system setting"),
                new ThemePreferenceOption(AppThemePreference.Light, "Light"),
                new ThemePreferenceOption(AppThemePreference.Dark, "Dark")
            };
            ThemePreferenceBox.DisplayMemberPath = nameof(ThemePreferenceOption.Label);
        }

        if (PanelAutoOpenBox.ItemsSource is null)
        {
            PanelAutoOpenBox.ItemsSource = new[]
            {
                new PanelAutoOpenOption(
                    NotificationPanelAutoOpenMode.UnfocusedOnly,
                    "When app is unfocused or in background"),
                new PanelAutoOpenOption(NotificationPanelAutoOpenMode.Always, "Always on new alerts"),
                new PanelAutoOpenOption(NotificationPanelAutoOpenMode.Never, "Never auto-open")
            };
            PanelAutoOpenBox.DisplayMemberPath = nameof(PanelAutoOpenOption.Label);
        }
    }

    private static string GetAppVersion()
    {
        var version = typeof(App).Assembly.GetName().Version;
        return version is null ? "1.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private void RefreshStoragePaths()
    {
        var appDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UnifiedMessenger");

        InstancesPathText.Text = Path.Combine(appDataRoot, "instances.json");
        ProfilesPathText.Text = WebViewProfileManager.Instance.UserDataFolder;
    }

    private void RefreshArchivedAccounts()
    {
        if (_registry is null)
        {
            ArchivedAccountsList.ItemsSource = null;
            NoArchivedAccountsText.Visibility = Visibility.Visible;
            return;
        }

        var items = _registry.ArchivedInstances
            .OrderBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(i => new ArchivedAccountItem(
                i.Id,
                i.DisplayName,
                PlatformDefinition.FindById(i.Platform)?.DisplayName ?? i.Platform))
            .ToList();

        ArchivedAccountsList.ItemsSource = items;
        NoArchivedAccountsText.Visibility = items.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async void BackgroundToastsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.EnableBackgroundToasts = BackgroundToastsToggle.IsOn);
    }

    private async void TaskbarBadgeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.ShowTaskbarBadge = TaskbarBadgeToggle.IsOn);

        _ = TaskbarBadgeService.Instance.SyncBadgeAsync(NotificationHub.Instance.TotalUnreadCount);
    }

    private async void ThemePreferenceBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressToggleEvents || ThemePreferenceBox.SelectedItem is not ThemePreferenceOption option)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.ThemePreference = option.Preference);

        ThemeService.Apply(option.Preference);
    }

    private async void PanelAutoOpenBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressToggleEvents || PanelAutoOpenBox.SelectedItem is not PanelAutoOpenOption option)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.PanelAutoOpen = option.Mode);
    }

    private async void IncludeMutedBadgesToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.IncludeMutedChatBadges = IncludeMutedBadgesToggle.IsOn);

        await InstanceSessionManager.Instance.BroadcastAdapterSettingsAsync();
    }

    private async void ToastGroupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.ToastGroupByInstance = ToastGroupToggle.IsOn);
    }

    private async void ToastBrandingToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.ToastUsePlatformBranding = ToastBrandingToggle.IsOn);
    }

    private async void SlaThresholdBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressToggleEvents || double.IsNaN(args.NewValue))
        {
            return;
        }

        var minutes = (int)Math.Clamp(Math.Round(args.NewValue), 5, 120);
        if (Math.Abs(minutes - args.NewValue) > 0.01)
        {
            sender.Value = minutes;
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.SlaThresholdMinutes = minutes);

        ShellNavigationService.Instance.RequestDashboardRefresh();
    }

    private void ClearNotificationsButton_Click(object sender, RoutedEventArgs e)
    {
        NotificationHub.Instance.ClearAlerts();
    }

    private void RestoreAccountButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string instanceId })
        {
            ShellNavigationService.Instance.RequestArchivedInstanceRestore(instanceId);
        }
    }

    private void AboutLink_Click(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(AboutPage));
    }

    private sealed record ThemePreferenceOption(AppThemePreference Preference, string Label);

    private sealed record PanelAutoOpenOption(NotificationPanelAutoOpenMode Mode, string Label);
}
