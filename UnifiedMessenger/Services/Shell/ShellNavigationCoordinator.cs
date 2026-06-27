using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Pages;
using UnifiedMessenger.Services;
using UnifiedMessenger.ViewModels;

namespace UnifiedMessenger.Services.Shell;

public sealed class ShellNavigationCoordinator
{
    private readonly ApplicationServices _services;
    private readonly IShellUiHost _ui;
    private readonly MainWindowViewModel _viewModel;
    private ShellChromeCoordinator? _chrome;

    public ShellNavigationCoordinator(
        ApplicationServices services,
        IShellUiHost ui,
        MainWindowViewModel viewModel)
    {
        _services = services;
        _ui = ui;
        _viewModel = viewModel;
    }

    public void BindChrome(ShellChromeCoordinator chrome) => _chrome = chrome;

    public bool IsDashboardSelected { get; private set; } = true;

    public bool IsSettingsSelected { get; private set; }

    public string? SelectedInstanceId { get; private set; }

    public Frame ContentFrame => _ui.ContentFrame;

    public void RefreshDashboardIfVisible()
    {
        if (_ui.ContentFrame.Content is DashboardPage dashboard)
        {
            dashboard.RefreshAll();
        }
    }

    /// <summary>Forces the command center to redraw avatars even when oversight data is unchanged.</summary>
    public void ForceRefreshDashboardIcons()
    {
        if (_ui.ContentFrame.Content is DashboardPage dashboard)
        {
            dashboard.ForceRefreshIcons();
        }
    }


    public async Task ShowDashboardAsync()
    {
        IsDashboardSelected = true;
        IsSettingsSelected = false;
        SelectedInstanceId = null;

        _viewModel.IsDashboardSelected = true;
        _viewModel.IsSettingsSelected = false;
        _viewModel.IsWorkQueueSelected = false;

        await _services.SessionManager.HideVisibleSessionAsync();
        _ui.InstanceWebViewHost.Visibility = Visibility.Collapsed;
        SetInstanceLoading(false, null);
        _ui.ContentFrame.Visibility = Visibility.Visible;

        var navArgs = PageServices.CreateRegistryArgs(_services);
        NavigateShellFrame(typeof(DashboardPage), navArgs);
        ActiveWorkspaceContext.SetDashboardVisible();
        _ui.AppTitleBar.Subtitle = "Dashboard";
        UpdateBackToDashboardLink(false);
        _chrome?.UpdateShellChromeSelection();
    }

    public async Task ShowSettingsAsync(string? sectionKey = null)
    {
        IsDashboardSelected = false;
        IsSettingsSelected = true;
        SelectedInstanceId = null;

        _viewModel.IsDashboardSelected = false;
        _viewModel.IsSettingsSelected = true;
        _viewModel.IsWorkQueueSelected = false;

        await _services.SessionManager.HideVisibleSessionAsync();
        _ui.InstanceWebViewHost.Visibility = Visibility.Collapsed;
        SetInstanceLoading(false, null);
        _ui.ContentFrame.Visibility = Visibility.Visible;

        var navArgs = PageServices.CreateRegistryArgs(_services, sectionKey);
        NavigateShellFrame(typeof(SettingsPage), navArgs);
        ActiveWorkspaceContext.SetSettingsVisible();
        _ui.AppTitleBar.Subtitle = "Settings";
        UpdateBackToDashboardLink(false);
        _chrome?.UpdateShellChromeSelection();
    }

    public async Task SelectInstanceAsync(string instanceId)
    {
        if (!ShellNavigationService.IsValidInstanceId(instanceId))
        {
            return;
        }

        await NavigateToInstanceAsync(instanceId.Trim()).ConfigureAwait(true);
    }

