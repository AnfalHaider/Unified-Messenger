using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using UnifiedMessenger.Dialogs;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Pages;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        RefreshContent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
    }

    private void RefreshContent()
    {
        VersionText.Text = AboutPageHelper.BuildAboutVersionLabel(typeof(App).Assembly.GetName().Version);
    }

    private async void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        var services = ApplicationServiceProvider.Current;
        var result = await services.GitHubUpdate.CheckForUpdatesManualAsync();

        // When an update is available, offer to install it — not just report the version.
        if (result.Status == UpdateCheckStatus.UpdateAvailable &&
            !string.IsNullOrWhiteSpace(result.DownloadUrl) &&
            result.LatestVersion is not null)
        {
            var prompt = new AutoUpdateDialog(
                result.CurrentVersion?.ToString() ?? "this version",
                result.LatestVersion.ToString())
            {
                XamlRoot = XamlRoot
            };

            if (await prompt.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            try
            {
                // Downloads, verifies SHA-256, launches the installer, and exits the app.
                await services.GitHubUpdate.ApplyUpdateAsync(result);
            }
            catch (Exception ex)
            {
                await new ContentDialog
                {
                    Title = "Update failed",
                    CloseButtonText = "OK",
                    XamlRoot = XamlRoot,
                    Content = new TextBlock
                    {
                        Text = $"Could not install the update: {ex.Message}",
                        TextWrapping = TextWrapping.WrapWholeWords
                    }
                }.ShowAsync();
            }

            return;
        }

        await new ContentDialog
        {
            Title = "Update check",
            CloseButtonText = "OK",
            XamlRoot = XamlRoot,
            Content = new TextBlock
            {
                Text = SettingsPageHelper.BuildUpdateCheckMessage(result),
                TextWrapping = TextWrapping.WrapWholeWords
            }
        }.ShowAsync();
    }

    private void SettingsBreadcrumb_Click(object sender, RoutedEventArgs e)
    {
        if (Frame?.CanGoBack == true)
        {
            Frame.GoBack();
        }
    }

    private void BackLink_Click(object sender, RoutedEventArgs e) =>
        SettingsBreadcrumb_Click(sender, e);
}