    public async Task NavigateToInstanceAsync(InstanceNavigationRequest request)
    {
        await SelectInstanceAsync(request.InstanceId).ConfigureAwait(true);

        if (!request.HasConversationTarget)
        {
            return;
        }

        var instance = _services.Registry.FindById(request.InstanceId);
        if (instance is null)
        {
            _services.Navigation.NotifyNavigationFailed(new InstanceNavigationFailedEventArgs
            {
                InstanceId = request.InstanceId,
                ConversationKey = request.ConversationKey,
                Message = "That account is no longer available. Refresh the dashboard and try again."
            });
            return;
        }

        SetInstanceLoading(true, "Opening conversation…");
        try
        {
            var focused = await ConversationFocusHelper.TryFocusConversationWithRetryAsync(
                    _services.SessionManager,
                    instance,
                    request.ConversationKey,
                    request.CustomerName)
                .ConfigureAwait(true);

            if (!focused)
            {
                _services.Navigation.NotifyNavigationFailed(new InstanceNavigationFailedEventArgs
                {
                    InstanceId = request.InstanceId,
                    ConversationKey = request.ConversationKey,
                    Message = "The account opened, but Unified Messenger could not focus the requested chat."
                });

                _ui.DispatcherQueue.TryEnqueue(() =>
                    _ = _services.Dialog.ShowErrorAsync(
                        "Could not open conversation",
                        "The account opened, but Unified Messenger could not focus the requested chat. Open it manually in the inbox."));
            }
        }
        finally
        {
            SetInstanceLoading(false, null);
        }
    }

    private async Task NavigateToInstanceAsync(string instanceId)
    {
        var instance = _services.Registry.FindById(instanceId);
        if (instance is null)
        {
            return;
        }

        SelectedInstanceId = instanceId;
        IsDashboardSelected = false;
        IsSettingsSelected = false;
        _viewModel.IsDashboardSelected = false;
        _viewModel.IsSettingsSelected = false;
        _viewModel.IsWorkQueueSelected = false;
        ActiveWorkspaceContext.SetActiveInstance(instanceId);
        _chrome?.UpdateShellChromeSelection();
        _ui.ContentFrame.Visibility = Visibility.Collapsed;
        _ui.InstanceWebViewHost.Visibility = Visibility.Visible;
        UpdateBackToDashboardLink(true);

        SetInstanceLoading(true, instance.DisplayName);
        try
        {
            await _services.SessionManager.SwitchToAsync(instance);
            _ui.AppTitleBar.Subtitle = instance.DisplayName;
        }
        catch (Exception ex)
        {
            await _services.Dialog.ShowErrorAsync("Could not load instance", ShellErrorFormatter.Format(ex));
        }
        finally
        {
            SetInstanceLoading(false, null);
        }
    }

    public void SetInstanceLoading(bool isLoading, string? message)
    {
        _viewModel.SetInstanceLoading(
            isLoading,
            message ?? (isLoading ? "Loading instance..." : null));
        ApplyInstanceLoadingUi();
    }

    public void ApplyInstanceLoadingUi()
    {
        var showLoading = _viewModel.IsInstanceLoading || _viewModel.ShowStartupWarmProgress;
        _ui.InstanceLoadingPanel.Visibility = showLoading ? Visibility.Visible : Visibility.Collapsed;

        if (_viewModel.ShowStartupWarmProgress)
        {
            _ui.StartupWarmProgressBar.Visibility = Visibility.Visible;
            _ui.StartupWarmProgressBar.Value = _viewModel.StartupWarmProgress;
            _ui.InstanceLoadingText.Text = _viewModel.StartupWarmStatusText;
            return;
        }

        _ui.StartupWarmProgressBar.Visibility = Visibility.Collapsed;
        _ui.InstanceLoadingText.Text = string.IsNullOrWhiteSpace(_viewModel.InstanceLoadingMessage)
            ? "Loading instance..."
            : _viewModel.InstanceLoadingMessage;
    }

    private void NavigateShellFrame(Type pageType, object? parameter)
    {
        // Intentionally clear the back stack on every top-level navigation.
        // This app uses a custom sidebar rail for top-level navigation rather than
        // the Frame's built-in back navigation. Sub-page back (e.g. AboutPage → SettingsPage)
        // works because NavigateToAboutPage pushes SettingsPage→AboutPage without clearing first.
        while (_ui.ContentFrame.CanGoBack)
        {
            _ui.ContentFrame.GoBack();
        }

        _ui.ContentFrame.Navigate(pageType, parameter);
    }

    private void UpdateBackToDashboardLink(bool showInstanceBackLink) =>
        _ui.BackToDashboardButton.Visibility = showInstanceBackLink
            ? Visibility.Visible
            : Visibility.Collapsed;
}
